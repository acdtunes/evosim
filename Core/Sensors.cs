using System;

namespace EvolutionSim.Core;

public record Sensors(
    float[] PlantRetina,
    float[] NonParasiteCreatureRetina,
    float[] ParasiteCreatureRetina,
    float Energy)
{
    public float[] ToArray()
    {
        int totalLength = PlantRetina.Length + NonParasiteCreatureRetina.Length + ParasiteCreatureRetina.Length + 1;
        var input = new float[totalLength];
        Array.Copy(PlantRetina, 0, input, 0, PlantRetina.Length);
        Array.Copy(NonParasiteCreatureRetina, 0, input, PlantRetina.Length, NonParasiteCreatureRetina.Length);
        Array.Copy(ParasiteCreatureRetina, 0, input, PlantRetina.Length + NonParasiteCreatureRetina.Length, ParasiteCreatureRetina.Length);
        input[input.Length - 1] = Energy;
        return input;
    }
}