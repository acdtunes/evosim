using System;
using Microsoft.Xna.Framework;

namespace EvolutionSim.Core;

public abstract class Creature
{
    public int Id { get; }
    public float Energy { get; private set; }
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

    // NEW: Store the latest sensor readings and jet forces for debugging/display purposes.
    public Sensors LastSensors { get; private set; }
    public JetForces LastJetForces { get; private set; }

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
            random,
            simulation.Parameters
        );
        _brain = new Brain(Genome.InputCount, Genome.HiddenCount, Genome.OutputCount, _genome.BrainWeights);
        Energy = 100f; // Starting energy
    }

    public void Update(float dt)
    {
        Age += dt;
        
        // Get sensor readings and store stats (thanks to the modified EvaluateBrain)
        var forces = EvaluateBrain();
        
        // Calculate the energy cost from activating jets.
        float energyCost = CalculateJetEnergyCost(forces, dt);
        Energy -= energyCost;
        
        // Let the physical body move the creature.
        _physical.ApplyJetForces(forces);
        _physical.Update(dt);
        
        // Check for plant collision (eating)
        CheckForPlantCollision();

    }

    /// <summary>
    /// Simple energy cost calculation for activating jets.
    /// </summary>
    private float CalculateJetEnergyCost(JetForces forces, float dt)
    {
        var costFactor = _simulation.Parameters.Creature.MovementEnergyCostFactor;
        float cost = (forces.Front + forces.Back +
                      forces.TopRight + forces.TopLeft +
                      forces.BottomRight + forces.BottomLeft) * costFactor * dt;
        return cost;
    }

    /// <summary>
    /// Checks whether the creature is close enough to a plant to "eat" it.
    /// </summary>
    private void CheckForPlantCollision()
    {
        float eatingRadius = Size / 2; 
        var plant = _simulation.GetPlantAtPosition(Position, eatingRadius);
        if (plant != null && Energy < _genome.Fullness * _genome.EnergyStorage)
        {
            Energy = Math.Min(Energy + _simulation.Parameters.Plant.EnergyGain, _genome.EnergyStorage);
            _simulation.KillPlant(plant);
        }
    }

    /// <summary>
    /// Evaluates the creature's brain given current sensors.
    /// </summary>
    private JetForces EvaluateBrain()
    {
        // --- Plant Sensor (already implemented) ---
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

        float hungerSensor = 1 - MathHelper.Clamp(Energy / 100f, 0, 1);

        var sensors = new Sensors(
            plantNormalizedDistance, plantAngleSin, plantAngleCos,
            creatureNormalizedDistance, creatureAngleSin, creatureAngleCos,
            hungerSensor
        );

        LastSensors = sensors;
        var forces = _brain.Evaluate(sensors);
        LastJetForces = forces;
        return forces;
    }
}