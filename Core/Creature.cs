using System;
using Microsoft.Xna.Framework;

namespace EvolutionSim.Core;

public abstract class Creature
{
    public int Id { get; }

    private readonly Random _random;
    private readonly Simulation _simulation;
    private readonly Brain _brain;
    private readonly PhysicalBody _physical;

    public Vector2 Position => _physical.Position;
    public float Heading => _physical.Heading;
    public BodyShape BodyShape => _physical.Shape;
    public float Mass { get; }
    public float Size { get; }
    public float Age { get; private set; }
    private readonly Genome _genome;

    protected Creature(Vector2 position, float size, float mass, Random random, Simulation simulation)
    {
        Id = simulation.GetNextCreatureId();
        Size = size;
        Mass = mass;
        _random = random;
        _simulation = simulation;
        _genome = new Genome(random, simulation.Parameters.MutationRate);
        var heading = (float)(_random.NextDouble() * MathHelper.TwoPi);
        _physical = new PhysicalBody(
            position, 
            heading, 
            mass, 
            size,
            BodyShape.Rod,
            simulation.Parameters
        );
        _brain = new Brain(Genome.InputCount, Genome.HiddenCount, Genome.OutputCount, _genome.BrainWeights);
    }


    public void Update(float dt)
    {
        Age += dt;
        var forces = EvaluateBrain();
        _physical.ApplyJetForces(forces);
        _physical.Update(dt);
    }
    
    private JetForces EvaluateBrain()
    {
        // --- Plant Sensor ---
        var nearestPlant = _simulation.GetNearestPlant(Position);
        float plantAngleSin = 0;
        float plantAngleCos = 0;
        float plantNormalizedDistance = 1;
        if (nearestPlant != null)
        {
            var toPlant = Position.TorusDifference(nearestPlant.Position, _simulation.Parameters.World.WorldWidth, _simulation.Parameters.World.WorldHeight);
            var distance = toPlant.Length();
            plantNormalizedDistance = MathHelper.Clamp(distance / _genome.ForagingRange, 0, 1);

            var targetAngle = (float)Math.Atan2(toPlant.Y, toPlant.X);
            var angleDiff = MathHelper.WrapAngle(targetAngle - Heading);
            plantAngleSin = (float)Math.Sin(angleDiff);
            plantAngleCos = (float)Math.Cos(angleDiff);
        }

        // --- Creature Sensor ---
        // Find the nearest creature (excluding self)
        var nearestCreature = _simulation.GetNearestCreature(Position, this.Id);
        float creatureAngleSin = 0;
        float creatureAngleCos = 0;
        float creatureNormalizedDistance = 1;
        if (nearestCreature != null)
        {
            var toCreature = Position.TorusDifference(nearestCreature.Position, _simulation.Parameters.World.WorldWidth, _simulation.Parameters.World.WorldHeight);
            var distanceCreature = toCreature.Length();
            creatureNormalizedDistance = MathHelper.Clamp(distanceCreature / _genome.ForagingRange, 0, 1);

            var targetCreatureAngle = (float)Math.Atan2(toCreature.Y, toCreature.X);
            var angleDiffCreature = MathHelper.WrapAngle(targetCreatureAngle - Heading);
            creatureAngleSin = (float)Math.Sin(angleDiffCreature);
            creatureAngleCos = (float)Math.Cos(angleDiffCreature);
        }

        var sensors = new Sensors(
            plantNormalizedDistance, plantAngleSin, plantAngleCos,
            creatureNormalizedDistance, creatureAngleSin, creatureAngleCos
        );
    
        return _brain.Evaluate(sensors);
    }
}