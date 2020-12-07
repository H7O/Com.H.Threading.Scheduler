using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Com.H.Text;

namespace Com.H.Threading.Scheduler
{
    public class ReplayPlan
    {
        public enum ReplayOption
        {
            None,
            RepeatLast,
            RepeatFromStart,
            SkipRemaining
        }
        public ReplayOption Replay { get; set; }
        /// <summary>
        /// in miliseconds
        /// </summary>
        public int? PauseBetweenPlays { get; set; }
        public ReplayPlan() => this.Replay = ReplayOption.None;
    }
    public class ServiceSchedulerEventArgs : EventArgs
    {
        #region properties
        public CancellationToken CancellationToken { get; private set; }
        public object Sender { get; private set; }
        public IServiceItem Service { get; private set; }
        public IEnumerable<IServiceItem> AllServices { get; private set; }
        public DateTime? LastExecuted { get; private set; }
        public DateTime Now { get; private set; }
        public DateTime Today { get; private set; }
        public DateTime Tomorrow { get; private set; }
        public ReplayPlan Loop { get; private set; }
        public IDictionary<string, object> ExtraVars { get; private set; }
        #endregion

        #region indexer
        /// <summary>
        /// Takes settings path formatted with '/' seperator between parent child elements,
        /// and returns the value for the first applicable settings found in the path
        /// </summary>
        /// <param name="index">settings path formatted with '/' seperator for parent child hierarchy</param>
        /// <returns>value of the settings available at the defined index. If multiple values available for the index hierarchy
        /// the first value will be returned.
        /// </returns>
        public string this[string index]
        { 
            get
            {
                try
                {
                    return this.GetItem(index)?.GetValue(); 
                }
                catch { }
                return null;
            }
                        
        }
        
        

        #endregion

        #region constructor
        public ServiceSchedulerEventArgs(
            ServiceScheduler scheduler,
            IServiceItem service,
            CancellationToken cancellationToken,
            IDictionary<string, object> extraVars = null
            )
        {
            this.Sender = scheduler;
            this.Service = service;
            this.CancellationToken = cancellationToken;
            this.ExtraVars = extraVars;
            this.Loop = new ReplayPlan();

            #region time variables
            if (service?["now_datetime"]?.GetValue() != null
                &&
                DateTime.TryParse(service["now_datetime"].GetValue(), out _)
                )
                this.Now = DateTime.Parse(service["now_datetime"].GetValue(), CultureInfo.InvariantCulture);
            else this.Now = DateTime.Now;

            this.Today = this.Now.Date;
            this.Tomorrow = this.Today.AddDays(1);
            #endregion

        }
        #endregion

        #region getters
        public IServiceItem GetItem(string index)
        => index?.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                                    .Aggregate((IServiceItem)null, (i, n) =>
                                   i?.Children?.FirstOrDefault(x => x.Name.EqualsIgnoreCase(n)) ??
                                   this.Service[n]);
        
        public IEnumerable<IServiceItem> GetItems(string index)
        => index?.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                                    .Aggregate((IEnumerable<IServiceItem>)null, (i, n) =>
                                   i?.SelectMany(x => x.Children)?.Where(c => c.Name.EqualsIgnoreCase(n)) ??
                                   this.Service?.Children?.Where(x=>x.Name.EqualsIgnoreCase(n)));
            
        
        public IEnumerable<string> GetValues(string index)
        => this.GetItems(index).Select(x =>
        {
            try
            {
                return x?.GetValue();
            }
            catch { }
            return null;
        });

        #endregion
    }



}
