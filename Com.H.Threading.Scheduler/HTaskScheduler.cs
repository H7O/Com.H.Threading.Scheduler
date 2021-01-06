using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Com.H.IO;
using Com.H.Linq;
using Com.H.Text;

namespace Com.H.Threading.Scheduler
{
    public class HTaskScheduler : IDisposable
    {

        #region properties
        /// <summary>
        /// The time interval in miliseconds of how often the schedular checks on tasks eligibility to run.
        /// Default value is 1000 miliseconds
        /// </summary>
        public int TickInterval { get; set; }
        private string FilePath { get; set; }
        private IHTaskTimeLogger TimeLog { get; set; }
        public IHTaskCollection Tasks { get; private set; }
        private CancellationTokenSource Cts { get; set; }
        private AtomicGate RunSwitch { get; set; }
        private TrafficController ThreadTraffic { get; set; }

        #endregion

        #region constructor
        /// <summary>
        /// Default constructor that uses a local xml config file to load tasks configuration.
        /// For custom configuration from Database, Json files, etc.. implement 
        /// IHTaskCollection and IHTaskTimeLog interfaces and make use of the overloaded constructor
        /// that accepts those interfaces to integrate your custom config / logging workflow with the schedular
        /// framework.
        /// </summary>
        /// <param name="xmlConfigFilePath"></param>
        public HTaskScheduler(string xmlConfigFilePath)
        {
            if (string.IsNullOrWhiteSpace(xmlConfigFilePath))
                throw new ArgumentNullException(nameof(xmlConfigFilePath));
            this.FilePath = xmlConfigFilePath;
            this.ThreadTraffic = new TrafficController();
            this.TickInterval = 1000;
            // this.Load();
            this.RunSwitch = new AtomicGate();
            this.Load();
        }

        /// <summary>
        /// You can make use of this constructor to provide your implemnetation of the IEnumerable<IHTaskItem>
        /// and IHTaskTimeLog if you wish to incorporate your config / loggin workflow into this schedular
        /// framework.
        /// Leaving timeLog empty defaults the schedular to do logging in memory only. The disadvantage is 
        /// after a restart from a shutdown to the app, the schedular won't have information on what 
        /// task it already ran prior the shutdown which would result in running the again.
        /// </summary>
        /// <param name="tasks"></param>
        /// <param name="timeLog"></param>
        public HTaskScheduler(IHTaskCollection tasks, IHTaskTimeLogger timeLog = null)
        {
            this.Tasks = tasks ?? throw new ArgumentNullException(nameof(tasks));
            this.TimeLog = timeLog ?? new XmlFileHTaskTimeLogger();
            this.ThreadTraffic = new TrafficController();
            this.TickInterval = 1000;
            this.RunSwitch = new AtomicGate();
        }
        #endregion

        #region load
        private void Load()
        {
            if (string.IsNullOrWhiteSpace(this.FilePath)
                && (this.Tasks?.Any() == false)
                ) throw new ArgumentNullException(
                    "no file path or tasks defined");

            if (string.IsNullOrWhiteSpace(this.FilePath) == false
                && File.Exists(this.FilePath) == false)
            {
                if (this.Tasks?.Any() == false)
                    throw new FileNotFoundException(this.FilePath);
                return;
            }

            this.Tasks = new XmlFileHTaskCollection(this.FilePath);
            this.TimeLog = new XmlFileHTaskTimeLogger(
                Path.Combine(
                    Directory.GetParent(this.FilePath).FullName,
                    new FileInfo(this.FilePath).Name + ".log"));
            foreach (var task in this.Tasks.Where(x => x.Schedule.IgnoreLogOnRestart
                && this.TimeLog[x.UniqueKey] != null
                ))
            {
                this.TimeLog[task.UniqueKey].LastExecuted = null;
                this.TimeLog[task.UniqueKey].ErrorCount = 0;
                this.TimeLog[task.UniqueKey].LastError = null;
            }

        }
        #endregion

        #region start / stop
        /// <summary>
        /// Start monitoring scheduled tasks in order to trigger IsTaskDue event when tasks are ready for execution.
        /// </summary>
        /// <param name="cancellationToken">If provided, the monitoring force stops all running tasks</param>
        /// <returns>a running monitoring task</returns>
        public Task Start(CancellationToken? cancellationToken = null)
        {
            if (!this.RunSwitch.TryOpen()) return Task.CompletedTask;
            //this.Load();
            this.Cts = cancellationToken == null
                ? new CancellationTokenSource()
                : CancellationTokenSource.CreateLinkedTokenSource(
                    (CancellationToken)cancellationToken);
            return Cancellable.CancellableRunAsync(MonitorTasks, this.Cts.Token);
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
        private void MonitorTasks()
        {
            while (!this.Cts.IsCancellationRequested)
            {
                foreach (var task in this.Tasks)
                {
                    Cancellable.CancellableRunAsync(() =>
                    {
                        this.ThreadTraffic.QueueCall(() =>
                        {
                            Process(task);
                        }, 0, task.UniqueKey);
                    }, this.Cts.Token);
                }
                Task.Delay(this.TickInterval, this.Cts.Token).GetAwaiter().GetResult();
            }
        }

        private void Process(IHTaskItem task)
        {
            HTaskSchedulerEventArgs evArgs = null;
            try
            {
                // check if task is eligible to run including retry on error status (if failed to run in an earlier attempt and the schedule
                // for when to retry and retry max attempts).


                if (!this.IsDue(task)) return;


                void RunTask()
                {

                    if (this.Cts.IsCancellationRequested) return;

                    // todo: threaded having continuewith to check
                    // run status

                    evArgs = new HTaskSchedulerEventArgs(
                        this,
                        task,
                        this.Cts.Token
                        );

                    // if eligible to run (and has no previous error logged), trigger synchronous event
                    this.OnTaskIsDueAsync(evArgs)
                        .GetAwaiter().GetResult();
                }

                if (task.Schedule?.Repeat != null)
                {
                    AtomicGate delaySwitch = new AtomicGate();

                    foreach (var repeatDataModel in task.Schedule?.Repeat.EnsureEnumerable())
                    {
                        if (task.Schedule?.RepeatDelayInterval > 0
                            && !delaySwitch.TryOpen())
                        {
                            if (this.Cts?.Token != null)
                                Task.Delay((int)task.Schedule.RepeatDelayInterval, (CancellationToken)this.Cts.Token)
                                    .GetAwaiter().GetResult();
                            else Task.Delay((int)task.Schedule.RepeatDelayInterval)
                                    .GetAwaiter().GetResult();
                        }
                        if (this.Cts.IsCancellationRequested) return;
                        IEnumerable<IHTaskItem> AllChildren(IHTaskItem item)
                        {
                            return (item.Children?.SelectMany(x => AllChildren(x)) ??
                                Enumerable.Empty<IHTaskItem>()).Append(item);
                        }
                        foreach (var child in AllChildren(task)
                            //.Where(x => x.Vars?.Custom == null)
                            )
                            child.Vars.Custom = repeatDataModel;
                        RunTask();
                    }
                }
                else RunTask();

                // log successful run, and reset retry on error logic in case it was previously set.
                this.TimeLog[task.UniqueKey].LastExecuted = DateTime.Now;
                this.TimeLog[task.UniqueKey].ErrorCount = 0;
                this.TimeLog[task.UniqueKey].LastError = null;
                this.TimeLog.Save();
            }
            catch (Exception ex)
            {
                // catch errors within the thread, and check if retry on error is enabled
                // if enabled, don't throw exception, trigger OnErrorEvent async, then log error retry attempts and last error
                this.OnErrorAsync(new HTaskSchedularErrorEventArgs(this, ex, evArgs));
                if (task.Schedule.RetryAttemptsAfterError == null) throw;
                this.TimeLog[task.UniqueKey].ErrorCount++;
                this.TimeLog[task.UniqueKey].LastError = DateTime.Now;
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
                !item.Schedule.Dates.Any(x=>
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
                if (log.LastExecuted?.Date == timeNow.Date)
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

        public delegate void TaskIsDueEventHandler(object sender, HTaskSchedulerEventArgs e);


        /// <summary>
        /// Gets triggered whenever a task is due for execution
        /// </summary>
        public event TaskIsDueEventHandler TaskIsDue;
        protected virtual Task OnTaskIsDueAsync(HTaskSchedulerEventArgs e)
        {

            if (e == null) return Task.CompletedTask;
            return Cancellable.CancellableRunAsync(
                () => TaskIsDue?.Invoke(e.Sender, e)
                , this.Cts.Token);
        }

        #endregion

        #region OnError

        public delegate void ErrorEventHandler(object sender, HTaskSchedularErrorEventArgs e);
        /// <summary>
        /// Gets triggered whenever there is an error that might get supressed if retry on error is enabled
        /// </summary>
        public event ErrorEventHandler Error;
        protected virtual Task OnErrorAsync(HTaskSchedularErrorEventArgs e)
        {
            if (e == null) return Task.CompletedTask;
            return Cancellable.CancellableRunAsync(
                () => Error?.Invoke(e.Sender, e)
                , this.Cts.Token);
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
                        this.Cts.Cancel();
                    }
                    catch { }
                    try
                    {
                        this.RunSwitch.TryClose();
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
