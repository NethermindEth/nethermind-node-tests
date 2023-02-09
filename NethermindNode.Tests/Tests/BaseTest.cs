using Microsoft.Extensions.Configuration;
using NethermindNode.Tests.Helpers;

namespace NethermindNode.Tests;

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
