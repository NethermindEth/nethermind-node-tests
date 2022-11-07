using NLog;
using System.Text;

namespace SedgeNodeFuzzer.Helpers
{
    public static class CurlExecutor
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public async static Task<string?> ExecuteCommand(string command, string url)
        {
            if (Logger.IsTraceEnabled)
                Logger.Trace("Executing command: " + command);
            var data = new StringContent($"{{\"method\":\"{command}\",\"params\":[],\"id\":1,\"jsonrpc\":\"2.0\"}}", Encoding.UTF8, "application/json");
            var response = await TryPostAsync(url, data);
                     
            return response?.Content.ReadAsStringAsync().Result;
        }

        private async static Task<HttpResponseMessage?> TryPostAsync(string url, StringContent? data)
        {
            var client = new HttpClient();
            HttpResponseMessage response;
            try
            {
                response = await client.PostAsync(url, data);
                return response;
            }
            catch (AggregateException e)
            {
                if (e.InnerException is HttpRequestException)
                {
                    if (e.InnerException is IOException)
                    {
                        Logger.Error(e.Message);
                        Logger.Error(e.StackTrace);
                        return null;
                    }
                }
                throw e;
            }
        }
    }
}
