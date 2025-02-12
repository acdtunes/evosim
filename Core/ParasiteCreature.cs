// In ParasiteCreature.cs

using System;
using Microsoft.Xna.Framework;

namespace EvolutionSim.Core;

public class ParasiteCreature : Creature
{
    // Existing constructor:
    public ParasiteCreature(Vector2 position, Random random, Simulation simulation)
        : base(position, 10f, 3f, random, simulation)
    { }

    // New constructor for offspring:
    public ParasiteCreature(Vector2 position, Random random, Simulation simulation, Genome genome)
        : base(position, 10f, 3f, random, simulation, genome)
    { }

    public override bool IsParasite => true;

    public override void Update(float dt, JetForces forces)
    {
        base.Update(dt, forces);
        // (Existing parasite-specific energy-draining behavior remains here.)
        foreach (var other in _simulation.Creatures.Values)
        {
            if (other == this || other.IsParasite)
                continue;
            float collisionDistance = (this.Size + other.Size) / 2f;
            if (Vector2.Distance(this.Position, other.Position) < collisionDistance)
            {
                float drainAmount = 10f * dt;
                float actualDrain = Math.Min(other.Energy, drainAmount);
                if (actualDrain > 0)
                {
                    Energy += actualDrain;
                    other.Energy -= actualDrain;
                    this.ParasiteEnergyDelta += actualDrain;
                    other.ParasiteEnergyDelta -= actualDrain;
                }
            }
        }
    }
}