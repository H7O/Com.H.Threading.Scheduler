using System;
using System.Collections.Generic;
using System.Text;

namespace Com.H.Threading.Scheduler
{
    public class TimeLog
    {
        public DateTime? LastExecuted { get; set; }
        public DateTime? LastError { get; set; }
        public int ErrorCount { get; set; }
    }
    public interface IServiceTimeLogger
    {
        TimeLog this[string key] { get; }
        void Save();
    }
}
