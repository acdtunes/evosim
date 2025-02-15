using System.IO;
using YamlDotNet.Serialization;

namespace EvolutionSim.Configuration;

public static class SimulationConfigParser
{
    public static SimulationParameters Parse(string filePath)
    {
        var deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .Build();

        var yamlContent = File.ReadAllText(filePath);
        return deserializer.Deserialize<SimulationParameters>(yamlContent);
    }
}