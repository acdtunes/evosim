using System;

namespace EvolutionSim.Core;

public record Sensors(
    float PlantNormalizedDistance,
    float PlantAngleSin,
    float PlantAngleCos,
    float CreatureNormalizedDistance,
    float CreatureAngleSin,
    float CreatureAngleCos,
    float Energy)
{
    public float[] ToArray()
    {
        return new float[]
        {
            PlantNormalizedDistance,
            PlantAngleSin,
            PlantAngleCos,
            CreatureNormalizedDistance,
            CreatureAngleSin,
            CreatureAngleCos,
            Energy
        };
    }
}