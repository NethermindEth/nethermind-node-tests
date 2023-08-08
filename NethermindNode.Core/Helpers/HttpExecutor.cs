using NLog;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;

namespace NethermindNode.Core.Helpers;

public static class HttpExecutor
{
    public async static Task<string> ExecuteNethermindJsonRpcCommandAsync(string command, string parameters, string id, string url, Logger logger)
    {
        var data = new StringContent($"{{\"method\":\"{command}\",\"params\":[{parameters}],\"id\":{id},\"jsonrpc\":\"2.0\"}}", Encoding.UTF8, "application/json");
        using (var client = new HttpClient())
        {
            var result = await client.PostAsync(url, data);
            if (!result.IsSuccessStatusCode)
            {
                return $"Error {result.StatusCode}: {await result.Content.ReadAsStringAsync()}";
            }

            return await result.Content.ReadAsStringAsync();
        }
    }

    public async static Task<string> ExecuteBatchedNethermindJsonRpcCommandAsync(string command, List<string> parameters, string url, Logger logger)
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
        using (var client = new HttpClient())
        {
            var result = await client.PostAsync(url, dataList);
            if (!result.IsSuccessStatusCode)
            {
                return $"Error {result.StatusCode}: {await result.Content.ReadAsStringAsync()}";
            }

            return await result.Content.ReadAsStringAsync();
        }
    }

    public async static Task<Tuple<string, TimeSpan, bool>> ExecuteNethermindJsonRpcCommandWithTimingInfo(string command, string parameters, string id, string url, Logger logger)
    {
        var data = new StringContent($"{{\"method\":\"{command}\",\"params\":[{parameters}],\"id\":{id},\"jsonrpc\":\"2.0\"}}", Encoding.UTF8, "application/json");
        var response = await PostHttpWithTimingInfo(url, data, logger);

        return response;
    }

    public async static Task<Tuple<string, TimeSpan, bool>> ExecuteBatchedNethermindJsonRpcCommandWithTimingInfo(string command, List<string> parameters, string url, Logger logger)
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

            result = await client.PostAsync(url, data);

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
}
