using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Com.H.Text;

namespace Com.H.Threading.Scheduler
{
    public class HTaskRunSummaryEventArgs : EventArgs
    {
        #region properties
        public CancellationToken CancellationToken { get; init; }
        public object Sender { get; init; }
        public IHTaskItem MainTask { get; init; }
        public IEnumerable<IHTaskItem>? AllMainTasks { get; init; }
        public DateTime? LastExecuted { get; init; }
        public ReplayPlan? Loop { get; init; }
        public IDictionary<string, object>? ExtraVars { get; init; }
		public List<IHTaskItem>? RepeatedTasks { get; set; }
		public List<Exception>? RepeatExceptions { get; set; }
		public int RepeatFailureCount => this.RepeatExceptions?.Count ?? 0;
		public int RepeatSuccessCount => this.RepeatedTasks?.Count - this.RepeatFailureCount ?? 0; 

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
        public string? this[string index]
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
        public HTaskRunSummaryEventArgs(
            HTaskScheduler scheduler,
            IHTaskItem task,
            CancellationToken cancellationToken
            )
        {
            this.Sender = scheduler;
            this.MainTask = task;
            this.CancellationToken = cancellationToken;
            this.Loop = new ReplayPlan();
        }
        #endregion

        #region getters
        public IHTaskItem? GetItem(string index)
            => this.MainTask?.GetItem(index);

        public IEnumerable<IHTaskItem?>? GetItems(string index)
            => this.MainTask?.GetItems(index);

        public IEnumerable<string?>? GetValues(string index)
            => this.MainTask?.GetValues(index);

        public IEnumerable<dynamic?>? GetModels(string index)
            => this.MainTask?.GetModels(index);

        public dynamic? GetModel(string index)
            => this.MainTask?.GetItem(index)?.GetModel();

        #endregion
    }



}
