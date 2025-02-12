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
        {
            if (simulation.Plants.Count < simulation.Parameters.Population.MaxPlantCount)
            {
                float angle = (float)(random.NextDouble() * MathHelper.TwoPi);
                float distance = (float)(random.NextDouble() * 50);
                Vector2 offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * distance;
                Vector2 newPlantPos = Position + offset;
                newPlantPos.X = MathHelper.Clamp(newPlantPos.X, 0, simulation.Parameters.World.WorldWidth);
                newPlantPos.Y = MathHelper.Clamp(newPlantPos.Y, 0, simulation.Parameters.World.WorldHeight);
                var newPlant = new Plant(newPlantPos, random, simulation);
                simulation.AddPlant(newPlant);
            }
        }
    }
}