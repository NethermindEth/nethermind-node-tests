using Microsoft.Extensions.Configuration;

namespace NethermindNode.Core.Helpers
{
    public sealed class ConfigurationHelper
    {
        private static readonly Lazy<ConfigurationHelper> lazyInstance =
            new Lazy<ConfigurationHelper>(() => new ConfigurationHelper());

        public static ConfigurationHelper Instance => lazyInstance.Value;

        private IConfiguration Configuration { get; }

        private ConfigurationHelper()
        {

            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config.json");

            Configuration = builder.Build();
        }

        public string this[string setting]
        {
            get { return Configuration[setting]; }
        }
    }
}
