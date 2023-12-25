using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Vanara.Extensions;

namespace AuxiliaryTrustProcess.Class
{
    internal static class BackendUtil
    {
        private static readonly string CARootCertificatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Ssl", "RX-Explorer Certificate Authority Root.cer");
        private static readonly string ClientCertificatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Ssl", "RX-Explorer Client Certificate.pfx");

        public static async Task<T> SendAndGetResponseAsync<T>(Uri APIEndPoint, HttpMethod Method, HttpContent Content = null, CancellationToken CancelToken = default)
        {
            using (X509Certificate2 CARootCertificate = new X509Certificate2(CARootCertificatePath))
            using (SecureString ClientPassword = "<RX-Explorer-Client-Certificate-Secret-Value>".ToSecureString())
            using (X509Certificate2 ClientCertificate = new X509Certificate2(ClientCertificatePath, ClientPassword))
            {
                if (!CARootCertificate.Thumbprint.Equals("6214E899BDD239EF372330D69F56ABE276ED7F21", StringComparison.OrdinalIgnoreCase)
                    || !ClientCertificate.Thumbprint.Equals("341EDDE937BDF6EAB593AA7F31496B2209F7EC92", StringComparison.OrdinalIgnoreCase))
                {
                    throw new UnauthorizedAccessException("Certificate is not correct and could not be trusted");
                }

                X509ChainPolicy ChainPolicy = new X509ChainPolicy
                {
                    DisableCertificateDownloads = true,
                    TrustMode = X509ChainTrustMode.CustomRootTrust,
                    RevocationMode = X509RevocationMode.NoCheck,
                };

                ChainPolicy.CustomTrustStore.Clear();
                ChainPolicy.CustomTrustStore.Add(CARootCertificate);

                SslClientAuthenticationOptions SslOptions = new SslClientAuthenticationOptions
                {
                    AllowRenegotiation = true,
                    EncryptionPolicy = EncryptionPolicy.RequireEncryption,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                    ClientCertificates = new X509CertificateCollection(new X509Certificate[] { ClientCertificate }),
                    CertificateChainPolicy = ChainPolicy
                };

                SslOptions.RemoteCertificateValidationCallback += (Sender, Certificate, Chain, SslPolicyErrors) =>
                {
                    if (SslPolicyErrors == SslPolicyErrors.None)
                    {
                        if (string.Equals((Certificate as X509Certificate2)?.Thumbprint, "307403D75FE7E9D5969F85E309C98DFD56D32E87", StringComparison.OrdinalIgnoreCase))
                        {
                            string CertificateSubjectHost = Certificate.Subject.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                                                                               .Select((Element) => Regex.Match(Element, @"^CN=(?<HostName>[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3})$"))
                                                                               .Where((Mat) => (Mat?.Success).GetValueOrDefault())
                                                                               .Select((Mat) => Mat.Groups["HostName"].Value)
                                                                               .SingleOrDefault();

                            if (string.Equals((Sender as SslStream)?.TargetHostName ?? APIEndPoint.Host, CertificateSubjectHost, StringComparison.Ordinal))
                            {
                                return true;
                            }
                            else
                            {
                                LogTracer.Log($"Connection to \"{APIEndPoint.AbsoluteUri}\" was rejected due to the subject of remote certificate is not match with the host name");
                            }
                        }
                        else
                        {
                            LogTracer.Log($"Connection to \"{APIEndPoint.AbsoluteUri}\" was rejected due to remote certificate is not correct and could not be trusted");
                        }
                    }
                    else
                    {
                        LogTracer.Log($"Connection to \"{APIEndPoint.AbsoluteUri}\" was rejected due to SSL policy conflict: {SslPolicyErrors}");
                    }

                    return false;
                };

                SocketsHttpHandler HttpHandler = new SocketsHttpHandler
                {
                    SslOptions = SslOptions,
                    ConnectTimeout = TimeSpan.FromSeconds(30)
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
}
