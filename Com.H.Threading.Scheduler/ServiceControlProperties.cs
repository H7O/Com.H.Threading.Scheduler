using Com.H.Text;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Linq;

namespace Com.H.Threading.Scheduler
{
    public class ServiceControlProperties : IServiceControlProperties, IDisposable
    {
        

        #region properties
        private IServiceItem ServiceItem { get; set; }
        private CachedRun Cache { get; set; }

       
        public bool Enabled
        {
            get
            {
                if (this.ServiceItem["enabled"] == null) return true;
                return this.ServiceItem["enabled"].GetValue()?.ContainsIgnoreCase("true")??false;
            }
        }

        public IEnumerable<dynamic> Repeat
        {
            get => this.ServiceItem["repeat"]?.GetModel<dynamic>();
            //get =>
            //    this.ServiceItem["repeat"] == null? null
                    //: (IEnumerable<dynamic>) Enumerable.Aggregate(
                    //                    this.ServiceItem.AllServices?.ValueProcessors
                    //                    ?? new ConcurrentDictionary<string, Func<ValueProcessorItem, CancellationToken?, ValueProcessorItem>>()
                    //                    , ValueProcessorItem.Parse(this.ServiceItem["repeat"]).DateProcessor()
                    //                    , (i, n) => n.Value(i, this.CToken).DateProcessor()
                    //                    ).CsvDataModelProcessor().Item.Vars.Custom;
            
        }
        public DateTime? NotBefore
        {
            get
            {
                var item = this.ServiceItem["not_before"]?.GetValue();
                if (item == null) return null;
                if (DateTime.TryParse(item, out DateTime notBefore)) return notBefore;
                return null;
            }
        }
        public DateTime? NotAfter
        {
            get
            {
                var item = this.ServiceItem["not_after"]?.GetValue();
                if (item == null) return null;
                if (DateTime.TryParse(item, out DateTime notAfter)) return notAfter;
                return null;
            }
        }

        public DateTime? Date
        {
            get
            {
                var item = this.ServiceItem["date"]?.GetValue();
                if (item == null) return null;
                if (DateTime.TryParse(item, out DateTime exactDateTime)) return exactDateTime.Date;
                return null;
            }
        }

        public IEnumerable<int> DaysOfYear => this.ServiceItem["doy"]?.GetValue()?.ExtractRangeInts();

        public IEnumerable<int> DaysOfMonth => this.ServiceItem["dom"]?.GetValue()?.ExtractRangeInts();
        public IEnumerable<string> DaysOfWeek => this.ServiceItem["dow"]?.GetValue()?
            .Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim().ToUpper(CultureInfo.InvariantCulture));
        public bool? LastDayOfMonth
        {
            get
            {
                var item = this.ServiceItem["eom"]?.GetValue();
                if (item == null) return null;
                if (item.ContainsIgnoreCase("true")) return true;
                if (item.ContainsIgnoreCase("false")) return false;
                return null;
            }
        }

        public bool? FirstDayOfMonth
        {
            get
            {
                var item = this.ServiceItem["bom"]?.GetValue();
                if (item == null) return null;
                if (item.ContainsIgnoreCase("true")) return true;
                if (item.ContainsIgnoreCase("false")) return false;
                return null;
            }
        }

        public TimeSpan? Time
        {
            get
            {
                var item = this.ServiceItem["time"]?.GetValue();
                if (item == null) return null;
                if (TimeSpan.TryParse(item, out TimeSpan value))
                    return value;
                return null;
            }
        }

        public TimeSpan? UntilTime
        {
            get
            {
                var item = this.ServiceItem["until_time"]?.GetValue();
                if (item == null) return null;
                if (TimeSpan.TryParse(item, out TimeSpan value))
                    return value;
                return null;
            }
        }
        public int? Interval
        {
            get
            {
                var item = this.ServiceItem["interval"]?.GetValue();
                if (item == null) return null;
                if (int.TryParse(item, out int value)) return value;
                return null;
            }
        }

        public bool IgnoreLogOnRestart
            =>this.ServiceItem["ignore_log_on_restart"]?.GetValue()?.ContainsIgnoreCase("true") ?? false;



        public int? RetryInMilisecAfterError
        {
            get
            {
                var item = this.ServiceItem["sleep_on_error"]?.GetValue();
                if (item == null) return null;
                if (int.TryParse(item, out int value)) return value;
                return null;
            }
        }
        public int? RetryAttemptsAfterError
        {
            get
            {
                var item = this.ServiceItem["retry_attempts_on_error"]?.GetValue();
                if (item == null) return null;
                if (int.TryParse(item, out int value)) return value;
                return null;
            }
        }

        public DateTime Now
        {
            get
            {
                var item = this.ServiceItem["now"]?.GetValue();
                if (item == null) return DateTime.Now;
                if (DateTime.TryParse(item, out DateTime dateTime)) return dateTime;
                return DateTime.Now;
            }
        }

        public DateTime Today { get => this.Now.Date; }
        public DateTime Tomorrow { get => this.Today.AddDays(1); }


        #endregion

        #region constructor
        public ServiceControlProperties(IServiceItem item)
        => (this.ServiceItem, this.Cache) = (item, new CachedRun());

        #endregion

        #region dispose
        private bool disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    try
                    {
                        if (this.Cache != null) this.Cache.Dispose();
                    }
                    catch { }
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~ServiceControlProperties()
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
