using System.IO;
using EvolutionSim.Core;
using YamlDotNet.Serialization;

namespace EvolutionSim;

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