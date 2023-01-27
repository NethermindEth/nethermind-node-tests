using NLog;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;

namespace NethermindNode.Core.Helpers
{
    public static class HttpExecutor
    {
        public async static Task<Tuple<string, TimeSpan, bool>> ExecuteNethermindJsonRpcCommand(string command, string parameters, string url, Logger logger)
        {
            var data = new StringContent($"{{\"method\":\"{command}\",\"params\":[{parameters}],\"id\":1,\"jsonrpc\":\"2.0\"}}", Encoding.UTF8, "application/json");
            var response = await PostHttpWithTimingInfo(url, data, logger);

            return response;
        }

        public async static Task<Tuple<string, TimeSpan, bool>> ExecuteBatchedNethermindJsonRpcCommand(string command, List<string> parameters, string url, Logger logger)
        {
            //generate content
            List<string> content = new List<string>();
            int i = 1;
            foreach (var item in parameters)
            {
                content.Add($"{{\"method\":\"{command}\",\"params\":[{item}],\"id\":{i},\"jsonrpc\":\"2.0\"}}");
                i++;
            }
            string contentString = "[" + content.Aggregate((x, y) => x + "," + y) + "]";

            StringContent dataList = new StringContent(contentString, Encoding.UTF8, "application/json");
            var response = await PostHttpWithTimingInfo(url, dataList, logger);

            return response;
        }

        private async static Task<Tuple<string, TimeSpan, bool>> PostHttpWithTimingInfo(string url, StringContent? data, Logger logger)
        {
            var stopWatch = Stopwatch.StartNew();
            using (var client = new HttpClient())
            {
                bool isSuccess = false;
                string responseString = "";
                HttpResponseMessage result = new HttpResponseMessage();

                result = await TryPostAsync(url, data, logger);

                if (result != null && result.IsSuccessStatusCode)
                {
                    isSuccess = true;
                    var responseContent = result.Content;

                    // by calling .Result you are synchronously reading the result
                    responseString = responseContent.ReadAsStringAsync().Result;
                }
                return new Tuple<string, TimeSpan, bool>(responseString, stopWatch.Elapsed, isSuccess);
            }
        }

        private async static Task<HttpResponseMessage?> TryPostAsync(string url, StringContent? data, Logger logger)
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

                if (e.InnerException is SocketException &&
                    (
                        e.InnerException.Message.Contains("Connection refused") ||
                        e.InnerException.Message.Contains("Network is unreachable") ||
                        e.InnerException.Message.Contains("Cannot assign requested address")
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
                throw e;
            }
        }
    }
}
