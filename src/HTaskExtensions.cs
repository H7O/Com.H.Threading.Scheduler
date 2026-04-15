using Com.H.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Com.H.Threading.Scheduler
{
    #region sub classes
    public enum ContentCachePeriod
    {
        None = 0,
        OncePerDay = 1,
        Miliseconds = 2
    }

    public class ContentSettings
    {
        public string? Type { get; set; }
        public ContentCachePeriod CachePeriod { get; set; }
        public int? CacheInMilisec { get; set; }
    }


    #endregion
    public static class HTaskExtensions
    {

        public static ContentSettings GetContentSettings(this IHTaskItemAttr attr)
        {
            var settings = new ContentSettings()
            { CachePeriod = ContentCachePeriod.None };
            if (attr == null) return settings;

            settings.Type = attr["content_type"];

            // cache type valid values: "none", ("once per day" / "daily" / "once_per_day"), or a numeric value represnting cache time in miliseconds.
            var cachePeriod = attr["content_cache"];
            if (cachePeriod != null && !cachePeriod.EqualsIgnoreCase("none"))
            {
                if (new string[] { "once_per_day", "once per day", "daily" }.Any(x => x.EqualsIgnoreCase(cachePeriod)))
                    settings.CachePeriod = ContentCachePeriod.OncePerDay;
                else
                {
                    if (int.TryParse(cachePeriod, out int cacheInMilisec)
                        && cacheInMilisec > 0
                        )
                    {
                        settings.CachePeriod = ContentCachePeriod.Miliseconds;
                        settings.CacheInMilisec = cacheInMilisec;
                    }
                }
            }
            return settings;
        }



    }




}
