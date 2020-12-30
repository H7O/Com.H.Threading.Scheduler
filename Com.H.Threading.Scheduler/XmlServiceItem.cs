using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;
using System.Linq;
using System.Globalization;
using System.Threading;
using Com.H.Security.Cryptography;
using System.Collections.Concurrent;
using Com.H.Data;
using Com.H.Linq;

namespace Com.H.Threading.Scheduler
{
    public class XmlServiceItem : IServiceItem, IDisposable
    {
        #region properties
        
        public string UniqueKey { get; init; }
        private XElement Element { get; init; }
        public string RawValue { get; init; }
        public ContentSettings ContentSettings { get; init; }
        private CancellationTokenSource Cts { get; set; }
        private bool disposedValue;
        public DefaultVars Vars { get; init; }
        public string Name { get; init; }
        public string FullName { get; init; }
        public IServiceItem Parent { get; init; }
        public IServiceItemAttr Attributes { get; init; }
        public ICollection<IServiceItem> Children { get; private set; }
        public IServiceControlProperties Schedule { get; init; }
        private CachedRun Cache { get; set; }
        public IServiceCollection AllServices { get; init; }

        #endregion

        #region indexer
        public IServiceItem this[string name]
        {
            get
            {
                if (this.Children == null
                    || string.IsNullOrWhiteSpace(name)) return null;

                return this.Children.FirstOrDefault(x =>
                    x.Name != null
                    && x.Name.ToUpper(CultureInfo.InvariantCulture)
                    .Equals(name.ToUpper(CultureInfo.InvariantCulture)));
            }
        }
        #endregion

        #region constructor
        public XmlServiceItem(IServiceCollection services, XElement element, IServiceItem parent = null, CancellationToken? token = null)
        {
            this.AllServices = services?? throw new ArgumentNullException(nameof(services));
            this.Element = element ?? throw new ArgumentNullException(nameof(element));
            this.RawValue = this.Element.Value;
            this.Parent = parent;
            if (token != null) this.Cts = CancellationTokenSource
                    .CreateLinkedTokenSource((CancellationToken)token);
            this.Attributes = new XmlServiceItemAttr(this.Element);
            this.Name = this.Element.Name.LocalName;
            if (this.Parent != null)
                this.FullName = $"{(this.Parent?.Parent == null ? "" : this.Parent?.FullName)}/{this.Name}";
            else this.FullName = "/";
            this.ContentSettings = this.Attributes.GetContentSettings();
            this.UniqueKey = this.Element.ToString().ToSha256InBase64String();
            XmlServiceItem schedulerItem = null;
            this.Schedule = this.Element.Element("sys") == null ? parent?.Schedule
                : new ServiceControlProperties(
                    schedulerItem = new XmlServiceItem(this.AllServices, 
                    this.Element.Element("sys"), this, this.Cts?.Token));

            this.Vars = new DefaultVars()
            {
                Now = this.Schedule?.Now 
                    ?? this.Parent?.Vars?.Now
                    ?? DateTime.Now,
                Tomorrow = this.Schedule?.Tomorrow 
                    ?? this.Parent?.Vars?.Tomorrow 
                    ?? DateTime.Today.AddDays(1),
            };


            this.Children = this.Element.Elements()?
                .Where(x=>!x.Name.LocalName.Equals("sys"))?
                .Select(x =>
            new XmlServiceItem(this.AllServices, x, this, this.Cts?.Token))?
            .ToArray() 
                ?? Array.Empty<XmlServiceItem>();
            if (schedulerItem != null)
                this.Children = this.Children.Union(new XmlServiceItem[] { schedulerItem }).ToArray();

                
        }
        #endregion


        #region get value

        private ValueProcessorItem GetValueProcessorItem()
        =>
           Enumerable.Aggregate(
                this.AllServices?.ValueProcessors?
               .OrdinalFilter(
                   this.ContentSettings?.Type?
                   .Split(new string[] {",", "=>", "->", ">"}, 
                       StringSplitOptions.RemoveEmptyEntries 
                       | StringSplitOptions.TrimEntries))
                ?? Array.Empty<ValueProcessor>()
                , ValueProcessorItem.Parse(this)
                .DateProcessor().CustomVarsProcessor()
                , (i, n) => n(i, this.Cts?.Token)
                .DateProcessor().CustomVarsProcessor()
                );
        

        public string GetValue()
        {
            try
            {
                string GetContent()
                {
                    return this.GetValueProcessorItem()?.Value;
                }
                if (this.ContentSettings.CachePeriod == ContentCachePeriod.None)
                    return GetContent();
                if (this.Cache == null)
                    this.Cache = new CachedRun();
                return this.ContentSettings.CachePeriod ==
                    ContentCachePeriod.Miliseconds ?
                    this.Cache.Run(GetContent,
                        TimeSpan.FromMilliseconds((int)this.ContentSettings.CacheInMilisec),
                            this.FullName)
                    : this.Cache.Run(GetContent,
                        DateTime.Today.AddDays(1), this.FullName);
            }
            catch
            {
                throw;
            }

        }

        public IEnumerable<T> GetModel<T>()
        {
            try
            {
                IEnumerable<T> GetContent()
                {
                    return (IEnumerable<T>) this.GetValueProcessorItem()?.Data;
                }
                if (this.ContentSettings.CachePeriod == ContentCachePeriod.None)
                    return GetContent();
                if (this.Cache == null)
                    this.Cache = new CachedRun();
                return this.ContentSettings.CachePeriod ==
                    ContentCachePeriod.Miliseconds ?
                    this.Cache.Run(GetContent,
                        TimeSpan.FromMilliseconds((int)this.ContentSettings.CacheInMilisec),
                            this.FullName)
                    : this.Cache.Run(GetContent,
                        DateTime.Today.AddDays(1), this.FullName);
            }
            catch
            {
                throw;
            }

        }
        #endregion

        #region
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    try
                    {
                        this.Cache?.Dispose();
                    }
                    catch { }
                }

                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~XmlServiceItem()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion



    }
}
