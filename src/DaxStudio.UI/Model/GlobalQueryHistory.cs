﻿using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DaxStudio.UI.Events;
using System.ComponentModel.Composition;
using System.IO;
using Newtonsoft.Json;
using Serilog;
using DaxStudio.Interfaces;

namespace DaxStudio.UI.Model
{
    [Export]
    public class GlobalQueryHistory : IHandle<QueryHistoryEvent>
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly string _queryHistoryPath;
        private readonly IGlobalOptions _globalOptions;

        [ImportingConstructor]
        public GlobalQueryHistory(IEventAggregator eventAggregator, IGlobalOptions globalOptions )
        {
            _globalOptions = globalOptions;
            _eventAggregator = eventAggregator;
            _eventAggregator.Subscribe(this);
            QueryHistory = new BindableCollection<QueryHistoryEvent>();

            _queryHistoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DaxStudio",
                "QueryHistory");
            Log.Debug("{class} {method} {message} {value}", "GlobalQueryHistory", "Constructor", "Setting Query History Path", _queryHistoryPath);
            if (!Directory.Exists(_queryHistoryPath))
            {
                Log.Debug("{class} {method} {message} {value}", "GlobalQueryHistory", "Constructor", "Creating Query History Path", _queryHistoryPath);
                Directory.CreateDirectory(_queryHistoryPath);
            }
            LoadHistoryFilesAsync();
    
        }

        private void LoadHistoryFilesAsync()
        {
            Task.Run(() =>
            {
                DirectoryInfo d = new DirectoryInfo(_queryHistoryPath);
                foreach (var fileInfo in d.GetFiles("*-query-history.json", SearchOption.TopDirectoryOnly))
                {

                    using (StreamReader file = File.OpenText(fileInfo.FullName))
                    {
                        JsonSerializer serializer = new JsonSerializer();
                        QueryHistoryEvent queryHistory = (QueryHistoryEvent)serializer.Deserialize(file, typeof(QueryHistoryEvent));
                        QueryHistory.Add(queryHistory);
                    }

                }
            });
        }

        public void Handle(QueryHistoryEvent message)
        {
            QueryHistory.Add(message);
            while (QueryHistory.Count > _globalOptions.MaxQueryHistory)
            {
                QueryHistory.RemoveAt(0);
            }
            SaveHistoryFile(message);
        }

        private void SaveHistoryFile(QueryHistoryEvent message)
        {
            File.WriteAllText(UniqueFilePath(message),  Newtonsoft.Json.JsonConvert.SerializeObject(message));
            EnsureFileLimitAsync();
        }

        private void EnsureFileLimitAsync()
        {
            foreach (var fi in new DirectoryInfo(_queryHistoryPath)
                .GetFiles()
                .OrderByDescending(x => x.LastWriteTime)
                .Skip(_globalOptions.MaxQueryHistory))
                fi.Delete();
        }

        private string UniqueFilePath(QueryHistoryEvent message)
        {
            return Path.Combine(_queryHistoryPath,
                string.Format("{0}-query-history.json"
                , message.StartTime.ToString("yyyyMMddHHmmssfff")));
        }

        public BindableCollection<QueryHistoryEvent> QueryHistory { get; set; }
    }
}