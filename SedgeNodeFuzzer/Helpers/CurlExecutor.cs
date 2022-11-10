using NLog;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;

namespace SedgeNodeFuzzer.Helpers
{
    public static class CurlExecutor
    {
        public async static Task<string?> ExecuteCommand(string command, string url, NLog.Logger logger)
        {
            if (logger.IsTraceEnabled)
                logger.Trace("Executing command: " + command);
            var data = new StringContent($"{{\"method\":\"{command}\",\"params\":[],\"id\":1,\"jsonrpc\":\"2.0\"}}", Encoding.UTF8, "application/json");
            var response = await TryPostAsync(url, data, logger);

            return response?.Content.ReadAsStringAsync().Result;
        }

        private async static Task<HttpResponseMessage?> TryPostAsync(string url, StringContent? data, NLog.Logger logger)
        {
            var client = new HttpClient();
            HttpResponseMessage response;
            try
            {
                response = await client.PostAsync(url, data);
                return response;
            }
            //TODO: some better way to catch those exceptions which are result of not started node (we want to have tests that are able to wait properly until node is deployed)
            catch (HttpRequestException e)
            {
                if (e.InnerException is IOException &&
                        (
                        e.InnerException.Message.Contains("Connection reset by peer") ||
                        e.InnerException.Message.Contains("premature")
                        )
                    )
                {
                    if (logger.IsTraceEnabled)
                    {
                        logger.Trace(e.Message);
                        logger.Trace(e.StackTrace);
                    }
                    return null;
                }

                if (e.InnerException is SocketException && e.InnerException.Message.Contains("Connection refused"))
                {
                    if (logger.IsTraceEnabled)
                    {
                        logger.Trace(e.Message);
                        logger.Trace(e.StackTrace);
                    }
                    return null;
                }
                throw e;
            }
        }
    }
}
