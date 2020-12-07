using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;
using System.Linq;
using System.Globalization;
using Com.H.Net;
using Com.H.Text;
using System.Threading;
using Com.H.Security.Cryptography;

namespace Com.H.Threading.Scheduler
{
    public class XmlServiceItem : IServiceItem, IDisposable
    {
        private bool disposedValue;
        #region properties
        public string UniqueKey { get; set; }
        //{
        //    get
        //    {
        //        if (!string.IsNullOrWhiteSpace(uniqueKey)) return uniqueKey;
        //        return uniqueKey = this.GetUniqueKey();
        //    }
        //}
        private XElement Element { get; set; }
        private UriContentSettings UriSettings { get; set; }
        private CancellationTokenSource Cts { get; set; }
        public string Name { get; private set; }
        public string GetValue()
        {
            try
            {
                if (this.UriSettings.UriTypeContent == UriContentType.No)
                    return this.Element.Value;
                if (

                        this.UriSettings.UriTypeContent == UriContentType.Yes
                        && !Uri.IsWellFormedUriString(this.Element.Value, UriKind.Absolute)
                    )
                {
                    throw new FormatException(
                        $"Invalid uri format for {this.Name}: {this.Element.Value}");
                }
                if (this.UriSettings.CachePeriod == UriContentCachePeriod.None)
                {
                    return new Uri(this.Element.Value)
                        .GetContentAsync(this.Cts?.Token,
                        this.UriSettings.Referer, this.UriSettings.UserAgent)
                        .GetAwaiter().GetResult();
                }
                if (this.Cache == null)
                    this.Cache = new CachedRun();
                Func<string> GetContent = () =>
                {
                    var value = new Uri(this.Element.Value).GetContentAsync(
                                this.Cts?.Token, this.UriSettings.Referer,
                                this.UriSettings.UserAgent)
                        .GetAwaiter().GetResult();
                    if (value == null)
                        throw new TimeoutException(
                            $"Uri settings retrieval timed-out for {this.Name}: {this.Element.Value}");
                    return value;
                };
                return this.UriSettings.CachePeriod ==
                    UriContentCachePeriod.Miliseconds ?
                    this.Cache.Run<string>(GetContent,
                        TimeSpan.FromMilliseconds((int)this.UriSettings.CacheInMilisec),
                            this.Name)
                    : this.Cache.Run<string>(GetContent,
                        DateTime.Today.AddDays(1), this.Name);

            }
            catch
            {
                throw;
            }

        }
        public IServiceItem Parent { get; private set; }
        public IServiceItemAttr Attributes { get; private set; }
        public ICollection<IServiceItem> Children { get; private set; }
        public IServiceControlProperties Schedule { get; private set; }
        private CachedRun Cache { get; set; }

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

        public XmlServiceItem(XElement element, CancellationToken? token = null)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));
            this.Element = element;
            if (token != null) this.Cts = CancellationTokenSource
                    .CreateLinkedTokenSource((CancellationToken)token);
            this.Attributes = new XmlServiceItemAttr(this.Element);
            this.Children = this.Element.Elements()?.Select(x => new XmlServiceItem(x, this.Cts?.Token)
            { Parent = this })?.ToArray() ?? Array.Empty<XmlServiceItem>();
            this.Name = this.Element.Name.LocalName;
            this.UriSettings = this.Attributes.GetUriSettings();
            if (this["sys"] != null)
            {
                this.Schedule = new ServiceControlProperties(this["sys"]);
            }
            this.UniqueKey = this.Element.ToString().ToSha256InBase64String();
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
