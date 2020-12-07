﻿using Com.H.IO;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Com.H.Threading.Scheduler
{
    public class XmlFileServiceTimeLogger : IServiceTimeLogger
    {
        #region properties
        private string LogFilePath { get; set; }
        private ConcurrentDictionary<string, TimeLog> TimeLogs { get; set; }
        private ReaderWriterLockSlim RWLock { get; set; }

        #endregion

        #region constructor
        /// <summary>
        /// If the constructor is called without providing logFilePath, the logger will revert to memory only logs.
        /// This means, on a shutdown and restart of the application, the schedular won't pickup from where it left off.
        /// i.e. any services that already ran throughout the day before the shutdown would run again after the restart 
        /// as there won't be a perminant log referece of them to load from disk after restarting.
        /// </summary>
        /// <param name="logFilePath"></param>
        public XmlFileServiceTimeLogger(string logFilePath = null)
        {
            this.LogFilePath = logFilePath;
            this.RWLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
            
            this.Load();
        }
        #endregion

        #region indexer
        public TimeLog this[string key] 
        {
            get
            {
                try
                {
                    this.RWLock.EnterReadLock();
                    if (this.TimeLogs.ContainsKey(key) != false)
                        return this.TimeLogs[key];
                }
                catch { throw; }
                finally
                {
                    this.RWLock.ExitReadLock();
                }
                try
                {
                    this.RWLock.EnterWriteLock();
                    return this.TimeLogs[key] = new TimeLog();
                }
                catch { throw; }
                finally
                {
                    this.RWLock.ExitWriteLock();
                }
            }
        }
        #endregion

        #region file load / save

        private void CleanExpiredLogs()
        {
            try
            {
                this.RWLock.EnterWriteLock();
                foreach (var key in this.TimeLogs
                    .Where(x => x.Value.LastExecuted < DateTime.Today)
                    .Select(x => x.Key))
                    this.TimeLogs.TryRemove(key, out _);
            }
            catch { throw; }
            finally
            {
                this.RWLock.ExitWriteLock();
            }
        }
        public void Save()
        {
            this.CleanExpiredLogs();
            if (string.IsNullOrWhiteSpace(this.LogFilePath)) return;
            this.LogFilePath.EnsureParentDirectory();
            XElement xml = null;
            try
            {
                this.RWLock.EnterReadLock();
                xml = new XElement("logs",
                this.TimeLogs.Select(x =>
                        new XElement("log",
                            new XElement("key", new XCData(x.Key)),
                            new XElement("last_executed",
                                new XCData(x.Value.LastExecuted?.ToString("yyyy-MM-dd HH:mm:ss.fffff",
                                    CultureInfo.InvariantCulture)??string.Empty)),
                            new XElement("last_error",
                                new XCData(x.Value.LastError?.ToString("yyyy-MM-dd HH:mm:ss.fffff",
                                    CultureInfo.InvariantCulture) ?? string.Empty)),
                            new XElement("error_retry_count",
                                new XCData(x.Value.ErrorCount.ToString(
                                    CultureInfo.InvariantCulture)))
                            )));
            }
            catch{ throw; }
            finally
            {
                this.RWLock.ExitReadLock();
            }
            
            File.WriteAllText(this.LogFilePath, xml.ToString(), Encoding.UTF8);
        }
        private void Load()
        {
            if (string.IsNullOrWhiteSpace(this.LogFilePath)
                || !File.Exists(this.LogFilePath))
            {
                this.TimeLogs = new ConcurrentDictionary<string, TimeLog>();
                return;
            }
            try
            {
                this.RWLock.EnterWriteLock();
                this.TimeLogs = new ConcurrentDictionary<string, TimeLog>(
                    XElement.Load(this.LogFilePath).Elements()
                    .ToDictionary(key => key.Element("key").Value,
                        value => new TimeLog()
                        { 
                            LastExecuted =
                             DateTime.TryParse(value.Element("last_executed")?.Value, out _) ?
                             (DateTime?)DateTime.ParseExact(value.Element("last_executed").Value,
                             "yyyy-MM-dd HH:mm:ss.fffff", CultureInfo.InvariantCulture)
                            :null,
                            LastError = 
                                DateTime.TryParse(value.Element("last_error")?.Value, out _)?
                                (DateTime?) DateTime.ParseExact(value.Element("last_error").Value,
                                "yyyy-MM-dd HH:mm:ss.fffff", CultureInfo.InvariantCulture)
                                :null,
                            ErrorCount = int.Parse(value.Element("error_retry_count").Value
                            , CultureInfo.InvariantCulture)
                        }
                    ));
            }
            catch 
            {
                bool cleanedUp = false;
                try
                {
                    if (File.Exists(this.LogFilePath)) File.Delete(this.LogFilePath);
                    cleanedUp = true;
                }
                catch { }
                if (!cleanedUp) throw;
            }
            finally
            {
                this.RWLock.ExitWriteLock();
            }
        }
        #endregion

    }
}
