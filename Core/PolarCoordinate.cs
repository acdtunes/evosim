using System;
using Microsoft.Xna.Framework;

namespace EvolutionSim.Core;

public readonly record struct PolarCoordinate(float Distance, float Angle)
{
     public float Sin => (float)Math.Sin(Angle);

     public float Cos => (float)Math.Cos(Angle);

     public Vector2 ToCartesian() => new Vector2(Cos * Distance, Sin * Distance);

     public static PolarCoordinate FromCartesian(Vector2 vector)
    {
        float distance = vector.Length();
        float angle = (float)Math.Atan2(vector.Y, vector.X);
        return new PolarCoordinate(distance, angle);
    }

    public override string ToString() => $"Distance: {Distance:F2}, Angle: {Angle:F2} rad";
} 