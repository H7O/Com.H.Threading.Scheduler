using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using Com.H.Threading.Scheduler.VP;
using Com.H.Events;

namespace Com.H.Threading.Scheduler
{
    public delegate ValueProcessorItem ValueProcessor(ValueProcessorItem? valueItem,
    CancellationToken? token = null);

    public interface IHTaskCollection : ICollection<IHTaskItem?>
    {
        ConcurrentDictionary<string, ValueProcessor?>? ValueProcessors { get; }
        event HErrorEventHandler? Error;
    }
    
}
