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
        /// Service item full name (includes parent names seerated by forward slash '/')
        /// </summary>
        string FullName { get; }

        /// <summary>
        /// Service item value
        /// </summary>
        string GetValue();
        /// <summary>
        /// Service item DataModel (used when model_type attribute is defined)
        /// </summary>
        /// <returns></returns>
        IEnumerable<T> GetModel<T>();
        /// <summary>
        /// Parent service item
        /// </summary>
        IServiceItem Parent { get; }
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
        IServiceItem this[string name] { get; }
        /// <summary>
        /// Run schedule configuration
        /// </summary>
        IServiceControlProperties Schedule { get; }

        /// <summary>
        /// DataModel to fill placeholders IServiceItem values
        /// </summary>
        DefaultVars Vars { get; }

        ContentSettings ContentSettings { get; }
        /// <summary>
        /// The raw un-processed value to be used in values post-processors.
        /// </summary>
        string RawValue { get; }
        /// <summary>
        /// Refernce to all existing services loaded from configuration
        /// </summary>
        IServiceCollection AllServices { get; }

    }
}
