using System;
using Microsoft.Xna.Framework;

namespace EvolutionSim.Core;

public static class Vector2Extensions
{
    public static float Cross(this Vector2 a, Vector2 b)
    {
        return a.X * b.Y - a.Y * b.X;
    }

    public static float TorusDistance(this Vector2 a, Vector2 b, float worldWidth, float worldHeight)
    {
        var dx = Math.Abs(a.X - b.X);
        var dy = Math.Abs(a.Y - b.Y);
        dx = Math.Min(dx, worldWidth - dx);
        dy = Math.Min(dy, worldHeight - dy);
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    public static Vector2 TorusDifference(this Vector2 a, Vector2 b, float worldWidth, float worldHeight)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        if (dx > worldWidth / 2)
            dx -= worldWidth;
        if (dx < -worldWidth / 2)
            dx += worldWidth;
        if (dy > worldHeight / 2)
            dy -= worldHeight;
        if (dy < -worldHeight / 2)
            dy += worldHeight;

        return new Vector2(dx, dy);
    }
}