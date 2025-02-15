using System;
using Microsoft.Xna.Framework;

namespace EvolutionSim.Core
{
    public class Retina(float[] activations)
    {
        public float[] Activations { get; private set; } = activations;

           public static Retina FromTargets(Vector2 source, float referenceHeading, float range, float worldWidth, float worldHeight, int numCones, params Vector2[] targets)
        {
            float[] retina = new float[numCones];
            for (int i = 0; i < numCones; i++)
            {
                retina[i] = 0f;
            }

            float coneAngleSize = MathHelper.TwoPi / numCones;
            foreach (var target in targets)
            {
                Vector2 diff = source.TorusDifference(target, worldWidth, worldHeight);
                float distance = diff.Length();
                if (distance > range)
                    continue; 

                float angle = (float)Math.Atan2(diff.Y, diff.X);
                float relativeAngle = MathHelper.WrapAngle(angle - referenceHeading);
                if (relativeAngle < 0)
                    relativeAngle += MathHelper.TwoPi;

                int coneIndex = (int)(relativeAngle / coneAngleSize) % numCones;
                float activation = 1f - MathHelper.Clamp(distance / range, 0f, 1f);
                retina[coneIndex] = Math.Max(retina[coneIndex], activation);
            }
            return new Retina(retina);
        }
    }
} 