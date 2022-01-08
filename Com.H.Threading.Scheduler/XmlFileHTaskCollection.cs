using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Threading;
using Com.H.Threading.Scheduler.VP;
using Com.H.IO;
using Com.H.Text;
using System.Text.RegularExpressions;
using System.Reflection;

namespace Com.H.Threading.Scheduler
{

    public class XmlFileHTaskCollection : IHTaskCollection
    {
        internal class TasksFileContainer
        {
            internal string FileName { get; set; }
            internal IHTaskItem Task { get; set; }
        }
        #region properties

        
        private List<TasksFileContainer> Tasks { get; set; }

        /// <summary>
        /// Allows for adding custom value post processing logic when retrieving configuration values.
        /// Each tag in configuration file could have the reserved attribute 'content_type' defined with 
        /// a value that if matches a key in this dictionary would instruct the scheduler to 
        /// lookup the dictionary key corresponding Func, execute it, and return the resulting value
        /// instead of the actual tag value.
        /// By default, the engine adds UriValueProcessor that correspond to 'content_type' value of 'uri'
        /// </summary>
        public ConcurrentDictionary<string, ValueProcessor> ValueProcessors { get; private set; }
        private DateTime? TasksLastModified { get; set; }
        private int? TasksFileCount { get; set; }
        private string BasePath { get; set; }
        private object TaskLock { get; set; } = new object();

        int ICollection<IHTaskItem>.Count => this.Tasks.Count;

        bool ICollection<IHTaskItem>.IsReadOnly => true;
        #endregion

        #region constructor
        public XmlFileHTaskCollection(string taskCollectionPath)
        {
            if (string.IsNullOrWhiteSpace(taskCollectionPath))
                throw new ArgumentNullException(nameof(taskCollectionPath));
            this.BasePath = taskCollectionPath;
            if (!Directory.Exists(this.BasePath) 
                && !File.Exists(this.BasePath))
                throw new FileNotFoundException(this.BasePath);

            this.ValueProcessors = new ConcurrentDictionary<string, ValueProcessor>();
            AddCustomPlugins();
            this.ValueProcessors.TryAdd("uri", DefaultValueProcessors.UriProcessor);
            this.ValueProcessors.TryAdd("csv", DefaultValueProcessors.CsvDataModelProcessor);
            this.ValueProcessors.TryAdd("psv", DefaultValueProcessors.PsvDataModelProcessor);
            this.ValueProcessors.TryAdd("json", DefaultValueProcessors.JsonDataModelProcessor);
            this.ValueProcessors.TryAdd("xml", DefaultValueProcessors.XmlDataModelProcessor);

        }
        private void AddCustomPlugins()
        {
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Select(x => Path.GetFileName(x.Location));

            foreach (string file in Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, 
                "Com.H.Threading.Scheduler.VP.*.dll")
                .Except(loadedAssemblies)
                )
            {
                var type = Regex.Match(file, @".*\.(?<type>.*)\.[dlD][l|L]{2}$")
                    .Groups["type"]?
                    .Value?.ToLower();
                if (string.IsNullOrWhiteSpace(type)) continue;
                // Console.WriteLine($"file = {file}, type = {type}");
                // continue;
                foreach (Type assemblyType in Assembly.LoadFrom(file).GetTypes())
                {
                    if (typeof(IValueProcessor).IsAssignableFrom(assemblyType))
                    {
                        //Console.WriteLine($"vp : {type} added = " +
                        _ = this.ValueProcessors.TryAdd(type,
                            ((IValueProcessor)Activator.CreateInstance(assemblyType)).GetProcessor);
                            //);
                    }
                }
            }

        }


        #endregion
        #region load from disk
        private ICollection<IHTaskItem> GetTasks()
        {
            if (!File.Exists(this.BasePath)
                && !Directory.Exists(this.BasePath)
                )
                throw new FileNotFoundException(this.BasePath);
            var currentFiles = this.BasePath.ListFiles(true, @".*\.xml$");
            var currentDate = currentFiles.Select(x => x.LastWriteTime).Max();

            var currentFileCount = currentFiles.Count();

            lock (this.TaskLock)
            {
                if (this.Tasks != null
                        && this.TasksLastModified != null
                        && this.TasksFileCount != null
                        && currentDate <= this.TasksLastModified
                        && currentFileCount == this.TasksFileCount
                        )
                    return this.Tasks.Select(x=>x.Task).ToList();
                if (this.Tasks == null) this.Tasks = new List<TasksFileContainer>();
                
                foreach(var file in currentFiles.Where(x=>
                this.TasksLastModified == null
                ||
                x.LastWriteTime > this.TasksLastModified))
                {
                    try
                    {
                        var tasksToAdd = XElement.Load(file.FullName)
                                .Elements().Select(x =>
                                new TasksFileContainer()
                                {  
                                    Task = new XmlHTaskItem(this, x) { FullName = file.FullName },
                                    FileName = file.FullName
                                });
                        this.Tasks.RemoveAll(x => x.FileName.EqualsIgnoreCase(file.FullName));
                        this.Tasks.AddRange(tasksToAdd);
                    }
                    catch(Exception ex)
                    {
                        // todo: add error event to be consumed by the scheduler and 
                        // spit out via scheduler own OnError event.
                        // This to allow bypassing of erroneous XML configurations instead of
                        // halting the parsing process
                        throw new FormatException($"XML format error trying to load {file.FullName}: {ex.Message}");
                    }

                } 

                this.TasksLastModified = currentDate;
                this.TasksFileCount = currentFileCount;
                return this.Tasks.Select(x => x.Task).ToList();
            } // lock end
        }

        #endregion

        #region IEnumerator
        public IEnumerator<IHTaskItem> GetEnumerator()
        {
            return this.GetTasks()?.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)this.GetTasks())?.GetEnumerator();
        }

        void ICollection<IHTaskItem>.Add(IHTaskItem item)
            => throw new NotImplementedException();
        void ICollection<IHTaskItem>.Clear()
            => throw new NotImplementedException();

        bool ICollection<IHTaskItem>.Contains(IHTaskItem item)
            => this.GetTasks()?.Contains(item) ?? false;


        void ICollection<IHTaskItem>.CopyTo(IHTaskItem[] array, int arrayIndex)
        => this.GetTasks()?.CopyTo(array, arrayIndex);


        bool ICollection<IHTaskItem>.Remove(IHTaskItem item)
            => throw new NotImplementedException();
        #endregion
    }
}
