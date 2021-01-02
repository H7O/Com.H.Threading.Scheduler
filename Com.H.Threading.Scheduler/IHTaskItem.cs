using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

namespace Com.H.Threading.Scheduler
{
    public interface IHTaskItem
    {
        /// <summary>
        /// Auto generated unique key to be used by scheduler to distinguish tasks from each other.
        /// </summary>
        string UniqueKey { get; }
        /// <summary>
        /// Task item name
        /// </summary>
        string Name { get; }
        /// <summary>
        /// Task item full name (includes parent names seerated by forward slash '/')
        /// </summary>
        string FullName { get; }

        /// <summary>
        /// Task item value
        /// </summary>
        string GetValue();
        /// <summary>
        /// Task item DataModel (used when content_type attribute is defined)
        /// </summary>
        /// <returns></returns>
        T GetModel<T>();
        /// <summary>
        /// Task item DataModel (used when content_type attribute is defined)
        /// if no content_type is defined, this method returns the output of GetValue()
        /// </summary>
        /// <returns></returns>
        dynamic GetModel() => GetModel<dynamic>()??GetValue();
        /// <summary>
        /// Parent task item
        /// </summary>
        IHTaskItem Parent { get; }
        /// <summary>
        /// Task item attributes
        /// </summary>
        IHTaskItemAttr Attributes { get; }
        /// <summary>
        /// Child items
        /// </summary>
        ICollection<IHTaskItem> Children { get; }
        /// <summary>
        /// Returns a child item by name if available, otherwise returns null
        /// </summary>
        /// <param name="">Child item name</param>
        /// <returns></returns>
        IHTaskItem this[string name] { get; }
        /// <summary>
        /// Run schedule configuration
        /// </summary>
        IHTaskControlProperties Schedule { get; }

        /// <summary>
        /// DataModel to fill placeholders IHTaskItem values
        /// </summary>
        DefaultVars Vars { get; }

        ContentSettings ContentSettings { get; }
        /// <summary>
        /// The raw un-processed value to be used in values post-processors.
        /// </summary>
        string RawValue { get; }
        /// <summary>
        /// Refernce to all existing tasks loaded from configuration
        /// </summary>
        IHTaskCollection AllTasks { get; }

    }
}
