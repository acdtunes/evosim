namespace EvolutionSim.Core;

public record Sensors(
    float PlantNormalizedDistance,
    float PlantAngleSin,
    float PlantAngleCos,
    float CreatureNormalizedDistance,
    float CreatureAngleSin,
    float CreatureAngleCos,
    float Hunger);
