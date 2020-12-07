using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

namespace Com.H.Threading.Scheduler
{
    public interface IServiceItemAttr
    {
        IDictionary<string, string> Items { get; }
        string this[string attr] { get; }
    }
}
