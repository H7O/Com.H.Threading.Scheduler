using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Com.H.Threading.Scheduler
{
    public class XmlFileServiceCollection : ICollection<IServiceItem>
    {
        #region properties
        private ICollection<IServiceItem> Services { get; set; }
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
                            .Elements().Select(x => new XmlServiceItem(x)).ToArray();

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
