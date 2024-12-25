using System;
using System.Net.Http;
using System.Net.Security;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AuxiliaryTrustProcess.Class
{
    internal static class BackendUtil
    {
        public static async Task<T> SendAndGetResponseAsync<T>(Uri APIEndPoint, HttpMethod Method, HttpContent Content = null, CancellationToken CancelToken = default)
        {
            SocketsHttpHandler HttpHandler = new SocketsHttpHandler
            {
                ConnectTimeout = TimeSpan.FromSeconds(60),
                SslOptions = new SslClientAuthenticationOptions
                {
                    TargetHost = "xz-home.brasilia.me"
                }
            };

            using (HttpClient Client = new HttpClient(HttpHandler))
            {
                Client.Timeout = Timeout.InfiniteTimeSpan;

                using (HttpRequestMessage Request = new HttpRequestMessage(Method, APIEndPoint))
                {
                    Request.Content = Content;
                    Request.Headers.UserAgent.ParseAdd("RX-Explorer (UWP)");

                    using (HttpResponseMessage Response = await Client.SendAsync(Request, CancelToken))
                    {
                        string ResponseRawJsonString = await Response.Content.ReadAsStringAsync(CancelToken);

                        if (!string.IsNullOrEmpty(ResponseRawJsonString))
                        {
                            JsonSourceGenerationContext CaseInsensitiveContext = new JsonSourceGenerationContext(new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });

                            if (Response.IsSuccessStatusCode)
                            {
                                return ((BackendResponseBaseData<T>)JsonSerializer.Deserialize(ResponseRawJsonString, typeof(BackendResponseBaseData<T>), CaseInsensitiveContext)).Content;
                            }
                            else
                            {
                                throw new Exception(JsonSerializer.Deserialize(ResponseRawJsonString, CaseInsensitiveContext.BackendResponseBaseData).FailureReason);
                            }
                        }

                        throw new Exception("Backend replied an unexpected empty response");
                    }
                }
            }
        }
    }
}
