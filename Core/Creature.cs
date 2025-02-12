using System;
using Microsoft.Xna.Framework;

namespace EvolutionSim.Core;

public abstract class Creature
{
    private readonly Brain _brain;
    private readonly Genome _genome;
    private readonly PhysicalBody _physical;
    private readonly Random _random;
    private readonly Simulation _simulation;
    private float _backJetTimer;
    private float _bottomLeftJetTimer;
    private float _bottomRightJetTimer;
    private float _cachedBack;
    private float _cachedBottomLeft;
    private float _cachedBottomRight;

    // NEW: Cached force values for each individual jet
    private float _cachedFront;
    private float _cachedTopLeft;
    private float _cachedTopRight;

    // NEW: Independent per-jet cooldown timers
    private float _frontJetTimer;
    private float _topLeftJetTimer;
    private float _topRightJetTimer;

    protected Creature(Vector2 position, float size, float mass, Random random, Simulation simulation)
    {
        Id = simulation.GetNextCreatureId();
        Size = size;
        Mass = mass;
        _random = random;
        _simulation = simulation;
        _genome = new Genome(random, simulation.Parameters.MutationRate);
        var heading = (float)(_random.NextDouble() * MathHelper.TwoPi);
        _physical = new PhysicalBody(position, heading, mass, size, BodyShape.Rod, random, simulation.Parameters);
        _brain = new Brain(Genome.InputCount, Genome.HiddenCount, Genome.OutputCount, _genome.BrainWeights);
        Energy = 100f;

        InitializeJetTimers(simulation);

        LastJetForces = new JetForces(0f, 0f, 0f, 0f, 0f, 0f);
    }

    public int Id { get; }
    public float Energy { get; private set; }

    public Vector2 Position => _physical.Position;
    public float Heading => _physical.Heading;
    public BodyShape BodyShape => _physical.Shape;
    public float Mass { get; }
    public float Size { get; }
    public float Age { get; private set; }

    // NEW: Store the latest sensor readings and jet forces for debugging/display purposes.
    public Sensors LastSensors { get; private set; }
    public JetForces LastJetForces { get; private set; }

    private void InitializeJetTimers(Simulation simulation)
    {
        _frontJetTimer = (float)_random.NextDouble() * simulation.Parameters.Physics.JetCooldown;
        _backJetTimer = (float)_random.NextDouble() * simulation.Parameters.Physics.JetCooldown;
        _topRightJetTimer = (float)_random.NextDouble() * simulation.Parameters.Physics.JetCooldown;
        _topLeftJetTimer = (float)_random.NextDouble() * simulation.Parameters.Physics.JetCooldown;
        _bottomRightJetTimer = (float)_random.NextDouble() * simulation.Parameters.Physics.JetCooldown;
        _bottomLeftJetTimer = (float)_random.NextDouble() * simulation.Parameters.Physics.JetCooldown;
    }

    public void Update(float dt, JetForces forces)
    {
        Age += dt;

        UpdateJetTimers(dt, forces);

        LastJetForces = new JetForces(_cachedFront, _cachedBack, _cachedTopRight, _cachedTopLeft, _cachedBottomRight,
            _cachedBottomLeft);

        Energy -= CalculateJetEnergyCost(LastJetForces, dt);

        _physical.ApplyJetForces(LastJetForces);
        _physical.Update(dt);

        CheckForPlantCollision();
    }

    private void UpdateJetTimers(float dt, JetForces forces)
    {
        _frontJetTimer -= dt;
        if (_frontJetTimer <= 0f)
        {
            _frontJetTimer = _simulation.Parameters.Physics.JetCooldown;
            _cachedFront = forces.Front;
        }

        _backJetTimer -= dt;
        if (_backJetTimer <= 0f)
        {
            _backJetTimer = _simulation.Parameters.Physics.JetCooldown;
            _cachedBack = forces.Back;
        }

        _topRightJetTimer -= dt;
        if (_topRightJetTimer <= 0f)
        {
            _topRightJetTimer = _simulation.Parameters.Physics.JetCooldown;
            _cachedTopRight = forces.TopRight;
        }

        _topLeftJetTimer -= dt;
        if (_topLeftJetTimer <= 0f)
        {
            _topLeftJetTimer = _simulation.Parameters.Physics.JetCooldown;
            _cachedTopLeft = forces.TopLeft;
        }

        _bottomRightJetTimer -= dt;
        if (_bottomRightJetTimer <= 0f)
        {
            _bottomRightJetTimer = _simulation.Parameters.Physics.JetCooldown;
            _cachedBottomRight = forces.BottomRight;
        }

        _bottomLeftJetTimer -= dt;
        if (_bottomLeftJetTimer <= 0f)
        {
            _bottomLeftJetTimer = _simulation.Parameters.Physics.JetCooldown;
            _cachedBottomLeft = forces.BottomLeft;
        }
    }

    /// <summary>
    ///     Simple energy cost calculation for activating jets.
    /// </summary>
    private float CalculateJetEnergyCost(JetForces forces, float dt)
    {
        var costFactor = _simulation.Parameters.Creature.MovementEnergyCostFactor;
        return (forces.Front + forces.Back +
                forces.TopRight + forces.TopLeft +
                forces.BottomRight + forces.BottomLeft) * costFactor * dt;
    }

    /// <summary>
    ///     Checks whether the creature is close enough to a plant to "eat" it.
    /// </summary>
    private void CheckForPlantCollision()
    {
        var eatingRadius = Size / 2;
        var plant = _simulation.GetPlantAtPosition(Position, eatingRadius);
        if (plant != null && Energy < _genome.Fullness * _genome.EnergyStorage)
        {
            Energy = Math.Min(Energy + _simulation.Parameters.Plant.EnergyGain, _genome.EnergyStorage);
            _simulation.KillPlant(plant);
        }
    }

    public Sensors ReadSensors()
    {
        var nearestPlant = _simulation.GetNearestPlant(Position);
        float plantAngleSin = 0;
        float plantAngleCos = 0;
        float plantNormalizedDistance = 1;
        if (nearestPlant != null)
        {
            var toPlant = Position.TorusDifference(nearestPlant.Position, _simulation.Parameters.World.WorldWidth,
                _simulation.Parameters.World.WorldHeight);
            var distance = toPlant.Length();
            plantNormalizedDistance = MathHelper.Clamp(distance / _genome.ForagingRange, 0, 1);

            var targetAngle = (float)Math.Atan2(toPlant.Y, toPlant.X);
            var angleDiff = MathHelper.WrapAngle(targetAngle - Heading);
            plantAngleSin = (float)Math.Sin(angleDiff);
            plantAngleCos = (float)Math.Cos(angleDiff);
        }

        var nearestCreature = _simulation.GetNearestCreature(Position, Id);
        float creatureAngleSin = 0;
        float creatureAngleCos = 0;
        float creatureNormalizedDistance = 1;
        if (nearestCreature != null)
        {
            var toCreature = Position.TorusDifference(nearestCreature.Position, _simulation.Parameters.World.WorldWidth,
                _simulation.Parameters.World.WorldHeight);
            var distanceCreature = toCreature.Length();
            creatureNormalizedDistance = MathHelper.Clamp(distanceCreature / _genome.ForagingRange, 0, 1);

            var targetCreatureAngle = (float)Math.Atan2(toCreature.Y, toCreature.X);
            var angleDiffCreature = MathHelper.WrapAngle(targetCreatureAngle - Heading);
            creatureAngleSin = (float)Math.Sin(angleDiffCreature);
            creatureAngleCos = (float)Math.Cos(angleDiffCreature);
        }

        var hungerSensor = 1 - MathHelper.Clamp(Energy / 100f, 0, 1);

        LastSensors = new Sensors(
            plantNormalizedDistance, plantAngleSin, plantAngleCos,
            creatureNormalizedDistance, creatureAngleSin, creatureAngleCos,
            hungerSensor
        );

        return LastSensors;
    }
}