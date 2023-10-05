using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            string configPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), @"../../../nethermind-node-tests"));

            var builder = new ConfigurationBuilder()
                .SetBasePath(configPath)
                .AddJsonFile("config.json");

            Configuration = builder.Build();
        }

        public string this[string setting]
        {
            get { return Configuration[setting]; }
        }
    }
}
