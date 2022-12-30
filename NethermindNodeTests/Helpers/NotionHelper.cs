using Notion.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NethermindNodeTests.Helpers
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

        public void AddRecord(PagesCreateParameters recordToAdd)
        {
            var result = _client.Pages.CreateAsync(recordToAdd).Result;
        }
    }
}
