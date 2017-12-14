using Apollo.NetCore.Exceptions;
using Apollo.NetCore.Models;
using Apollo.NetCore.Util;
using Apollo.NetCore.Util.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Apollo.NetCore.Internals
{
    /// <summary>
    /// 远程配置仓储
    /// </summary>
    public class RemoteConfigRepository
    {
        private ConcurrentBag<IRepositoryChangeListener> _listeners = new ConcurrentBag<IRepositoryChangeListener>();
        private ThreadSafe<ApolloConfig> _config;
        private ThreadSafe<RemoteServiceConfig> _remoteServiceConfig;
        private ConfigServiceLocator _serviceLocator;
        private ApolloSettings _apolloSettings;
        private CancellationTokenSource _cancellationTokenSource;
        private ManualResetEventSlim _eventSlim;
        private string _namespaceName;
        private ILogger _logger;

        public RemoteConfigRepository(IOptions<ApolloSettings> apolloSettings,ILoggerFactory loggerFactory, ConfigServiceLocator configServiceLocator, string namespaceName = "application")
        {
            _logger = loggerFactory.CreateLogger<RemoteConfigRepository>();
            _config = new ThreadSafe<ApolloConfig>(null);
            _remoteServiceConfig = new ThreadSafe<RemoteServiceConfig>(null);
            _serviceLocator = configServiceLocator;
            _namespaceName = namespaceName;
            _apolloSettings = apolloSettings.Value;
            InitScheduleRefresh();
        }

        protected void Sync()
        {
            lock (this)
            {
                try
                {
                    var previous = _config.ReadFullFence();
                    var current = LoadApolloConfig().GetAwaiter().GetResult();
                    if (!object.ReferenceEquals(_config.ReadFullFence(), current))
                    {
                        _logger.LogInformation($"Remote Config refreshed!");
                        _config.WriteFullFence(current);
                        this.FireRepositoryChange(_namespaceName, GetConfig());
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }

        public Properties GetConfig()
        {
            if (_config.ReadFullFence() == null)
            {
                Sync();
            }
            return TransformApolloConfigToProperties(_config.ReadFullFence());
        }

        public void AddChangeListener(IRepositoryChangeListener listener)
        {
            _listeners.Add(listener);
        }

        private Properties TransformApolloConfigToProperties(ApolloConfig apolloConfig)
        {
            return new Properties(apolloConfig.Configurations);
        }

        public async Task<ApolloConfig> LoadApolloConfig()
        {
            int maxRetries = 2;
            Exception exception = null;
            var url = string.Empty;
            for (int i = 0; i < maxRetries; i++)
            {
                var randomConfigServices = new List<RemoteServiceConfig>(ConfigServices);
                //Access the server which notifies the client first?why
                //if (_remoteServiceConfig.ReadFullFence() != null)
                //{
                //    randomConfigServices.Insert(0, _remoteServiceConfig.AtomicExchange(null));
                //}

                foreach (var item in randomConfigServices)
                {
                    //TODO:缺省m_remoteMessages
                    url = AssembleQueryConfigUrl(item.HomepageUrl,_apolloSettings);
                    _logger.LogInformation($"loading config from  {url}");
                    try
                    {
                        var response = await HttpUtil.Get<ApolloConfig>(new HttpRequest(url));
                        _logger.LogInformation($"config  server responds with {response.StatusCode} HTTP status code.");
                        if (response.StatusCode == HttpStatusCode.NotModified)
                        {
                            return _config.ReadFullFence();
                        }
                        _logger.LogInformation($"Loaded config from {_namespaceName}: {response.Body}");
                        return response.Body;
                    }
                    catch (RemoteStatusCodeException ex)
                    {
                        if (ex.StatusCode == HttpStatusCode.NotFound)
                        {
                            _logger.LogError($"Could not find config for namespace - appId: {_apolloSettings.AppID}, cluster: {_apolloSettings.Cluster}, namespace: {_namespaceName}, please check whether the configs are released in Apollo! \r\n exception:{ExceptionUtil.GetDetailMessage(ex)}");
                        }
                    }
                    catch (Exception ex)
                    {
                        exception = ex;
                    }
                }
                Thread.Sleep(1000); //sleep 1 second
            }
            throw new RemoteException($"Load Apollo Config failed - appId: {_apolloSettings.AppID}, cluster: {_apolloSettings.Cluster}, namespace: {_namespaceName}, url: {url}", exception);
        }

        protected void FireRepositoryChange(string namespaceName, Properties newProperties)
        {
            foreach (var listener in _listeners)
            {
                try
                {
                    listener.OnRepositoryChange(namespaceName, newProperties);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to invoke repository change listener {listener.GetType()}:{ex}");
                }
            }
        }

        private string AssembleQueryConfigUrl(string url,ApolloSettings setting)
        {
            ///该接口会直接从数据库中获取配置，可以配合配置推送通知实现实时更新配置。
            //var uri = $"{url.TrimEnd('/')}/configs/{setting.AppID}/{setting.Cluster}/{_namespaceName}"; url内网地址
            var uri = $"{setting.Url.TrimEnd('/')}/configs/{setting.AppID}/{setting.Cluster}/{_namespaceName}";
            var query = string.Empty; 
            if (!string.IsNullOrEmpty(NetworkUtil.LocalIp))
            {
                query = $"{query}&ip={NetworkUtil.LocalIp}";
            }
            var rKey = _config.ReadFullFence()?.ReleaseKey;
            if (!string.IsNullOrWhiteSpace(rKey))
            {
                query = $"{query}&releaseKey={rKey}";
            }
            return $"{uri}?{query}";
        }

        /// <summary>
        /// 定时刷新
        /// </summary>
        private void InitScheduleRefresh()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _eventSlim = new ManualResetEventSlim(false, spinCount: 1);
            var _processQueueTask = Task.Factory.StartNew(ScheduleRefresh, _cancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        private void ScheduleRefresh()
        {
            _logger.LogInformation($"Schedule refresh with interval: {_apolloSettings.RefreshInterval} ms");
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                Task.Factory.StartNew(() => { Thread.Sleep(_apolloSettings.RefreshInterval); _eventSlim.Set(); });
                _logger.LogInformation($"refresh config for namespace: {_namespaceName}");
                Sync();
                try
                {
                    _eventSlim.Wait(_cancellationTokenSource.Token);
                    _eventSlim.Reset();
                }
                catch (OperationCanceledException ex)
                {
                    _logger.LogError($"load config from namespace: {_namespaceName} error !\r\n exception: {ExceptionUtil.GetDetailMessage(ex)}");
                    // ignore
                }
            }
        }

        private IList<RemoteServiceConfig> ConfigServices
        {
            get
            {
                var services = _serviceLocator.GetConfigServices();
                if (services.Count == 0)
                {
                    throw new RemoteException("No available config service");
                }
                return services;
            }
        }
    }
}
