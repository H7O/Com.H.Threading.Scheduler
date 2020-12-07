using Com.H.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Com.H.Threading.Scheduler
{
    #region sub classes
    public enum UriContentType
    {
        No = 0,
        Yes = 1,
        Auto = 2
    }
    public enum UriContentCachePeriod
    {
        None = 0,
        OncePerDay = 1,
        Miliseconds = 2
    }

    public class UriContentSettings
    {
        public UriContentType UriTypeContent { get; set; }
        public UriContentCachePeriod CachePeriod { get; set; }
        public string Referer { get; set; }
        public string UserAgent { get; set; }
        public int? CacheInMilisec { get; set; }
    }
    #endregion
    public static class ServiceExtensions
    {
        public static UriContentSettings GetUriSettings(this IServiceItemAttr attr)
        {
            var uriSettings = new UriContentSettings()
            { UriTypeContent = UriContentType.No, CachePeriod = UriContentCachePeriod.None };
            if (attr == null) return uriSettings;
            
            // is_uri valid values: "yes", "true", and "auto", anything else is considered "no"
            var isUriSettings = attr["uri_content"];

            switch (isUriSettings)
            {
                case string uriType
                    when uriType.EqualsIgnoreCase("yes") || uriType.EqualsIgnoreCase("true"):
                    uriSettings.UriTypeContent = UriContentType.Yes;
                    break;
                case string uriType when uriType.EqualsIgnoreCase("auto"):
                    uriSettings.UriTypeContent = UriContentType.Auto;
                    break;
                case null:
                default: 
                    uriSettings.UriTypeContent = UriContentType.No;
                    break;
            }

            if (uriSettings.UriTypeContent == UriContentType.No) return uriSettings;

            // cache type valid values: "none", ("once per day" / "daily" / "once_per_day"), or a numeric value represnting cache time in miliseconds.
            var cachePeriod = attr["uri_content_cache"];
            if (cachePeriod != null && !cachePeriod.EqualsIgnoreCase("none"))
            {
                if (new string[] { "once_per_day", "once per day", "daily" }.Any(x => x.EqualsIgnoreCase(cachePeriod)))
                    uriSettings.CachePeriod = UriContentCachePeriod.OncePerDay;
                else
                {
                    int cacheInMilisec;
                    if (int.TryParse(cachePeriod, out cacheInMilisec)
                        && cacheInMilisec > 0
                        )
                    {
                        uriSettings.CachePeriod = UriContentCachePeriod.Miliseconds;
                        uriSettings.CacheInMilisec = cacheInMilisec;
                    }
                }
            }
            uriSettings.UserAgent = attr["uri_user_agent"];
            uriSettings.Referer = attr["uri_referer"];

            return uriSettings;

        }


        //public static string GetUniqueKey(this IServiceItem item)
        //=> $"{item?.Name}/{item?.GetValue()}" 
        //    + item?.Attributes?.Items?.Keys==null?""
        //    :string.Join("/",
        //        item?.Attributes?.Items?.Keys?
        //        .Concat(item?.Attributes?.Items?.Values));


        


    }




}
