using Notion.Client;

namespace NethermindNode.Tests.Helpers
{
    public class NotionHelper
    {
        private NotionClient _client;
        public NotionHelper()
        {
            _client = NotionClientFactory.Create(new ClientOptions
            {
                AuthToken = ConfigurationHelper.Configuration["AuthToken"]
            });
        }

        public void AddRecord( PagesCreateParameters recordToAdd)
        {
            var result = _client.Pages.CreateAsync(recordToAdd).Result;
        }

        public void AddRecord(string databaseId, DatabasesUpdateParameters recordToAdd)
        {
            _client.Databases.UpdateAsync(databaseId, recordToAdd);
        }

        public Task<PaginatedList<Page>> GetRecords(string databaseId)
        {
            var queryParams = new DatabasesQueryParameters();
            return _client.Databases.QueryAsync(databaseId, queryParams);
        }
    }
}
