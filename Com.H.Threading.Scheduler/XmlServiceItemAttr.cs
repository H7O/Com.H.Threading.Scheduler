using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;

namespace Com.H.Threading.Scheduler
{
    public class XmlServiceItemAttr : IServiceItemAttr
    {
        public IDictionary<string, string> Items { get; private set; }
        public string this[string attr]
        {
            get
            {
                if (this.Items?.Count > 0
                    &&
                    this.Items.ContainsKey(attr)
                    ) return this.Items[attr];
                return null;
            }
        }
        
        public XmlServiceItemAttr(XElement element)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));
            this.Items = element?.Attributes()?
                .ToDictionary(k => k.Name.ToString(), v => v.Value);
        }

    }
}
