using Apollo.NetCore.Exceptions;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Apollo.NetCore.Util.Http
{
    public static class HttpUtil
    {
        private static string basicAuth = "Basic" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("user:"));
        private static readonly ConcurrentDictionary<string, HttpClient> _httpClients = new ConcurrentDictionary<string, HttpClient>();
        private static HttpClient _httpClient = new HttpClient();

        public static async Task<HttpResponse<T>> Get<T>(HttpRequest httpRequest)
        {
            HttpResponseMessage response = null;
            try
            {
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri(httpRequest.Url),
                    Method = HttpMethod.Get,
                };
                //request.Headers.Authorization = new AuthenticationHeaderValue(basicAuth);

                var timeout = TimeSpan.FromSeconds(httpRequest.Timeout ?? 60);
                using (var cts = new CancellationTokenSource(timeout))
                {
                    var httpClient = _httpClients.GetOrAdd(request.RequestUri.Host, new HttpClient { Timeout = timeout });

                    response = await Timeout(httpClient.SendAsync(request, cts.Token), (int)timeout.TotalMilliseconds, cts.Token);
                }

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    using (var s = await response.Content.ReadAsStreamAsync())
                    using (var sr = new StreamReader(s, Encoding.UTF8))
                    using (var jtr = new JsonTextReader(sr))
                        return new HttpResponse<T>(response.StatusCode, JsonSerializer.Create().Deserialize<T>(jtr));
                }

                if (response.StatusCode == HttpStatusCode.NotModified)
                {
                    return new HttpResponse<T>(response.StatusCode);
                }
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    T body = JsonConvert.DeserializeObject<T>(content);
                    return new HttpResponse<T>(response.StatusCode, body);
                }
                throw new RemoteStatusCodeException(response.StatusCode, string.Format("Get operation failed for {0}", httpRequest.Url));
            }
            catch (Exception ex)
            {
                throw new RemoteException("Could not complete get operation", ex);
            }
        }

        private static async Task<T> Timeout<T>(Task<T> task, int millisecondsDelay, CancellationToken token)
        {
            if (await Task.WhenAny(task, Task.Delay(millisecondsDelay, token)) == task)
                return task.Result;

            throw new TimeoutException();
        }
    }
}
