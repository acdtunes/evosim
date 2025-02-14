using System;
using Microsoft.Xna.Framework;

namespace EvolutionSim.Core;

public class Plant(Vector2 position, Random random, Simulation simulation)
{
    public int Id { get; set; } = simulation.GetNextPlantId();
    public Vector2 Position { get; } = position;

    public void Update(float dt)
    {
        double reproductionProbability = simulation.Parameters.Plant.ReproductionProbability;
        if (random.NextDouble() < reproductionProbability * dt)
            if (simulation.Plants.Count < simulation.Parameters.Population.GlobalMaxPlantCount)
            {
                var angle = (float)(random.NextDouble() * MathHelper.TwoPi);
                var distance =
                    (float)(random.NextDouble() * simulation.Parameters.Population.InitialPlantClusterRadius);
                var offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * distance;
                var newPlantPos = Position + offset;
                newPlantPos.X = MathHelper.Clamp(newPlantPos.X, 0, simulation.Parameters.World.WorldWidth);
                newPlantPos.Y = MathHelper.Clamp(newPlantPos.Y, 0, simulation.Parameters.World.WorldHeight);
                var newPlant = new Plant(newPlantPos, random, simulation);
                simulation.AddPlant(newPlant);
            }
    }
}