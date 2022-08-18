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
using Com.H.Threading.Scheduler.VP;
using Com.H.Text;

namespace Com.H.Threading.Scheduler
{
    public class XmlHTaskItem : IHTaskItem, IDisposable
    {
        #region properties
        public string XmlFileFullPath { get; set; }
        public string UniqueKey { get; init; }
        private XElement Element { get; init; }
        public string RawValue { get; init; }
        public ContentSettings ContentSettings { get; init; }
        private CancellationTokenSource Cts { get; set; }
        private bool disposedValue;
        private DefaultVars vars = null;
        public DefaultVars Vars
        {
            get
            {
                if (this.Parent?.Vars is not null) return this.Parent.Vars;
                if (vars is not null) return vars;
                this.vars = new DefaultVars()
                {
                    Now = this.Schedule?.Now
                        // ?? this.Parent?.Vars?.Now
                        ?? DateTime.Now,
                    Tomorrow = this.Schedule?.Tomorrow
                        // ?? this.Parent?.Vars?.Tomorrow
                        ?? DateTime.Today.AddDays(1),
                };
                return vars;
            }
            set
            {
                // Console.WriteLine($"before resetting is value null = ${value is null}");
                this.vars = value;
                // Console.WriteLine($"after resetting is value null = ${value is null}");
            }
        }
        public string Name { get; init; }
        public string FullName { get; init; }
        public IHTaskItem Parent { get; init; }
        public IHTaskItemAttr Attributes { get; init; }
        public ICollection<IHTaskItem> Children { get; private set; }
        public IHTaskControlProperties Schedule { get; init; }
        private CachedRunDeprecated Cache { get; set; }
        public IHTaskCollection AllTasks { get; init; }

        #endregion

        #region indexer
        public IHTaskItem this[string name] { get => GetItem(name); }
        #endregion

        private IHTaskItem GetDirectChild(string name)
        {
            if (this.Children == null
                || string.IsNullOrWhiteSpace(name)) return null;

            return this.Children.FirstOrDefault(x =>
                x.Name != null
                && x.Name.ToUpper(CultureInfo.InvariantCulture)
                .Equals(name.ToUpper(CultureInfo.InvariantCulture)));
        }

        #region constructor
        public XmlHTaskItem(IHTaskCollection hTasks, XElement element, IHTaskItem parent = null, CancellationToken? token = null)
        {
            this.AllTasks = hTasks ?? throw new ArgumentNullException(nameof(hTasks));
            this.Element = element ?? throw new ArgumentNullException(nameof(element));
            this.RawValue = this.Element.Value;
            this.Parent = parent;
            if (token != null) this.Cts = CancellationTokenSource
                    .CreateLinkedTokenSource((CancellationToken)token);
            this.Attributes = new XmlHTaskItemAttr(this.Element);
            this.Name = this.Element.Name.LocalName;
            if (this.Parent != null)
                this.FullName = $"{(this.Parent?.Parent == null ? "" : this.Parent?.FullName)}/{this.Name}";
            else this.FullName = "/";
            this.ContentSettings = this.Attributes.GetContentSettings();
            this.UniqueKey = this.Element.ToString().ToSha256InBase64String();
            XmlHTaskItem schedulerItem = null;
            this.Schedule = this.Element.Element("sys") == null ? parent?.Schedule
                : new HTaskControlProperties(
                    schedulerItem = new XmlHTaskItem(this.AllTasks,
                    this.Element.Element("sys"), this, this.Cts?.Token));

            // needs to be set in property only

            //this.Vars = new DefaultVars()
            //{
            //    Now = this.Schedule?.Now
            //        ?? this.Parent?.Vars?.Now
            //        ?? DateTime.Now,
            //    Tomorrow = this.Schedule?.Tomorrow
            //        ?? this.Parent?.Vars?.Tomorrow
            //        ?? DateTime.Today.AddDays(1),
            //};


            this.Children = this.Element.Elements()?
                .Where(x => !x.Name.LocalName.Equals("sys"))?
                .Select(x =>
            new XmlHTaskItem(this.AllTasks, x, this, this.Cts?.Token))?
            .ToArray()
                ?? Array.Empty<XmlHTaskItem>();
            if (schedulerItem != null)
                this.Children = this.Children.Union(new XmlHTaskItem[] { schedulerItem }).ToArray();


        }
        #endregion


        #region get

        public IHTaskItem GetItem(string index)
            => index?.Split(new char[] { '/', ':' }, StringSplitOptions.RemoveEmptyEntries)
                            .Aggregate((IHTaskItem)null, (i, n) =>
                           i?.Children?.FirstOrDefault(x => x.Name.EqualsIgnoreCase(n)) ??
                           GetDirectChild(n));
        public IEnumerable<IHTaskItem> GetItems(string index)
        => index?.Split(new char[] { '/', ':' }, StringSplitOptions.RemoveEmptyEntries)
                                    .Aggregate((IEnumerable<IHTaskItem>)null, (i, n) =>
                                   i?.SelectMany(x => x.Children)?.Where(c => c.Name.EqualsIgnoreCase(n)) ??
                                   this?.Children?.Where(x => x.Name.EqualsIgnoreCase(n)));

        public IEnumerable<string> GetValues(string index)
        => this.GetItems(index).Select(x =>
        {
            try
            {
                return x?.GetValue();
            }
            catch { }
            return null;
        });

        public IEnumerable<dynamic> GetModels(string index)
        => this.GetItems(index).Select(x =>
        {
            try
            {
                return x?.GetModel();
            }
            catch { }
            return null;
        });


        private ValueProcessorItem GetValueProcessorItem()
            =>
            Enumerable.Aggregate(
                 this.AllTasks?.ValueProcessors?
                .OrdinalFilter(
                    this.ContentSettings?.Type?
                    .Split(new string[] { ",", "=>", "->", ">" },
                        StringSplitOptions.RemoveEmptyEntries
                        | StringSplitOptions.TrimEntries))
                 ?? Array.Empty<ValueProcessor>()
                 , ValueProcessorItem.Parse(this)
                 .DefaultVarsProcessor().CustomVarsProcessor()
                 , (i, n) => n(i, this.Cts?.Token)
                 .DefaultVarsProcessor().CustomVarsProcessor()
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
                this.Cache ??= new CachedRunDeprecated();
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

        public T GetModel<T>()
        {
            try
            {
                T GetContent()
                {
                    return (T)this.GetValueProcessorItem()?.Data;
                }
                if (this.ContentSettings.CachePeriod == ContentCachePeriod.None)
                    return GetContent();
                this.Cache ??= new CachedRunDeprecated();
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
        // ~XmlTaskItem()
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
