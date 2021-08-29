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
    public class HTaskSchedulerEventArgs : EventArgs
    {
        #region properties
        public CancellationToken CancellationToken { get; init; }
        public object Sender { get; init; }
        public IHTaskItem Task { get; init; }
        public IEnumerable<IHTaskItem> AllTasks { get; init; }
        public DateTime? LastExecuted { get; init; }
        public ReplayPlan Loop { get; init; }
        public IDictionary<string, object> ExtraVars { get; init; }
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
        public HTaskSchedulerEventArgs(
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
        public IHTaskItem GetItem(string index)
        => index?.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                                    .Aggregate((IHTaskItem)null, (i, n) =>
                                   i?.Children?.FirstOrDefault(x => x.Name.EqualsIgnoreCase(n)) ??
                                   this.Task[n]);

        public IEnumerable<IHTaskItem> GetItems(string index)
        => index?.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                                    .Aggregate((IEnumerable<IHTaskItem>)null, (i, n) =>
                                   i?.SelectMany(x => x.Children)?.Where(c => c.Name.EqualsIgnoreCase(n)) ??
                                   this.Task?.Children?.Where(x => x.Name.EqualsIgnoreCase(n)));


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

        public IEnumerable<dynamic> GetModels(string index)
        => this.GetItems(index).Select(x =>
        {
            try
            {
                return x?.GetModel();
            }
            catch { }
            return null;
        });

        #endregion
    }



}
