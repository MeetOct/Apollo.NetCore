using System.Net;

namespace Apollo.NetCore.Util
{
    static class NetworkUtil
    {
        private static string  _localIp;
        private static object obj = new object();
        public static string LocalIp
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_localIp))
                {
                    return _localIp;
                }
                lock (obj)
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
    }
}
