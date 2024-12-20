﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Com.H.Events;
using Com.H.IO;
using Com.H.Linq;
using Com.H.Text;

namespace Com.H.Threading.Scheduler
{
    public class HTaskScheduler : IDisposable
    {
        #region properties
        /// <summary>
        /// The time interval in miliseconds of how often the scheduler checks on tasks eligibility to run.
        /// Default value is 1000 miliseconds
        /// </summary>
        public int TickInterval { get; set; }
        private string? XmlConfigPath { get; set; }
        private IHTaskTimeLogger? TimeLog { get; set; }
        public IHTaskCollection? Tasks { get; private set; }
        private CancellationTokenSource? Cts { get; set; }
        private AtomicGate RunSwitch { get; set; }
        private AtomicGate TasksLoadSwitch { get; set; }
        private TrafficController ThreadTraffic { get; set; }

        #endregion

        #region constructor
        /// <summary>
        /// Default constructor that uses a local xml config file to load tasks configuration.
        /// For custom configuration from Database, Json files, etc.. implement 
        /// IHTaskCollection and IHTaskTimeLog interfaces and make use of the overloaded constructor
        /// that accepts those interfaces to integrate your custom config / logging workflow with the scheduler
        /// framework.
        /// </summary>
        /// <param name="xmlConfigPath"></param>
        public HTaskScheduler(string xmlConfigPath)
        {
            if (string.IsNullOrWhiteSpace(xmlConfigPath))
                throw new ArgumentNullException(nameof(xmlConfigPath));
            this.XmlConfigPath = xmlConfigPath;
            this.ThreadTraffic = new TrafficController();
            this.TickInterval = 1000;
            this.RunSwitch = new AtomicGate();
            this.TasksLoadSwitch = new AtomicGate();
        }

        /// <summary>
        /// You can make use of this constructor to provide your implemnetation of the IEnumerable<IHTaskItem>
        /// and IHTaskTimeLog if you wish to incorporate your config / loggin workflow into this schedular
        /// framework.
        /// Leaving timeLog empty defaults the schedular to do logging in memory only. The disadvantage is 
        /// after a restart from a shutdown to the app, the schedular won't have information on what 
        /// tasks it already ran prior the shutdown which would result in running all tasks including the ones that already ran.
        /// </summary>
        /// <param name="tasks"></param>
        /// <param name="timeLog"></param>
        public HTaskScheduler(IHTaskCollection tasks, IHTaskTimeLogger? timeLog = null)
        {
            this.Tasks = tasks ?? throw new ArgumentNullException(nameof(tasks));
            this.TimeLog = timeLog ?? new XmlFileHTaskTimeLogger();
            this.ThreadTraffic = new TrafficController();
            this.TickInterval = 1000;
            this.RunSwitch = new AtomicGate();
            this.TasksLoadSwitch = new AtomicGate();
        }
        #endregion

        #region load
        private void Load()
        {
            if (!this.TasksLoadSwitch.TryOpen()) return;

            if (string.IsNullOrWhiteSpace(this.XmlConfigPath)
            ) throw new ArgumentNullException(
                "no tasks file or folder path defined");

            if (!File.Exists(this.XmlConfigPath)
                && !Directory.Exists(this.XmlConfigPath))
            {
                throw new FileNotFoundException(this.XmlConfigPath);
            }
            this.Tasks = new XmlFileHTaskCollection(this.XmlConfigPath, this.Cts?.Token);
            this.Tasks.Error += (s, e, c) => this.RaiseTaskLoadingErrorAsync(e);

            this.TimeLog = new XmlFileHTaskTimeLogger(
                Path.Combine(
                    Directory.GetParent(this.XmlConfigPath)?.FullName
                    ?? AppContext.BaseDirectory, // AppDomain.CurrentDomain.BaseDirectory,
                    new FileInfo(this.XmlConfigPath).Name + ".log"));
            foreach (var task in this.Tasks.Where(x => x?.Schedule?.IgnoreLogOnRestart == true
                            && this.TimeLog[x.UniqueKey] != null
                ))
            {
                // this.TimeLog[task.UniqueKey] cannot be null here.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                this.TimeLog[task.UniqueKey].LastExecuted = null;
                this.TimeLog[task.UniqueKey].ErrorCount = 0;
                this.TimeLog[task.UniqueKey].LastError = null;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            }


        }

        #endregion



        


        #region start / stop
        /// <summary>
        /// Start monitoring scheduled tasks in order to trigger IsTaskDue event when tasks are ready for execution.
        /// </summary>
        /// <param name="cancellationToken">If provided, the monitoring force stops all running tasks</param>
        /// <returns>a running monitoring task</returns>
        public void Start(CancellationToken? cancellationToken = null)
        {
            _ = StartAsync(cancellationToken);
        }


        /// <summary>
        /// Start monitoring scheduled tasks in order to trigger IsTaskDue event when tasks are ready for execution.
        /// </summary>
        /// <param name="cancellationToken">If provided, the monitoring force stops all running tasks</param>
        /// <returns>a running monitoring task</returns>
        public async Task StartAsync(CancellationToken? cancellationToken = null)
        {
            if (!this.RunSwitch.TryOpen()) return; // Task.CompletedTask;
            this.Load();
            this.Cts = cancellationToken == null
                ? new CancellationTokenSource()
                : CancellationTokenSource.CreateLinkedTokenSource(
                    (CancellationToken)cancellationToken);
            try
            {
                await MonitorTasks();
            }
            catch (OperationCanceledException)
            {
            }

        }


        /// <summary>
        /// Stops monitoring tasks schedule, and terminates running tasks.
        /// </summary>
        public void Stop()
        {
            if (!this.RunSwitch.TryClose()) return;
            if (this.Cts?.IsCancellationRequested == true) return;
            try
            {
                this.Cts?.Cancel();
            }
            catch { }
        }

        #endregion

        #region monitor
        private async Task MonitorTasks()
        {
            if (this.Cts is null) 
                throw new NullReferenceException("Cts is null in HTaskScheduler.MonitorTasks");
            ParallelOptions options = new()
            {
                CancellationToken = this.Cts.Token // ,
                                                   // MaxDegreeOfParallelism = Environment.ProcessorCount
            };

            while (this.Cts.IsCancellationRequested == false)
            {
                if (this.Tasks is not null)
                {
                    _ = Parallel.ForEachAsync(this.Tasks, options, async (task, cToken) =>
                    {
                        if (task is null || this.Cts.IsCancellationRequested) return;
                        await this.ThreadTraffic.QueueCallAsync(async () =>
                        {
                            await Process(task);
                        }, 0, task.UniqueKey);
                    });
                }

                    //await Parallel.ForEachAsync(
                    //    this.Tasks, options, async (task, cToken)  =>
                    //{
                    //    if (task is null) return;
                    //    await Cancellable.CancellableRunAsync(() =>
                    //    {
                    //        _ = this.ThreadTraffic.QueueCallAsync(async () =>
                    //        {
                    //            await Process(task);
                    //        }, 0, task.UniqueKey);
                    //    }, cToken);
                    //});
                
                await Task.Delay(this.TickInterval, this.Cts.Token);
            }
        }



        private async Task Process(IHTaskItem task)
        {
            if (this.Cts is null) throw new NullReferenceException("Cts is null in HTaskScheduler.Start()");
            HTaskEventArgs? evArgs = new(
                        this,
                        task,
                        this.Cts.Token
                        );
            try
            {
                // check if task is eligible to run including retry on error status (if failed to run in an earlier attempt and the schedule
                // for when to retry and retry max attempts).

                // reset vars for the session
                task.Vars = null;

                if (!this.IsDue(task)) return;


                if (task.Schedule?.Repeat is not null)
                {
                    var delaySwitch = new AtomicGate();

                    foreach (var repeatDataModel in task.Schedule.Repeat.EnsureEnumerable())
                    {
                        if (task.Schedule?.RepeatDelayInterval > 0
                            && !delaySwitch.TryOpen())
                        {
                            if (this.Cts?.Token is not null)
                                await Task.Delay((int)task.Schedule.RepeatDelayInterval, (CancellationToken)this.Cts.Token);
                            else await Task.Delay((int)task.Schedule.RepeatDelayInterval);
                        }
                        if (this.Cts?.IsCancellationRequested == true) return;

                        static IEnumerable<IHTaskItem> AllChildren(IHTaskItem item)
                        {
                            return (item.Children?
                                .Where(x => x is not null)
                                // x won't be null here
#pragma warning disable CS8604 // Possible null reference argument.
                                .SelectMany(x => AllChildren(x)) ??
#pragma warning restore CS8604 // Possible null reference argument.
                                // Enumerable.Empty<IHTaskItem>()).Append(item);
                                []).Append(item);
                        }
                        foreach (var child in AllChildren(task)
                            .Where(x => x?.Vars is not null)
                            //.Where(x => x.Vars?.Custom == null)
                            )
                            // Vars already checked for null
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                            child.Vars.Custom = repeatDataModel;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                        if (this.Cts?.IsCancellationRequested is true) return;
                        await this.RaiseTaskIsDueAsync(evArgs);
                    }
                }
                else
                {
                    if (this.Cts?.IsCancellationRequested is true) return;
                    await this.RaiseTaskIsDueAsync(evArgs);
                }

                    // log successful run, and reset retry on error logic in case it was previously set.
                if (task.UniqueKey is null) throw new InvalidOperationException("task.UniqueKey is not set");
                // TimeLog[index] creates an entry on-the-fly for the index if index not found.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                this.TimeLog[task.UniqueKey].LastExecuted = DateTime.Now;
                this.TimeLog[task.UniqueKey].ErrorCount = 0;
                this.TimeLog[task.UniqueKey].LastError = null;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                this.TimeLog.Save();
            }
            catch (Exception ex)
            {
                // catch errors within the thread, and check if retry on error is enabled
                // if enabled, don't throw exception, trigger OnErrorEvent async, then log error retry attempts and last error
                await this.RaiseTaskExecutionErrorAsync(new HTaskExecutionErrorEventArgs(this, ex, evArgs));
                if (task?.Schedule?.RetryAttemptsAfterError is null || task.UniqueKey is null) throw;
                // TimeLog[index] creates an entry on-the-fly for the index if index not found.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                this.TimeLog[task.UniqueKey].ErrorCount++;
                this.TimeLog[task.UniqueKey].LastError = DateTime.Now;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                this.TimeLog.Save();
            }

        }


        #region is due
        private bool IsDue(IHTaskItem item)
        {
            if (item?.Schedule == null) return false;


            DateTime timeNow = DateTime.Now;

            var log = this.TimeLog?[item.UniqueKey];

            #region error retry check
            if (item.Schedule.RetryAttemptsAfterError > 0
                &&
                log?.LastError != null
                &&
                (
                    log.ErrorCount > item.Schedule.RetryAttemptsAfterError
                    ||
                    (
                            item.Schedule.RetryInMilisecAfterError != null
                            &&
                            timeNow <
                            ((DateTime)log.LastError).AddMilliseconds(
                                (int)item.Schedule.RetryInMilisecAfterError)
                    )
                )
                )
                return false;
            #endregion


            #region not before
            if (item.Schedule.NotBefore != null
                && timeNow < item.Schedule.NotBefore) return false;
            #endregion

            #region not after
            if (item.Schedule.NotAfter != null
                && timeNow > item.Schedule.NotAfter) return false;
            #endregion

            #region dates
            if (item.Schedule.Dates != null
                &&
                !item.Schedule.Dates.Any(x =>
                timeNow >= x && timeNow < x.Date.AddDays(1))) return false;

            #endregion

            #region days of the year
            if (item.Schedule.DaysOfYear != null
                && !item.Schedule.DaysOfYear.Any(x => x == timeNow.DayOfYear))
                return false;
            #endregion

            #region end of the month
            if (item.Schedule.LastDayOfMonth == true
                && timeNow.AddDays(1).Day != 1) return false;
            #endregion

            #region days of the month
            if (item.Schedule.DaysOfMonth != null
                && !item.Schedule.DaysOfMonth.Any(x => x == timeNow.Day))
                return false;
            #endregion

            #region days of the week
            if (item.Schedule.DaysOfWeek != null
                && !item.Schedule.DaysOfWeek
                .Any(x => x.EqualsIgnoreCase(timeNow.DayOfWeek.ToString())))
                return false;
            #endregion

            #region time
            if (item.Schedule.Time != null
                && timeNow < (timeNow.Date + item.Schedule.Time))
                return false;
            #endregion

            #region until time
            if (item.Schedule.UntilTime != null
                && timeNow > (timeNow.Date + item.Schedule.UntilTime))
                return false;
            #endregion

            #region interval & last executed logic

            if (item.Schedule.Interval != null)
            {
                if (log?.LastExecuted != null
                    && item.Schedule.Interval >
                    ((TimeSpan)(timeNow - log.LastExecuted)).TotalMilliseconds)
                    return false;
            }
            else
            {
                if (log?.LastExecuted?.Date == timeNow.Date)
                    return false;
            }
            #endregion

            #region is enabled
            if (!item.Schedule.Enabled) return false;
            #endregion

            return true;

        }


        #endregion


        #endregion
        #region events
        

        #region OnTaskIsDue

        /// <summary>
        /// Gets triggered whenever a task is due for execution
        /// </summary>
        public event AsyncEventHandler<HTaskEventArgs> TaskIsDue;
        protected async Task RaiseTaskIsDueAsync(HTaskEventArgs e)
        {

            if (e is null || TaskIsDue is null) return; // Task.CompletedTask;
            if (this.Cts is null)
                throw new NullReferenceException("Cts is null in HTaskScheduler.RaiseTaskIsDueAsync()");
            // Get the invocation list and invoke each handler asynchronously

            //var tasks = new List<Task>();
            //foreach (AsyncEventHandler<HTaskEventArgs> handler in TaskIsDue.GetInvocationList())
            //{
            //    tasks.Add(handler(e.Sender, e, this.Cts.Token));
            //}

            if (this.Cts.Token.IsCancellationRequested) return;

            await Task.WhenAll(TaskIsDue.GetInvocationList()
                .Cast<AsyncEventHandler<HTaskEventArgs>>()
                .Select(handler =>
                    handler(e.Sender, e, this.Cts.Token)
                ));


            //await Task.WhenAll(tasks);
        }


        #endregion

        #region OnTaskExecutionError

        /// <summary>
        /// Gets triggered whenever there is an error that might get supressed if retry on error is enabled
        /// </summary>
        public event AsyncEventHandler<HTaskExecutionErrorEventArgs> TaskExecutionError;
        protected async Task RaiseTaskExecutionErrorAsync(HTaskExecutionErrorEventArgs e)
        {
            if (e == null || TaskExecutionError is null) return;
            if (this.Cts is null)
                throw new NullReferenceException("Cts is null in HTaskScheduler.RaiseTaskExecutionErrorAsync()");
            var tasks = new List<Task>();

            await Task.WhenAll(TaskExecutionError.GetInvocationList()
                .Cast<AsyncEventHandler<HTaskExecutionErrorEventArgs>>()
                .Select(handler =>
                    handler(e.Sender, e, this.Cts.Token)
                ));
        }

        #endregion




        #region OnTaskLoadingError


        /// <summary>
        /// Gets triggered whenever there is an error loading tasks.
        /// </summary>
        public event AsyncEventHandler<HErrorEventArgs> TaskLoadingError;
        protected async Task RaiseTaskLoadingErrorAsync(HErrorEventArgs e)
        {
            if (e is null || TaskIsDue is null) return; // Task.CompletedTask;
            if (this.Cts is null)
                throw new NullReferenceException("Cts is null in HTaskScheduler.RaiseTaskLoadingErrorAsync()");

            if (this.Cts.Token.IsCancellationRequested) return;

            await Task.WhenAll(TaskLoadingError.GetInvocationList()
                .Cast<AsyncEventHandler<HErrorEventArgs>>()
                .Select(handler =>
                    handler(e.Sender, e, this.Cts.Token)
                ));
        }

        #endregion

        #endregion

        #region disposable 
        private bool disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    try
                    {
                        this.Cts?.Cancel();
                    }
                    catch { }
                    try
                    {
                        this.RunSwitch.TryClose();
                    }
                    catch { }
                    try
                    {
                        this.TasksLoadSwitch.TryClose();
                    }
                    catch { }

                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~HTaskScheduler()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
