using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace NethermindNode.Core.Helpers
{
    public static class DockerComposeHelper
    {
        public static Dictionary<string, object> ReadDockerCompose(string filePath)
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            using var reader = new StreamReader(filePath);
            var yaml = reader.ReadToEnd();
            var result = deserializer.Deserialize<Dictionary<string, object>>(yaml);

            return result;
        }

        public static void WriteDockerCompose(Dictionary<string, object> compose, string filePath)
        {
            var serializer = new SerializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            var yaml = serializer.Serialize(compose);
            using var writer = new StreamWriter(filePath);
            writer.Write(yaml);
        }

        public static void RemoveCommandFlag(Dictionary<string, object> dockerCompose, string serviceName, string flagToRemove)
        {
            var services = (Dictionary<object, object>)dockerCompose["services"];
            if (services.ContainsKey(serviceName))
            {
                var service = (Dictionary<object, object>)services[serviceName];
                var command = (List<object>)service["command"];
                command.Remove(flagToRemove);
            }
        }
    }
}
