using System;
using System.Collections.Generic;
using System.Text;

namespace Com.H.Threading.Scheduler
{
    public class ServiceSchedularErrorEventArgs
    {
        public object Sender { get; init; }
        public Exception Exception { get; init; }
        public ServiceSchedulerEventArgs EventArgs { get; init; }
        public ServiceSchedularErrorEventArgs(
            object sender, Exception exception, ServiceSchedulerEventArgs eventArgs)
            => (this.Sender, this.Exception, this.EventArgs)
            = (sender, exception, eventArgs);
    }
}
