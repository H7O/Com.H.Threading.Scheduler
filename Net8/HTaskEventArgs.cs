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
        public ReplayOption? Replay { get; set; }
        /// <summary>
        /// in miliseconds
        /// </summary>
        public int? PauseBetweenPlays { get; set; }
        public ReplayPlan() => this.Replay = ReplayOption.None;
    }
    public class HTaskEventArgs : EventArgs
    {
        #region properties
        public CancellationToken CancellationToken { get; init; }
        public object Sender { get; init; }
        public IHTaskItem Task { get; init; }
        public IEnumerable<IHTaskItem>? AllTasks { get; init; }
        public DateTime? LastExecuted { get; init; }
        public ReplayPlan? Loop { get; init; }
        public IDictionary<string, object>? ExtraVars { get; init; }
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
        public HTaskEventArgs(
            HTaskScheduler scheduler,
            IHTaskItem task,
            CancellationToken cancellationToken
            )
        {
            this.Sender = scheduler;
            this.Task = task;
            this.CancellationToken = cancellationToken;
            this.Loop = new ReplayPlan();
        }
        #endregion

        #region getters
        public IHTaskItem? GetItem(string index)
            => this.Task?.GetItem(index);

        public IEnumerable<IHTaskItem?>? GetItems(string index)
            => this.Task?.GetItems(index);

        public IEnumerable<string?>? GetValues(string index)
            => this.Task?.GetValues(index);

        public IEnumerable<dynamic?>? GetModels(string index)
            => this.Task?.GetModels(index);

        public dynamic? GetModel(string index)
            => this.Task?.GetItem(index)?.GetModel();

        #endregion
    }



}
