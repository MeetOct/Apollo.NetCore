using Apollo.NetCore.Exceptions;
using Newtonsoft.Json;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Apollo.NetCore.Util.Http
{
    public static class HttpUtil
    {
        private static string basicAuth = "Basic" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("user:"));
        private static HttpClient _httpClient = new HttpClient();

        public static async Task<HttpResponse<T>> Get<T>(HttpRequest httpRequest)
        {
            try
            {
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri(httpRequest.Url),
                    Method = HttpMethod.Get,
                };
                //request.Headers.Authorization = new AuthenticationHeaderValue(basicAuth);
                _httpClient.Timeout = TimeSpan.FromSeconds(httpRequest.Timeout ?? 60);
                var resp = await _httpClient.SendAsync(request);
                if (resp.StatusCode == HttpStatusCode.OK|| resp.StatusCode == HttpStatusCode.NotModified)
                {
                    var content = await resp.Content.ReadAsStringAsync();
                    T body = JsonConvert.DeserializeObject<T>(content);
                    return new HttpResponse<T>(resp.StatusCode, body);
                }
                throw new RemoteStatusCodeException(resp.StatusCode, string.Format("Get operation failed for {0}", httpRequest.Url));
            }
            catch (Exception ex)
            {
                throw new RemoteException("Could not complete get operation", ex);
            }
        }
    }
}
