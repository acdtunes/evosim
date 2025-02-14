using System;
using System.Linq;
using Microsoft.Xna.Framework;

namespace EvolutionSim.Core;

public class ParasiteCreature : Creature
{
    public ParasiteCreature(Vector2 position, Random random, Simulation simulation)
        : base(position, 10f, 3f, random, simulation)
    {
    }

    public ParasiteCreature(Vector2 position, Random random, Simulation simulation, Genome genome)
        : base(position, 10f, 3f, random, simulation, genome)
    {
    }

    public override bool IsParasite => true;

    public override void Update(float dt, JetForces forces)
    {
        base.Update(dt, forces);
        foreach (var other in Simulation.GetNearbyCreatures<ParasiteCreature>(Position, Size * 1.5f, Id))
        {
            var collisionDistance = (Size + other.Size) / 2f;
            if (Vector2.Distance(Position, other.Position) < collisionDistance)
            {
                var drainAmount = 20 * dt;
                var actualDrain = Math.Min(other.Energy, drainAmount);
                if (actualDrain > 0 && Energy < Genome.Fullness * Genome.EnergyStorage)
                {
                    Energy += actualDrain;
                    other.Energy -= actualDrain;
                    ParasiteEnergyDelta += actualDrain;
                    other.ParasiteEnergyDelta -= actualDrain;
                }
            }
        }
    }

    protected override VisionSensor ReadVisionSensor(Func<Creature, bool> predicate)
    {
        var worldWidth = Simulation.Parameters.World.WorldWidth;
        var worldHeight = Simulation.Parameters.World.WorldHeight;
        var targets = Simulation.Creatures.Values
            .Where(c => c.Id != Id && predicate(c))
            .Select(c => c.Position);
        
        return VisionSensor.FromTargets(Position, Heading, Genome.ForagingRange, worldWidth,
            worldHeight, targets.ToArray());
    }
}