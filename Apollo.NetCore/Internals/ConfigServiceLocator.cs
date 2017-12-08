﻿using Apollo.NetCore.Exceptions;
using Apollo.NetCore.Models;
using Apollo.NetCore.Util;
using Apollo.NetCore.Util.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;

namespace Apollo.NetCore.Internals
{
    /// <summary>
    /// 配置服务地址
    /// </summary>
    public class ConfigServiceLocator
    {
        private string _localIp;
        private string LocalIp
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_localIp))
                {
                    return _localIp;
                }
                lock (this)
                {
                    if (!string.IsNullOrWhiteSpace(_localIp))
                    {
                        return _localIp;
                    }
                    var _hostName = Dns.GetHostName();
                    var adresses = Dns.GetHostAddressesAsync(_hostName).GetAwaiter().GetResult();
                    foreach (var ip in adresses)
                    {
                        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            _localIp = ip.ToString();
                            return _localIp;
                        }
                    }
                    _localIp = adresses[0].ToString();
                    return _localIp;
                }
            }
        }
        private ILogger _logger;
        private ApolloSettings _apolloSettings;
        private ThreadSafe<IList<RemoteServiceConfig>> _configServices;
        public ConfigServiceLocator(IOptions<ApolloSettings> apolloSettings)
        {
            _configServices = new ThreadSafe<IList<RemoteServiceConfig>>(new List<RemoteServiceConfig>());
            _apolloSettings = apolloSettings.Value;
        }

        public void Initialize()
        {
            this.TryUpdateConfigServices();
            this.SchedulePeriodicRefresh();
        }

        /// <summary>
        /// Get the config service info from remote meta server.
        /// </summary>
        /// <returns> the services dto </returns>
        public IList<RemoteServiceConfig> GetConfigServices()
        {
            if (_configServices.ReadFullFence().Count == 0)
            {
                UpdateConfigServices();
            }
            return _configServices.ReadFullFence();
        }

        private bool TryUpdateConfigServices()
        {
            try
            {
                UpdateConfigServices();
                return true;
            }
            catch (Exception)
            {
                //ignore
            }
            return false;
        }

        private void SchedulePeriodicRefresh()
        {
            Thread t = new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        Thread.Sleep(_apolloSettings.RefreshInterval);
                        _logger.LogInformation("refresh config services");
                        TryUpdateConfigServices();
                    }
                    catch (Exception)
                    {
                        //ignore
                    }
                }
            })
            {
                IsBackground = true
            };
            t.Start();
        }

        private void UpdateConfigServices()
        {
            lock (this)
            {
                string url = AssembleMetaServiceUrl();

                var request = new HttpRequest(url);
                int maxRetries = 5;
                Exception exception = null;

                for (int i = 0; i < maxRetries; i++)
                {
                    try
                    {
                        var services = HttpUtil.Get<IList<RemoteServiceConfig>>(request).GetAwaiter().GetResult().Body;
                        if (services == null || services.Count == 0)
                        {
                            continue;
                        }
                        _configServices.WriteFullFence(services);
                        return;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex.ToString());
                        exception = ex;
                    }
                    Thread.Sleep(1000); //sleep 1 second
                }
                throw new RemoteException($"Get config services failed from {url}", exception);
            }
        }

        private string AssembleMetaServiceUrl()
        {
            string url = _apolloSettings.Url;
            string appId = _apolloSettings.AppID;

            var uri = $"{url}/services/config";
            var query = string.Empty;
            if (!string.IsNullOrWhiteSpace(appId))
            {
                query = $"{query}&appId={appId}";
            }
            if (!string.IsNullOrEmpty(LocalIp))
            {
                query = $"{query}&ip={LocalIp}";
            }
            return $"{uri}?{query}";
        }
    }
}
