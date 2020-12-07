using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

namespace Com.H.Threading.Scheduler
{
    public interface IServiceItem
    {
        /// <summary>
        /// Auto generated unique key to be used by scheduler to distinguish services from each other.
        /// </summary>
        string UniqueKey { get; }
        /// <summary>
        /// Service item name
        /// </summary>
        string Name { get; }
        /// <summary>
        /// Service item value
        /// </summary>
        string GetValue();
        /// <summary>
        /// Parent service item
        /// </summary>
        IServiceItem Parent { get; }
        ///// <summary>
        ///// Last successful execution
        ///// </summary>
        //DateTime? LastExecuted { get; }
        ///// <summary>
        ///// Last recorded error to be used when retry on error mechanism is enabled
        ///// </summary>
        //DateTime? LastError { get; set; }
        ///// <summary>
        ///// Error retries to be used when retry on error mechanism is enabled
        ///// </summary>
        //int ErrorRetries { get; set; }
        /// <summary>
        /// Service item attributes
        /// </summary>
        IServiceItemAttr Attributes { get; }
        /// <summary>
        /// Child items
        /// </summary>
        ICollection<IServiceItem> Children { get; }
        /// <summary>
        /// Returns a child item by name if available, otherwise returns null
        /// </summary>
        /// <param name="">Child item name</param>
        /// <returns></returns>
        IServiceItem this[string name] {get;}
        /// <summary>
        /// Run schedule configuration
        /// </summary>
        IServiceControlProperties Schedule { get; }

    }
}
