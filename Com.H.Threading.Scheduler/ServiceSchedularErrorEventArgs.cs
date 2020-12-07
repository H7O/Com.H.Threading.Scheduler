using System;
using System.Collections.Generic;
using System.Text;

namespace Com.H.Threading.Scheduler
{
    public class ServiceSchedularErrorEventArgs
    {
        public object Sender { get; private set; }
        public Exception Exception { get; private set; }
        public ServiceSchedulerEventArgs EventArgs { get; private set; }
        public ServiceSchedularErrorEventArgs(
            object sender, Exception exception, ServiceSchedulerEventArgs eventArgs)
            => (this.Sender, this.Exception, this.EventArgs)
            = (sender, exception, eventArgs);
    }
}
