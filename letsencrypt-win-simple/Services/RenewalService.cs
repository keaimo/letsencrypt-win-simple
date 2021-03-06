﻿using LetsEncrypt.ACME.Simple.Clients;
using LetsEncrypt.ACME.Simple.Extensions;
using LetsEncrypt.ACME.Simple.Plugins;
using LetsEncrypt.ACME.Simple.Plugins.TargetPlugins;
using LetsEncrypt.ACME.Simple.Plugins.ValidationPlugins;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace LetsEncrypt.ACME.Simple.Services
{
    class RenewalService
    {
        private ILogService _log;
        private Options _options;
        private Settings _settings;
        private TaskSchedulerService _taskScheduler;
        private string _configPath;
        public float RenewalPeriod { get; set; } = 60;

        public RenewalService(Options options, ILogService log, Settings settings, InputService input, string clientName, string configPath)
        {
            _log = log;
            _options = options;
            _settings = settings;
            _configPath = configPath;
            _taskScheduler = new TaskSchedulerService(options, input, log, clientName);
            ParseRenewalPeriod();
            // Trigger init of renewals cache
            var x = Renewals;
        }

        private void ParseRenewalPeriod()
        {
            try
            {
                RenewalPeriod = Properties.Settings.Default.RenewalDays;
                _log.Debug("Renewal period: {RenewalPeriod}", RenewalPeriod);
            }
            catch (Exception ex)
            {
                _log.Warning("Error reading RenewalDays from app config, defaulting to {RenewalPeriod} Error: {@ex}", RenewalPeriod.ToString(), ex);
            }
        }

        public IEnumerable<ScheduledRenewal> Renewals
        {
            get
            {
                if (_renewalsCache == null)
                {
                    if (_settings.RenewalStore != null)
                    {
                        _renewalsCache = _settings.RenewalStore.Select(x => Load(x, _configPath)).Where(x => x != null).ToList();
                    }
                    else
                    {
                        _renewalsCache = new List<ScheduledRenewal>();
                    }
                }
                return _renewalsCache;
            }
            set
            {
                _renewalsCache = value.ToList();
                _settings.RenewalStore = _renewalsCache.Select(x => x.Save(_configPath)).ToArray();
            }
        }
        private List<ScheduledRenewal> _renewalsCache = null;

        public ScheduledRenewal Find(Target target)
        {
            return Renewals.Where(r => string.Equals(r.Binding.Host, target.Host)).FirstOrDefault();
        }

        public ScheduledRenewal CreateOrUpdate(Target target, RenewResult result)
        {
            if (!_options.NoTaskScheduler)
            {
                _taskScheduler.EnsureTaskScheduler();
            }

            var renewals = Renewals.ToList();
            var renewal = Find(target);
            if (renewal == null)
            {
                renewal = new ScheduledRenewal();
                renewal.History = new List<RenewResult>();
                renewals.Add(renewal);
                _log.Information(true, "Adding renewal for {target}", target);
            }
            else
            {
                _log.Debug("Updating existing renewal");
            }

            renewal.Binding = target;
            renewal.CentralSsl = _options.CentralSslStore;
            renewal.Date = DateTime.UtcNow.AddDays(RenewalPeriod);
            renewal.KeepExisting = _options.KeepExisting.ToString();
            renewal.Script = _options.Script;
            renewal.ScriptParameters = _options.ScriptParameters;
            renewal.Warmup = _options.Warmup;
            renewal.History.Add(result);

            Renewals = renewals;
            _log.Information(true, "Next renewal scheduled at {date}", renewal.Date.ToUserString());
            return renewal;
        }

        private ScheduledRenewal Load(string renewal, string path)
        {
            var result = JsonConvert.DeserializeObject<ScheduledRenewal>(renewal);

            if (result == null || result.Binding == null)
            {
                _log.Error("Unable to deserialize renewal {renewal}", renewal);
                return null;
            }

            if (result.History == null)
            {
                result.History = new List<RenewResult>();
                var historyFile = ScheduledRenewal.HistoryFile(result.Binding, path);
                if (historyFile.Exists)
                {
                    try
                    {
                        result.History = JsonConvert.DeserializeObject<List<RenewResult>>(File.ReadAllText(historyFile.FullName));
                    }
                    catch
                    {
                        _log.Warning("Unable to read history file {path}", historyFile.Name);
                    }
                }
            }

            if (result.Binding.AlternativeNames == null)
            {
                result.Binding.AlternativeNames = new List<string>();
            }

            if (result.Binding.HostIsDns == null)
            {
                result.Binding.HostIsDns = !result.San;
            }

            if (result.Binding.IIS == null)
            {
                result.Binding.IIS = !(result.Binding.PluginName == ScriptClient.PluginName);
            }
            return result;
        }

    }
}
