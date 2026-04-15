using System;
using System.Collections.Generic;
using System.Text;

namespace Com.H.Threading.Scheduler
{
    public class HTaskExecutionErrorEventArgs
    {
        public object Sender { get; init; }
        public Exception Exception { get; init; }
        public HTaskEventArgs EventArgs { get; init; }
        public HTaskExecutionErrorEventArgs(
            object sender, Exception exception, HTaskEventArgs eventArgs)
            => (this.Sender, this.Exception, this.EventArgs)
            = (sender, exception, eventArgs);
    }
}
