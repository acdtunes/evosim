using MessagePack;

namespace EvolutionSim.Core
{
    [MessagePackObject(keyAsPropertyName: true)]
    public class JetForces
    {
        public JetForces(float back, float frontRight, float frontLeft)
        {
            Back = back;
            FrontRight = frontRight;
            FrontLeft = frontLeft;
        }

        public float Back { get; set; }
        public float FrontRight { get; set; }
        public float FrontLeft { get; set; }

        public float[] ToArray()
        {
            return new[] { Back, FrontRight, FrontLeft };
        }
    }
}