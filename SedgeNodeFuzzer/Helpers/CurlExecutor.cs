using System.Text;

namespace SedgeNodeFuzzer.Helpers
{
    public static class CurlExecutor
    {
        public async static Task<HttpResponseMessage> ExecuteCommand(string command, string url)
        {
            var client = new HttpClient();

            var data = new StringContent("{\"method\":\"eth_syncing\",\"params\":[],\"id\":1,\"jsonrpc\":\"2.0\"}", Encoding.UTF8, "application/json");
            var response = await client.PostAsync(url, data);

            return response;
        }
    }
}
