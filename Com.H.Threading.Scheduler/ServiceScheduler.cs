using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Com.H.IO;
using Com.H.Text;

namespace Com.H.Threading.Scheduler
{
    public class ServiceScheduler : IDisposable
    {

        #region properties
        /// <summary>
        /// The time interval in miliseconds of how often the schedular checks on services eligibility to run.
        /// Default value is 1000 miliseconds
        /// </summary>
        public int TickInterval { get; set; }
        private string FilePath { get; set; }
        private IServiceTimeLogger TimeLog { get; set; }
        private ICollection<IServiceItem> Services { get; set; }
        private CancellationTokenSource Cts { get; set; }
        private AtomicGate RunSwitch { get; set; }
        private TrafficController ThreadTraffic { get; set; }

        #endregion

        #region constructor
        /// <summary>
        /// Default constructor that uses a local xml config file to load services configuration.
        /// For custom configuration from Database, Json files, etc.. implement 
        /// IServiceCollection and IServiceTimeLog interfaces and make use of the overloaded constructor
        /// that accepts those interfaces to integrate your custom config / logging workflow with the schedular
        /// framework.
        /// </summary>
        /// <param name="xmlConfigFilePath"></param>
        public ServiceScheduler(string xmlConfigFilePath)
        {
            if (string.IsNullOrWhiteSpace(xmlConfigFilePath))
                throw new ArgumentNullException(nameof(xmlConfigFilePath));
            this.FilePath = xmlConfigFilePath;
            this.ThreadTraffic = new TrafficController();
            this.TickInterval = 1000;
            // this.Load();
            this.RunSwitch = new AtomicGate();
        }

        /// <summary>
        /// You can make use of this constructor to provide your implemnetation of the IEnumerable<IServiceItem>
        /// and IServiceTimeLog if you wish to incorporate your config / loggin workflow into this schedular
        /// framework.
        /// Leaving timeLog empty defaults the schedular to do logging in memory only. The disadvantage is 
        /// after a restart from a shutdown to the app, the schedular won't have information on what 
        /// service it already ran prior the shutdown which would result in running the again.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="timeLog"></param>
        public ServiceScheduler(ICollection<IServiceItem> services, IServiceTimeLogger timeLog = null)
        {
            this.Services = services ?? throw new ArgumentNullException(nameof(services));
            this.TimeLog = timeLog ?? new XmlFileServiceTimeLogger();
            this.ThreadTraffic = new TrafficController();
            this.TickInterval = 1000;
            this.RunSwitch = new AtomicGate();
        }
        #endregion

        #region load
        private void Load()
        {
            if (string.IsNullOrEmpty(this.FilePath)
                && (this.Services?.Any() == false)
                ) throw new ArgumentNullException(
                    "no file path or services defined");

            if (string.IsNullOrWhiteSpace(this.FilePath) == false
                && File.Exists(this.FilePath) == false)
            {
                if (this.Services?.Any() == false)
                    throw new FileNotFoundException(this.FilePath);
                return;
            }

            this.Services = new XmlFileServiceCollection(this.FilePath);
            this.TimeLog = new XmlFileServiceTimeLogger(Directory.GetParent(this.FilePath).FullName
                + IOExtensions.DirectorySeperatorString
                + new FileInfo(this.FilePath).Name + ".log");
            foreach (var service in this.Services.Where(x => x.Schedule.IgnoreLogOnRestart
                && this.TimeLog[x.UniqueKey] != null
                ))
            {
                this.TimeLog[service.UniqueKey].LastExecuted = null;
                this.TimeLog[service.UniqueKey].ErrorCount = 0;
                this.TimeLog[service.UniqueKey].LastError = null;
            }

        }
        #endregion

        #region start / stop
        /// <summary>
        /// Start monitoring scheduled services in order to trigger IsServiceDue event when services are ready for execution.
        /// </summary>
        /// <param name="cancellationToken">If provided, the monitoring force stops all running services</param>
        /// <returns>a running monitoring task</returns>
        public Task Start(CancellationToken? cancellationToken = null)
        {
            if (!this.RunSwitch.TryOpen()) return Task.CompletedTask;
            this.Load();
            this.Cts = cancellationToken == null
                ? new CancellationTokenSource()
                : CancellationTokenSource.CreateLinkedTokenSource(
                    (CancellationToken) cancellationToken);
            return Cancellable.CancellableRunAsync(MonitorServices, this.Cts.Token);
        }
        /// <summary>
        /// Stops monitoring services schedule, and terminates running services.
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
        private void MonitorServices()
        {
            while (!this.Cts.IsCancellationRequested)
            {
                foreach (var service in this.Services)
                {
                    Cancellable.CancellableRunAsync(() =>
                    {
                        this.ThreadTraffic.QueueCall(() =>
                        {
                            Process(service);
                        }, 0, service.UniqueKey);
                    }, this.Cts.Token);
                }
                Task.Delay(this.TickInterval, this.Cts.Token).GetAwaiter().GetResult();
            }
        }

        private void Process(IServiceItem service)
        {
            ServiceSchedulerEventArgs evArgs = null;
            try
            {
                // check if service is eligible to run including retry on error status (if failed to run in an earlier attempt and the schedule
                // for when to retry and retry max attempts).
                

                if (!this.IsDue(service)) return;
                
                
                void RunService()
                {
                    
                    if (this.Cts.IsCancellationRequested) return;

                    // todo: threaded having continuewith to check
                    // run status

                    evArgs = new ServiceSchedulerEventArgs(
                        this,
                        service,
                        this.Cts.Token
                        );

                    // if eligible to run (and has no previous error logged), trigger synchronous event
                    this.OnServiceIsDueAsync(evArgs)
                        .GetAwaiter().GetResult();
                }

                if (service.Schedule?.Repeat != null)
                    foreach (var repeatDataModel in service.Schedule?.Repeat)
                    {
                        service.Vars.Custom = repeatDataModel;
                        RunService();
                        // todo: between repeat sleep timer goes here
                    }
                else RunService();
                
                // log successful run, and reset retry on error logic in case it was previously set.
                this.TimeLog[service.UniqueKey].LastExecuted = DateTime.Now;
                this.TimeLog[service.UniqueKey].ErrorCount = 0;
                this.TimeLog[service.UniqueKey].LastError = null;
                this.TimeLog.Save();
            }
            catch (Exception ex)
            {
                // catch errors within the thread, and check if retry on error is enabled
                // if enabled, don't throw exception, trigger OnErrorEvent async, then log error retry attempts and last error
                this.OnErrorAsync(new ServiceSchedularErrorEventArgs(this, ex, evArgs));
                if (service.Schedule.RetryAttemptsAfterError == null) throw;
                this.TimeLog[service.UniqueKey].ErrorCount++;
                this.TimeLog[service.UniqueKey].LastError = DateTime.Now;
                this.TimeLog.Save();
            }

        }

        #region is due
        private bool IsDue(IServiceItem item)
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

            #region date
            if (item.Schedule.Date != null
                && timeNow.Date != item.Schedule.Date) return false;

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

        #region OnServiceIsDue

        public delegate void ServiceIsDueEventHandler(object sender, ServiceSchedulerEventArgs e);


        /// <summary>
        /// Gets triggered whenever a service is due for execution
        /// </summary>
        public event ServiceIsDueEventHandler ServiceIsDue;
        protected virtual Task OnServiceIsDueAsync(ServiceSchedulerEventArgs e)
        {

            if (e == null) return Task.CompletedTask;
            return Cancellable.CancellableRunAsync(
                () => ServiceIsDue?.Invoke(e.Sender, e)
                , this.Cts.Token);
        }

        #endregion

        #region OnError

        public delegate void ErrorEventHandler(object sender, ServiceSchedularErrorEventArgs e);
        /// <summary>
        /// Gets triggered whenever there is an error that might get supressed if retry on error is enabled
        /// </summary>
        public event ErrorEventHandler Error;
        protected virtual Task OnErrorAsync(ServiceSchedularErrorEventArgs e)
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
        // ~ServiceScheduler()
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
