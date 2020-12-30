using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;

namespace Com.H.Threading.Scheduler
{
    public delegate ValueProcessorItem ValueProcessor(ValueProcessorItem valueItem,
    CancellationToken? token = null);

    public interface IServiceCollection : ICollection<IServiceItem>
    {
        ConcurrentDictionary<string, ValueProcessor> ValueProcessors { get; }
    }
}
