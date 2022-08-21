using System;
using System.Collections.Generic;
using System.Text;

namespace Com.H.Threading.Scheduler
{
    public class HTaskSchedulerErrorEventArgs
    {
        public object Sender { get; init; }
        public Exception Exception { get; init; }
        public HTaskSchedulerEventArgs EventArgs { get; init; }
        public HTaskSchedulerErrorEventArgs(
            object sender, Exception exception, HTaskSchedulerEventArgs eventArgs)
            => (this.Sender, this.Exception, this.EventArgs)
            = (sender, exception, eventArgs);
    }
}
