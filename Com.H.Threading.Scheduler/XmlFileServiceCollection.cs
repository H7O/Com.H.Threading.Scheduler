using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Threading;

namespace Com.H.Threading.Scheduler
{

    public class XmlFileServiceCollection : IServiceCollection
    {
        #region properties
        
        private ICollection<IServiceItem> Services { get; set; }

        /// <summary>
        /// Allows for adding custom value post processing logic when retrieving configuration values.
        /// Each tag in configuration file could have the reserved attribute 'content_type' defined with 
        /// a value that if matches a key in this dictionary would instruct the scheduler to 
        /// lookup the dictionary key corresponding Func, execute it, and return the resulting value
        /// instead of the actual tag value.
        /// By default, the engine adds UriValueProcessor that correspond to 'content_type' value of 'uri'
        /// </summary>
        public ConcurrentDictionary<string, ValueProcessor> ValueProcessors { get; private set; }
        private DateTime? ServicesLastModified { get; set; }
        private string FilePath { get; set; }
        private object ServicesLock { get; set; } = new object();

        int ICollection<IServiceItem>.Count => throw new NotImplementedException();

        bool ICollection<IServiceItem>.IsReadOnly => true;
        #endregion

        #region constructor
        public XmlFileServiceCollection(string serviceCollectionFilePath)
        {
            if (string.IsNullOrWhiteSpace(serviceCollectionFilePath))
                throw new ArgumentNullException(nameof(serviceCollectionFilePath));
            this.FilePath = serviceCollectionFilePath;
            if (!File.Exists(this.FilePath))
                throw new FileNotFoundException(this.FilePath);
            this.ValueProcessors = new ConcurrentDictionary<string, ValueProcessor>();
            this.ValueProcessors.TryAdd("uri", DefaultValueProcessors.UriProcessor);
            this.ValueProcessors.TryAdd("csv", DefaultValueProcessors.CsvDataModelProcessor);
            this.ValueProcessors.TryAdd("psv", DefaultValueProcessors.PsvDataModelProcessor);
            
        }
        #endregion
        #region load from disk
        private ICollection<IServiceItem> GetServices()
        {
            if (!File.Exists(this.FilePath))
                throw new FileNotFoundException(this.FilePath);
            lock (this.ServicesLock)
            {
                if (this.Services != null
                        && this.ServicesLastModified != null
                        && File.GetLastWriteTime(this.FilePath) <= this.ServicesLastModified)
                    return this.Services;

                this.Services = XElement.Load(this.FilePath)
                            .Elements().Select(x => new XmlServiceItem(this, x)).ToArray();

                this.ServicesLastModified = File.GetLastWriteTime(this.FilePath);
                return this.Services;
            }
        }
        #endregion

        #region IEnumerator
        public IEnumerator<IServiceItem> GetEnumerator()
        {
            return this.GetServices()?.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)this.GetServices())?.GetEnumerator();
        }

        void ICollection<IServiceItem>.Add(IServiceItem item)
            => throw new NotImplementedException();
        void ICollection<IServiceItem>.Clear()
            => throw new NotImplementedException();

        bool ICollection<IServiceItem>.Contains(IServiceItem item)
            => this.GetServices()?.Contains(item) ?? false;


        void ICollection<IServiceItem>.CopyTo(IServiceItem[] array, int arrayIndex)
        => this.GetServices()?.CopyTo(array, arrayIndex);


        bool ICollection<IServiceItem>.Remove(IServiceItem item)
            => throw new NotImplementedException();
        #endregion
    }
}
