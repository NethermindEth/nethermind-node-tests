using Notion.Client;

namespace NethermindNode.Core.Helpers;

public class NotionHelper
{
    private NotionClient _client;
    public NotionHelper(string token)
    {
        _client = NotionClientFactory.Create(new ClientOptions
        {
            AuthToken = token
        });
    }

    public void AddRecord(PagesCreateParameters recordToAdd)
    {
        var result = _client.Pages.CreateAsync(recordToAdd).Result;
    }
}
