using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AuxiliaryTrustProcess.Class
{
    internal static class BackendUtil
    {
        public static async Task<T> SendAndGetResponseAsync<T>(Uri APIEndPoint, HttpMethod Method, HttpContent Content = null, CancellationToken CancelToken = default)
        {
            using (X509Certificate2 CASubCertificate = new X509Certificate2(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Ssl", "RX-Explorer Digital Secure CA.cer")))
            using (X509Certificate2 CARootCertificate = new X509Certificate2(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Ssl", "RX-Explorer Certificate Authority Root.cer")))
            using (X509Certificate2 ClientCertificate = new X509Certificate2(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Ssl", "RX-Explorer Client Certificate.pfx"), "<RX-Explorer-Client-Certificate-Secret-Value>"))
            {
                X509ChainPolicy ChainPolicy = new X509ChainPolicy
                {
                    TrustMode = X509ChainTrustMode.CustomRootTrust,
                    RevocationMode = X509RevocationMode.NoCheck,
                    VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority
                };

                ChainPolicy.CustomTrustStore.Add(CASubCertificate);
                ChainPolicy.CustomTrustStore.Add(CARootCertificate);

                SslClientAuthenticationOptions SslOptions = new SslClientAuthenticationOptions
                {
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                    EncryptionPolicy = EncryptionPolicy.RequireEncryption,
                    ClientCertificates = new X509CertificateCollection(new X509Certificate[] { ClientCertificate }),
                    CertificateChainPolicy = ChainPolicy
                };

                SslOptions.RemoteCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) =>
                {
                    if (certificate is X509Certificate2 ServerCert)
                    {
                        if (chain.Build(ServerCert))
                        {
                            return (sender as SslStream)?.TargetHostName == ServerCert.Subject.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                                                                                              .Select((Element) => Regex.Match(Element, @"^CN=(?<HostName>[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3})$"))
                                                                                              .Where((Mat) => (Mat?.Success).GetValueOrDefault())
                                                                                              .Select((Mat) => Mat.Groups["HostName"].Value)
                                                                                              .SingleOrDefault();
                        }
                    }

                    return false;
                };

                SocketsHttpHandler HttpHandler = new SocketsHttpHandler
                {
                    SslOptions = SslOptions,
                    ConnectTimeout = TimeSpan.FromSeconds(30)
                };

                using (HttpClient Client = new HttpClient(HttpHandler)
                {
                    Timeout = Timeout.InfiniteTimeSpan
                })
                {
                    HttpRequestMessage Request = new HttpRequestMessage()
                    {
                        Method = Method,
                        RequestUri = APIEndPoint,
                        Content = Content
                    };
                    Request.Headers.UserAgent.ParseAdd("RX-Explorer (UWP)");

                    HttpResponseMessage Response = await Client.SendAsync(Request, CancelToken);

                    string ResponseRawJsonString = await Response.Content.ReadAsStringAsync(CancelToken);

                    if (!string.IsNullOrEmpty(ResponseRawJsonString))
                    {
                        JsonSerializerOptions JsonOptions = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true,
                            TypeInfoResolver = JsonSourceGenerationContext.Default
                        };

                        if (Response.IsSuccessStatusCode)
                        {
                            return JsonSerializer.Deserialize<BackendResponseBaseData<T>>(ResponseRawJsonString, JsonOptions).Content;
                        }
                        else
                        {
                            throw new Exception(JsonSerializer.Deserialize<BackendResponseBaseData>(ResponseRawJsonString, JsonOptions).ErrorMessage);
                        }
                    }

                    throw new Exception("Backend replied an unexpected response");
                }
            }
        }
    }
}
