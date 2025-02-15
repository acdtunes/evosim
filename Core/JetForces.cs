namespace EvolutionSim.Core;

public record JetForces(
    float Back,
    float FrontRight,
    float FrontLeft)
{
    public float[] ToArray()
    {
        return [Back, FrontRight, FrontLeft];
    }
}