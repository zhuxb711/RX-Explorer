using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    internal static class BackendHelper
    {
        private static JsonSerializerOptions SerializerOptions { get; } = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public static async Task<ValidateRecieptResponseDto> ValidateRecieptAsync(string AccountName, string ReceiptXml)
        {
            try
            {
                HttpWebRequest Request = WebRequest.CreateHttp("http://52.230.36.100:3303/validation/validateReceipt");
                Request.ReadWriteTimeout = 60000;
                Request.Timeout = 60000;
                Request.Method = "POST";
                Request.ContentType = "application/json";
                Request.UserAgent = "RX-Explorer (UWP)";

                using (Stream WriteStream = await Request.GetRequestStreamAsync())
                using (StreamWriter Writer = new StreamWriter(WriteStream, Encoding.UTF8, 1024, true))
                {
                    await Writer.WriteAsync(JsonSerializer.Serialize(new ValidateRecieptRequestDto
                    {
                        userId = AccountName,
                        receipt = ReceiptXml
                    }));

                    await Writer.FlushAsync();
                }

                using (HttpWebResponse Response = (HttpWebResponse)await Request.GetResponseAsync())
                using (StreamReader Reader = new StreamReader(Response.GetResponseStream(), Encoding.GetEncoding(Response.CharacterSet)))
                {
                    return JsonSerializer.Deserialize<ValidateRecieptResponseDto>(await Reader.ReadToEndAsync(), SerializerOptions);
                }
            }
            catch (WebException ex)
            {
                if (ex.Response is HttpWebResponse Response)
                {
                    using (StreamReader Reader = new StreamReader(Response.GetResponseStream(), Encoding.UTF8))
                    {
                        ResponseBase ErrorResponse = JsonSerializer.Deserialize<ResponseBase>(await Reader.ReadToEndAsync(), new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        throw new Exception(ErrorResponse.ErrorMessage);
                    }
                }

                throw;
            }
        }

        public static async Task<RedeemVisibilityStatusResponse> CheckRedeemVisibilityStatusAsync()
        {
            try
            {
                HttpWebRequest Request = WebRequest.CreateHttp("http://52.230.36.100:3303/switch/checkRedeemVisibilityStatus");
                Request.ReadWriteTimeout = 60000;
                Request.Timeout = 60000;
                Request.Method = "GET";
                Request.ContentType = "application/json";
                Request.UserAgent = "RX-Explorer (UWP)";

                using (HttpWebResponse Response = (HttpWebResponse)await Request.GetResponseAsync())
                using (StreamReader Reader = new StreamReader(Response.GetResponseStream(), Encoding.GetEncoding(Response.CharacterSet)))
                {
                    return JsonSerializer.Deserialize<RedeemVisibilityStatusResponse>(await Reader.ReadToEndAsync(), SerializerOptions);
                }
            }
            catch (WebException ex)
            {
                if (ex.Response is HttpWebResponse Response)
                {
                    using (StreamReader Reader = new StreamReader(Response.GetResponseStream(), Encoding.UTF8))
                    {
                        ResponseBase ErrorResponse = JsonSerializer.Deserialize<ResponseBase>(await Reader.ReadToEndAsync(), new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        throw new Exception(ErrorResponse.ErrorMessage);
                    }
                }

                throw;
            }
        }
    }
}
