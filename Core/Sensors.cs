namespace EvolutionSim.Core;

public record Sensors(
    VisionSensor PlantVisionSensor,
    VisionSensor CreatureVisionSensor,
    float Energy)
{
    public float[] ToArray()
    {
        return new[]
        {
            PlantVisionSensor.NormalizedDistance,
            PlantVisionSensor.NormalizedAngleSin,
            PlantVisionSensor.NormalizedAngleCos,
            CreatureVisionSensor.NormalizedDistance,
            CreatureVisionSensor.NormalizedAngleSin,
            CreatureVisionSensor.NormalizedAngleCos,
            Energy
        };
    }
}