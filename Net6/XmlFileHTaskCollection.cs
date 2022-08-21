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
using Com.H.Events;

namespace Com.H.Threading.Scheduler
{

    public class XmlFileHTaskCollection : IHTaskCollection
    {
        public CancellationTokenSource? Cts {get;set;}
        internal class TasksFileContainer
        {
            internal string? FileName { get; set; }
            internal IHTaskItem? Task { get; set; }
        }
        #region properties


        private List<TasksFileContainer>? Tasks { get; set; }

        /// <summary>
        /// Allows for adding custom value post processing logic when retrieving configuration values.
        /// Each tag in the configuration file could have the reserved attribute 'content_type' defined with 
        /// a value that if matches a key in this dictionary would instruct the scheduler to 
        /// lookup the dictionary key corresponding Func, executes it, and returns the resulting value
        /// instead of the actual tag value.
        /// By default, the engine adds UriValueProcessor that correspond to 'content_type' value of 'uri'
        /// </summary>
        public ConcurrentDictionary<string, ValueProcessor?> ValueProcessors { get; private set; }
        private DateTime? TasksLastModified { get; set; }
        private int? TasksFileCount { get; set; }
        private string BasePath { get; set; }
        private object TaskLock { get; set; } = new object();

        int ICollection<IHTaskItem?>.Count => this.Tasks?.Count??0;

        bool ICollection<IHTaskItem?>.IsReadOnly => true;
        #endregion

        #region constructor
        public XmlFileHTaskCollection(string taskCollectionPath, CancellationToken? token)
        {
            if (string.IsNullOrWhiteSpace(taskCollectionPath))
                throw new ArgumentNullException(nameof(taskCollectionPath));
            this.BasePath = taskCollectionPath;
            if (!Directory.Exists(this.BasePath)
                && !File.Exists(this.BasePath))
                throw new FileNotFoundException(this.BasePath);
            this.Cts = token is null ? 
                new CancellationTokenSource() : 
                CancellationTokenSource.CreateLinkedTokenSource((CancellationToken) token);
            this.ValueProcessors = new ConcurrentDictionary<string, ValueProcessor?>();
            AddCustomPlugins();

            // default processors cannot be null
#pragma warning disable CS8621 // Nullability of reference types in return type doesn't match the target delegate (possibly because of nullability attributes).
            _ = this.ValueProcessors.TryAdd("uri", DefaultValueProcessors.UriProcessor);
            _ = this.ValueProcessors.TryAdd("csv", DefaultValueProcessors.CsvDataModelProcessor);
            _ = this.ValueProcessors.TryAdd("psv", DefaultValueProcessors.PsvDataModelProcessor);
            _ = this.ValueProcessors.TryAdd("json", DefaultValueProcessors.JsonDataModelProcessor);
            _ = this.ValueProcessors.TryAdd("xml", DefaultValueProcessors.XmlDataModelProcessor);
#pragma warning restore CS8621 // Nullability of reference types in return type doesn't match the target delegate (possibly because of nullability attributes).
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
                var type = Regex.Match(file, @".*\.(?<type>.*)\.[d|D][l|L]{2}$")
                    .Groups["type"]?
                    .Value?.ToLower();
                if (string.IsNullOrWhiteSpace(type)) continue;
                foreach (Type assemblyType in Assembly.LoadFrom(file).GetTypes())
                {
                    if (typeof(IValueProcessor).IsAssignableFrom(assemblyType))
                    {
                        if (assemblyType is null) continue;
                        _ = this.ValueProcessors.TryAdd(type,

#pragma warning disable CS8621 // Nullability of reference types in return type doesn't match the target delegate (possibly because of nullability attributes).
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                                ((IValueProcessor)Activator.CreateInstance(assemblyType)).GetProcessor);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning restore CS8621 // Nullability of reference types in return type doesn't match the target delegate (possibly because of nullability attributes).

                    }
                }
            }

        }


        #endregion
        #region load from disk
        private ICollection<IHTaskItem?> GetTasks()
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
                    return this.Tasks.Select(x => x.Task).ToList();
                this.Tasks ??= new List<TasksFileContainer>();

                foreach (var file in currentFiles.Where(x =>
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
                    catch (Exception ex)
                    {
                        this.OnErrorAsync(new HErrorEventArgs(this, 
                            new FormatException($"XML format error trying to load {file.FullName}: {ex.Message}")));
                    }

                }

                this.TasksLastModified = currentDate;
                this.TasksFileCount = currentFileCount;
                return this.Tasks.Select(x => x.Task).ToList();
            } // lock end
        }

        #endregion

        #region IEnumerator
        public IEnumerator<IHTaskItem?> GetEnumerator()
        {
            return this.GetTasks()?.GetEnumerator() ?? Enumerable.Empty<IHTaskItem?>().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)this.GetTasks())?.GetEnumerator()
                ?? ((IEnumerable)Array.Empty<IHTaskItem?>()).GetEnumerator();
        }

        void ICollection<IHTaskItem?>.Add(IHTaskItem? item)
            => throw new NotImplementedException();
        void ICollection<IHTaskItem?>.Clear()
            => throw new NotImplementedException();

        bool ICollection<IHTaskItem?>.Contains(IHTaskItem? item)
            => this.GetTasks()?.Contains(item) ?? false;


        void ICollection<IHTaskItem?>.CopyTo(IHTaskItem?[] array, int arrayIndex)
        => this.GetTasks()?.CopyTo(array, arrayIndex);


        bool ICollection<IHTaskItem?>.Remove(IHTaskItem? item)
            => throw new NotImplementedException();
        #endregion

        #region events

        #region OnError

        
        /// <summary>
        /// Gets triggered whenever there is an error that might get supressed if retry on error is enabled
        /// </summary>
        public event HErrorEventHandler? Error;
        protected virtual Task OnErrorAsync(HErrorEventArgs e)
        {
            if (e == null) return Task.CompletedTask;
            if (this.Cts is null)
                throw new NullReferenceException("Cts is null in XmlFileHTaskCollection.OnErrorAsync()");
            return Cancellable.CancellableRunAsync(
                () => Error?.Invoke(e.Sender, e)
                , this.Cts.Token);
        }

        #endregion

        #endregion
    }
}
