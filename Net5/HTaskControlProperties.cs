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
    public class HTaskControlProperties : IHTaskControlProperties, IDisposable
    {

        #region properties
        private IHTaskItem TaskItem { get; set; }
        private CachedRunDeprecated Cache { get; set; }

       
        public bool Enabled
        {
            get
            {
                if (this.TaskItem["enabled"] == null) return true;
                return this.TaskItem["enabled"].GetValue()?.ContainsIgnoreCase("true")??false;
            }
        }

        public dynamic Repeat
        {
            get => this.TaskItem["repeat"]?.GetModel<dynamic>();
        }

        public int? RepeatDelayInterval
        {
            get
            {
                try
                {
                    return 
                        (this.TaskItem["repeat"]?.Attributes["delay_interval"]??
                        this.TaskItem["repeat"]?.Attributes["delay-interval"]) == null ? null
                        : int.Parse(
                            (this.TaskItem["repeat"]?.Attributes["delay_interval"] ??
                                this.TaskItem["repeat"]?.Attributes["delay-interval"])
                            );
                }
                catch { }
                return null;
            }
        }
        public DateTime? NotBefore
        {
            get
            {
                var item = this.TaskItem["not_before"]?.GetValue();
                if (item == null) return null;
                if (DateTime.TryParse(item, out DateTime notBefore)) return notBefore;
                return null;
            }
        }
        public DateTime? NotAfter
        {
            get
            {
                var item = this.TaskItem["not_after"]?.GetValue();
                if (item == null) return null;
                if (DateTime.TryParse(item, out DateTime notAfter)) return notAfter;
                return null;
            }
        }

        public IEnumerable<DateTime> Dates
        {
            get
            {
                var item = this.TaskItem["dates"]?.GetValue();
                if (item == null) return null;
                if (this.Time != null) return item.ExtractDates(
                    new string[] { "|", "\r", "\n" })
                          .Select(x =>
                          new DateTime(Math.Max(x.Ticks,
                          x.Date.AddTicks(((TimeSpan)this.Time).Ticks).Ticks)));
                else return item.ExtractDates(new string[] { "|", "\r", "\n" });
            }
        }

        public IEnumerable<int> DaysOfYear => this.TaskItem["doy"]?.GetValue()?.ExtractRangeInts();

        public IEnumerable<int> DaysOfMonth => this.TaskItem["dom"]?.GetValue()?.ExtractRangeInts();
        public IEnumerable<string> DaysOfWeek => this.TaskItem["dow"]?.GetValue()?
            .Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim().ToUpper(CultureInfo.InvariantCulture));
        public bool? LastDayOfMonth
        {
            get
            {
                var item = this.TaskItem["eom"]?.GetValue();
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
                var item = this.TaskItem["bom"]?.GetValue();
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
                var item = this.TaskItem["time"]?.GetValue();
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
                var item = this.TaskItem["until_time"]?.GetValue();
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
                var item = this.TaskItem["interval"]?.GetValue();
                if (item == null) return null;
                if (int.TryParse(item, out int value)) return value;
                return null;
            }
        }

        public bool IgnoreLogOnRestart
            =>this.TaskItem["ignore_log_on_restart"]?.GetValue()?.ContainsIgnoreCase("true") ?? false;



        public int? RetryInMilisecAfterError
        {
            get
            {
                var item = this.TaskItem["sleep_on_error"]?.GetValue();
                if (item == null) return null;
                if (int.TryParse(item, out int value)) return value;
                return null;
            }
        }
        public int? RetryAttemptsAfterError
        {
            get
            {
                var item = this.TaskItem["retry_attempts_on_error"]?.GetValue();
                if (item == null) return null;
                if (int.TryParse(item, out int value)) return value;
                return null;
            }
        }

        public DateTime Now
        {
            get
            {
                var item = this.TaskItem["now"]?.GetValue();
                if (item == null) return DateTime.Now;
                if (DateTime.TryParse(item, out DateTime dateTime)) return dateTime;
                return DateTime.Now;
            }
        }

        public DateTime Today { get => this.Now.Date; }
        public DateTime Tomorrow { get => this.Today.AddDays(1); }


        #endregion

        #region constructor
        public HTaskControlProperties(IHTaskItem item)
        => (this.TaskItem, this.Cache) = (item, new CachedRunDeprecated());

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
        // ~TaskControlProperties()
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
