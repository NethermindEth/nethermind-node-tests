using NLog;
using System.Text;

namespace SedgeNodeFuzzer.Helpers
{
    public static class CurlExecutor
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public async static Task<HttpResponseMessage> ExecuteCommand(string command, string url)
        {
            var client = new HttpClient();
            if (Logger.IsTraceEnabled)
                Logger.Trace("Executing command: " + command);
            var data = new StringContent($"{{\"method\":\"{command}\",\"params\":[],\"id\":1,\"jsonrpc\":\"2.0\"}}", Encoding.UTF8, "application/json");
            var response = await client.PostAsync(url, data);

            return response;
        }
    }
}
