using System;
using Microsoft.Xna.Framework;

namespace EvolutionSim.Core;

public class SimpleCreature : Creature
{
    public SimpleCreature(Vector2 position, float size, float mass, Random random, Simulation simulation)
        : base(position, size, mass, random, simulation)
    {
    }

    public SimpleCreature(Vector2 position, float size, float mass, Random random, Simulation simulation, Genome genome)
        : base(position, size, mass, random, simulation, genome)
    {
    }
}