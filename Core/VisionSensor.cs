using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace EvolutionSim.Core;

public class VisionSensor
{
    private VisionSensor(float normalizedDistance, float normalizedAngleSin, float normalizedAngleCos)
    {
        NormalizedDistance = normalizedDistance;
        NormalizedAngleSin = normalizedAngleSin;
        NormalizedAngleCos = normalizedAngleCos;
    }

    public float NormalizedDistance { get; }
    public float NormalizedAngleSin { get; }
    public float NormalizedAngleCos { get; }

    public static VisionSensor FromTargets(Vector2 source, float referenceHeading, float normalizationFactor, float worldWidth, float worldHeight, params Vector2[] targets)
    {
        switch (targets.Length)
        {
            case 0:
                return new VisionSensor(1, 0, 0);
            case 1:
            {
                var toTarget = source.TorusDifference(targets[0], worldWidth, worldHeight);
                var distance = toTarget.Length();
                var normalizedDistance = MathHelper.Clamp(distance / normalizationFactor, 0, 1);
                var targetAngle = (float)Math.Atan2(toTarget.Y, toTarget.X);
                var angleDiff = MathHelper.WrapAngle(targetAngle - referenceHeading);
                return new VisionSensor(normalizedDistance, (float)Math.Sin(angleDiff), (float)Math.Cos(angleDiff));
            }
        }

        var validTargets = targets.Where(t => source.TorusDistance(t, worldWidth, worldHeight) <= normalizationFactor).ToArray();
        if (validTargets.Length == 0)
            return new VisionSensor(1, 0, 0);

        var avgOffset = validTargets.Aggregate(Vector2.Zero, (current, t) => current + source.TorusDifference(t, worldWidth, worldHeight));
       
        avgOffset /= validTargets.Length;
        var dist = avgOffset.Length();
        var normDist = MathHelper.Clamp(dist / normalizationFactor, 0, 1);
        var avgTargetAngle = (float)Math.Atan2(avgOffset.Y, avgOffset.X);
        var avgAngleDiff = MathHelper.WrapAngle(avgTargetAngle - referenceHeading);
        
        return new VisionSensor(normDist, (float)Math.Sin(avgAngleDiff), (float)Math.Cos(avgAngleDiff));
    }
}