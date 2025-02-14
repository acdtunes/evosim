namespace EvolutionSim.Core;

public record JetForces(
    float Front,
    float Back,
    float TopRight,
    float TopLeft,
    float BottomRight,
    float BottomLeft)
{
    public float[] ToArray()
    {
        return [Front, Back, TopRight, TopLeft, BottomRight, BottomLeft];
    }
}