using System;
using Microsoft.Xna.Framework;

namespace EvolutionSim.Core;

public static class PlantClusterSpawner
{
    public static void SpawnCluster(Simulation simulation, Random random)
    {
        var parameters = simulation.Parameters;
        var clusterCenter = new Vector2(
            random.Next(parameters.World.WorldWidth),
            random.Next(parameters.World.WorldHeight)
        );

        var plantCount = random.Next(1, parameters.Population.MaxPlantsPerCluster + 1);
        var clusterRadius = parameters.Population.InitialPlantClusterRadius;

        for (var i = 0; i < plantCount; i++)
        {
            var angle = (float)(random.NextDouble() * MathHelper.TwoPi);
            var distance = (float)(random.NextDouble() * clusterRadius);
            var offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * distance;
            var pos = clusterCenter + offset;
            pos.X = (pos.X % parameters.World.WorldWidth + parameters.World.WorldWidth) % parameters.World.WorldWidth;
            pos.Y = (pos.Y % parameters.World.WorldHeight + parameters.World.WorldHeight) %
                    parameters.World.WorldHeight;
            var plant = new Plant(pos, random, simulation);
            simulation.AddPlant(plant);
        }
    }
}