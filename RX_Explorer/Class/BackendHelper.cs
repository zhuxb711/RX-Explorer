using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    internal static class BackendHelper
    {
        public static async Task<RetrieveAADTokenContentResponseDto> RetrieveAADTokenAsync()
        {
            try
            {
                HttpWebRequest Request = WebRequest.CreateHttp("http://52.230.36.100:3303/validation/retrieveAADToken");
                Request.ReadWriteTimeout = 60000;
                Request.Timeout = 60000;
                Request.Method = "GET";
                Request.ContentType = "application/json";
                Request.UserAgent = "RX-Explorer (UWP)";

                using (HttpWebResponse Response = (HttpWebResponse)await Request.GetResponseAsync())
                using (StreamReader Reader = new StreamReader(Response.GetResponseStream(), Encoding.GetEncoding(Response.CharacterSet)))
                {
                    RetrieveAADTokenResponseDto ResponseDto = JsonConvert.DeserializeObject<RetrieveAADTokenResponseDto>(await Reader.ReadToEndAsync());

                    if (ResponseDto.StatusCode is not 200 and not 201)
                    {
                        throw new Exception(ResponseDto.ErrorMessage);
                    }

                    return ResponseDto.Content;
                }
            }
            catch (WebException ex)
            {
                if (ex.Response is HttpWebResponse Response)
                {
                    using (StreamReader Reader = new StreamReader(Response.GetResponseStream(), Encoding.UTF8))
                    {
                        throw new Exception(JsonConvert.DeserializeObject<ResponseBase>(await Reader.ReadToEndAsync()).ErrorMessage);
                    }
                }

                throw;
            }
        }

        public static async Task<RedeemCodeContentResponseDto> RedeemCodeAsync(string CustomerCollectionId)
        {
            try
            {
                HttpWebRequest Request = WebRequest.CreateHttp("http://52.230.36.100:3303/validation/redeemCode");
                Request.ReadWriteTimeout = 60000;
                Request.Timeout = 60000;
                Request.Method = "PUT";
                Request.ContentType = "application/json";
                Request.UserAgent = "RX-Explorer (UWP)";

                using (Stream WriteStream = await Request.GetRequestStreamAsync())
                using (StreamWriter Writer = new StreamWriter(WriteStream, Encoding.UTF8, 1024, true))
                {
                    await Writer.WriteAsync(JsonConvert.SerializeObject(new RedeemCodeRequestDto
                    {
                        customerCollectionId = CustomerCollectionId,
                    }));

                    await Writer.FlushAsync();
                }

                using (HttpWebResponse Response = (HttpWebResponse)await Request.GetResponseAsync())
                using (StreamReader Reader = new StreamReader(Response.GetResponseStream(), Encoding.GetEncoding(Response.CharacterSet)))
                {
                    RedeemCodeResponseDto ResponseDto = JsonConvert.DeserializeObject<RedeemCodeResponseDto>(await Reader.ReadToEndAsync());

                    if (ResponseDto.StatusCode is not 200 and not 201)
                    {
                        throw new Exception(ResponseDto.ErrorMessage);
                    }

                    return ResponseDto.Content;
                }
            }
            catch (WebException ex)
            {
                if (ex.Response is HttpWebResponse Response)
                {
                    using (StreamReader Reader = new StreamReader(Response.GetResponseStream(), Encoding.UTF8))
                    {
                        throw new Exception(JsonConvert.DeserializeObject<ResponseBase>(await Reader.ReadToEndAsync()).ErrorMessage);
                    }
                }

                throw;
            }
        }

        public static async Task<RedeemVisibilityStatusResponse> CheckRedeemVisibilityStatusAsync()
        {
            try
            {
                HttpWebRequest Request = WebRequest.CreateHttp("http://52.230.36.100:3303/switch/retrieveSwitch?switchName=redeemVisibility");
                Request.ReadWriteTimeout = 60000;
                Request.Timeout = 60000;
                Request.Method = "GET";
                Request.ContentType = "application/json";
                Request.UserAgent = "RX-Explorer (UWP)";

                using (HttpWebResponse Response = (HttpWebResponse)await Request.GetResponseAsync())
                using (StreamReader Reader = new StreamReader(Response.GetResponseStream(), Encoding.GetEncoding(Response.CharacterSet)))
                {
                    return JsonConvert.DeserializeObject<RedeemVisibilityStatusResponse>(await Reader.ReadToEndAsync());
                }
            }
            catch (WebException ex)
            {
                if (ex.Response is HttpWebResponse Response)
                {
                    using (StreamReader Reader = new StreamReader(Response.GetResponseStream(), Encoding.UTF8))
                    {
                        throw new Exception(JsonConvert.DeserializeObject<ResponseBase>(await Reader.ReadToEndAsync()).ErrorMessage);
                    }
                }

                throw;
            }
        }
    }
}
