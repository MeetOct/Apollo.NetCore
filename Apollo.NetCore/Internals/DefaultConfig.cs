using Apollo.NetCore.Enums;
using Apollo.NetCore.Models;
using Apollo.NetCore.Util;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Apollo.NetCore.Internals
{
    public class DefaultConfig:IRepositoryChangeListener,IConfig
    {
        private ThreadSafe<Properties> _configProperties;
        private RemoteConfigRepository _configRepository;
        public event ConfigChangeEvent ConfigChanged;
        private string _namespaceName;
        private ILogger _logger;

        public DefaultConfig(RemoteConfigRepository configRepository, ILoggerFactory loggerFactory,string namespaceName = "application")
        {
            _namespaceName = namespaceName;
            _configProperties = new ThreadSafe<Properties>(null);
            _logger = loggerFactory.CreateLogger<DefaultConfig>();
            _configRepository = configRepository;
            Initialize();
        }

        private void Initialize()
        {
            try
            {
                _configProperties.WriteFullFence(_configRepository.GetConfig());
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"Init Apollo Local Config failed - namespace: {_namespaceName}, reason: {ExceptionUtil.GetDetailMessage(ex)}.");
            }
            finally
            {
                //register the change listener no matter config repository is working or not
                //so that whenever config repository is recovered, config could get changed
                _configRepository.AddChangeListener(this);
            }
        }

        public string GetProperty(string key, string defaultValue)
        {
            if (_configProperties.ReadFullFence() !=null)
            {
                return _configProperties.ReadFullFence().GetProperty(key);
            }
            _logger.LogInformation($"Could not load config for namespace {_namespaceName} from Apollo, please check whether the configs are released in Apollo! Return default value now!");
            return defaultValue;
        }

        public void OnRepositoryChange(string namespaceName, Properties newProperties)
        {
            lock (this)
            {
                var newConfigProperties = new Properties(newProperties);
                var actualChanges = UpdateAndGetChanges(newConfigProperties);
                //check double checked result
                if (actualChanges.Count == 0)
                {
                    return;
                }
                FireConfigChange(new ConfigChangeEventArgs(_namespaceName, actualChanges));
            }
        }

        private IDictionary<string, ConfigChange> UpdateAndGetChanges(Properties newConfigProperties)
        {
            var changes = GetChangesProperties(_configProperties.ReadFullFence(), newConfigProperties);

            //TODO: Double check since DefaultConfig has multiple config sources 
            //1. use getProperty to update configChanges's old value
            foreach (var change in changes)
            {
                change.OldValue = this.GetProperty(change.PropertyName, change.OldValue);
            }

            //2. update m_configProperties
            _configProperties.WriteFullFence(newConfigProperties);

            IDictionary<string, ConfigChange> actualChanges = new Dictionary<string, ConfigChange>();
            //3. use getProperty to update configChange's new value and calc the final changes
            foreach (var change in changes)
            {
                change.NewValue = this.GetProperty(change.PropertyName, change.NewValue);
                switch (change.ChangeType)
                {
                    case PropertyChangeType.添加:
                        if (string.Equals(change.OldValue, change.NewValue))
                        {
                            break;
                        }
                        if (change.OldValue != null)
                        {
                            change.ChangeType = PropertyChangeType.变更;
                        }
                        actualChanges[change.PropertyName] = change;
                        break;
                    case PropertyChangeType.变更:
                        if (!string.Equals(change.OldValue, change.NewValue))
                        {
                            actualChanges[change.PropertyName] = change;
                        }
                        break;
                    case PropertyChangeType.删除:
                        if (string.Equals(change.OldValue, change.NewValue))
                        {
                            break;
                        }
                        if (change.NewValue != null)
                        {
                            change.ChangeType = PropertyChangeType.变更;
                        }
                        actualChanges[change.PropertyName] = change;
                        break;
                    default:
                        //do nothing
                        break;
                }
            }

            return actualChanges;
        }

        protected ICollection<ConfigChange> GetChangesProperties(Properties previous, Properties current)
        {
            if (previous == null)
            {
                previous = new Properties();
            }
            if (current == null)
            {
                current = new Properties();
            }
            ISet<string> previousKeys = previous.GetPropertyNames();
            ISet<string> currentKeys = current.GetPropertyNames();

            IEnumerable<string> commonKeys = previousKeys.Intersect(currentKeys);
            IEnumerable<string> newKeys = currentKeys.Except(commonKeys);
            IEnumerable<string> removedKeys = previousKeys.Except(commonKeys);

            ICollection<ConfigChange> changes = new LinkedList<ConfigChange>();

            foreach (string newKey in newKeys)
            {
                changes.Add(new ConfigChange(_namespaceName, newKey, null, current.GetProperty(newKey), PropertyChangeType.添加));
            }

            foreach (string removedKey in removedKeys)
            {
                changes.Add(new ConfigChange(_namespaceName, removedKey, previous.GetProperty(removedKey), null, PropertyChangeType.删除));
            }

            foreach (string commonKey in commonKeys)
            {
                var previousValue = previous.GetProperty(commonKey);
                var currentValue = current.GetProperty(commonKey);
                if (string.Equals(previousValue, currentValue))
                {
                    continue;
                }
                changes.Add(new ConfigChange(_namespaceName, commonKey, previousValue, currentValue, PropertyChangeType.变更));
            }
            return changes;
        }

        protected void FireConfigChange(ConfigChangeEventArgs changeEvent)
        {
            if (ConfigChanged != null)
            {
                foreach (ConfigChangeEvent handler in ConfigChanged.GetInvocationList())
                {
                    ///TODO:待优化
                    handler.Invoke(this,changeEvent);
                }
            }
        }
    }
}
