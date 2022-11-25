using Microsoft.Extensions.Configuration;
using NethermindNodeTests.Helpers;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NethermindNodeTests.Tests
{
    public class BaseTest
    {

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            ConfigurationHelper.Configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .AddUserSecrets<BaseTest>(true)
                .Build();
        }
    }
}
