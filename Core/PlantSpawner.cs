using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace EvolutionSim.Core;

public class PlantSpawner(Simulation simulation, Random random)
{
    public void PopulateInitialPlants()
    {
        var clusters = simulation.Parameters.Population.InitialPlantClusters;
        for (var cluster = 0; cluster < clusters; cluster++)
        {
            var clusterCenter = GenerateRandomPosition(true, Vector2.Zero, 0);
            var plantCount = random.Next(1, simulation.Parameters.Population.MaxPlantsPerCluster + 1);
            for (var i = 0; i < plantCount; i++)
            {
                var pos = GenerateRandomPosition(
                    false,
                    clusterCenter,
                    simulation.Parameters.Population.InitialPlantClusterRadius);
                var plant = new Plant(pos, random, simulation);
                simulation.Plants.Add(plant.Id, plant);
            }
        }
    }

    public void DisperseSeeds(float dt)
    {
        foreach (var seed in from plant in new List<Plant>(simulation.Plants.Values)
                 where random.NextDouble() < simulation.Parameters.Plant.SeedDispersalProbability * dt
                 select GenerateRandomPosition(
                     false,
                     plant.Position,
                     simulation.Parameters.Plant.SeedDispersalRadius)
                 into seedPos
                 select new Plant(seedPos, random, simulation))
            simulation.AddPlant(seed);
    }

    private Vector2 GenerateRandomPosition(bool fullWorld, Vector2 center, float radius)
    {
        if (fullWorld)
            return new Vector2(
                random.Next(simulation.Parameters.World.WorldWidth),
                random.Next(simulation.Parameters.World.WorldHeight)
            );

        var angle = (float)(random.NextDouble() * MathHelper.TwoPi);
        var distance = (float)(random.NextDouble() * radius);
        var offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * distance;
        var pos = center + offset;

        pos.X = (pos.X % simulation.Parameters.World.WorldWidth + simulation.Parameters.World.WorldWidth) %
                simulation.Parameters.World.WorldWidth;
        pos.Y = (pos.Y % simulation.Parameters.World.WorldHeight + simulation.Parameters.World.WorldHeight) %
                simulation.Parameters.World.WorldHeight;
        return pos;
    }
}