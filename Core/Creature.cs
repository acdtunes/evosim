using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

namespace EvolutionSim.Core;

public abstract class Creature
{
    private readonly Jet _backJet;
    private readonly PhysicalBody _physical;
    private readonly Random _random;
    private readonly Jet _frontLeftJet;
    private readonly Jet _frontRightJet;
    protected readonly Simulation Simulation;
    private float _smoothedReward = 0;

    protected Creature(Vector2 position, float size, float mass, Random random, Simulation simulation,
        Genome? genome = null)
    {
        Id = simulation.GetNextCreatureId();
        Size = size;
        Mass = mass;
        _random = random;
        Simulation = simulation;
        Genome = genome ?? new Genome(random, simulation.Parameters.MutationRate);
        var heading = (float)(_random.NextDouble() * MathHelper.TwoPi);
        _physical = new PhysicalBody(position, heading, mass, size, BodyShape.Rod, random, simulation.Parameters);
        Energy = 100f;

        const float turningCostFactor = 50f;

        _backJet = new Jet(random, simulation.Parameters.Physics.JetCooldown, 1f);
        _frontRightJet = new Jet(random, simulation.Parameters.Physics.JetCooldown, turningCostFactor);
        _frontLeftJet = new Jet(random, simulation.Parameters.Physics.JetCooldown, turningCostFactor);

        LastSensors = ReadSensors();
        PreviousSensors = LastSensors;
        PreviousEnergy = Energy;
    }

    public Genome Genome { get; set; }

    public float ParasiteEnergyDelta { get; set; }

    public virtual bool IsParasite => false;

    public int Id { get; }
    public float Energy { get; set; }

    public Vector2 Position => _physical.Position;
    public float Heading => _physical.Heading;
    public BodyShape BodyShape => _physical.Shape;

    public float AngularVelocity => _physical.AngularVelocity;
    public float Mass { get; }
    public float Size { get; }
    public float Age { get; private set; }

    public Sensors LastSensors { get; private set; }

    public JetForces LastJetForces => new(_backJet.LastForce, _frontRightJet.LastForce, _frontLeftJet.LastForce);

    public Sensors PreviousSensors { get; set; }
    public float PreviousEnergy { get; set; }

    public virtual void Update(float dt, JetForces forces)
    {
        ParasiteEnergyDelta = 0;

        Age += dt;

        UpdateJetForces(dt, forces);

        var totalJetEnergyCost = CalculateJetEnergyCost(dt);
        Energy -= totalJetEnergyCost;

        _physical.ApplyJetForces(LastJetForces);
        _physical.Update(dt);

        if (!IsParasite)
            CheckForPlantCollision();

        LastSensors = ReadSensors();

        var reproductionProbability = Simulation.Parameters.Creature.ReproductionProbability;
        if (Energy >= Simulation.Parameters.Creature.ReproductionEnergyThreshold &&
            _random.NextDouble() < reproductionProbability * dt) 
            Reproduce();
        
        // if (Energy <= 0) 
        //     Simulation.KillCreature(this);
    }

    private void UpdateJetForces(float dt, JetForces forces)
    {
        _backJet.Update(dt, forces.Back);
        _frontRightJet.Update(dt, forces.FrontRight);
        _frontLeftJet.Update(dt, forces.FrontLeft);
    }

    protected virtual void Reproduce()
    {
        return;
        var type = GetType();
        var offspringGenome = Genome.Mutate();

        var offspringEnergy = Energy / 2;
        //Energy /= 2;

        var offset = new Vector2((float)_random.NextDouble() - 0.5f, (float)_random.NextDouble() - 0.5f) * Size;
        var offspringPosition = Position + offset;

        Creature offspring;
        if (this is ParasiteCreature)
            offspring = new ParasiteCreature(offspringPosition, _random, Simulation, offspringGenome);
        else
            offspring = new SimpleCreature(offspringPosition, Size, Mass, _random, Simulation, offspringGenome);
        offspring.Energy = offspringEnergy;
        Simulation.AddCreature(offspring);
    }

    private void CheckForPlantCollision()
    {
        var eatingRadius = Size / 2;
        var plant = Simulation.GetPlantAtPosition(Position, eatingRadius);
        if (plant != null && Energy < Genome.Fullness * Genome.EnergyStorage)
        {
            Energy = Math.Min(Energy + Simulation.Parameters.Plant.EnergyGain, Genome.EnergyStorage);
            Simulation.KillPlant(plant);
        }
    }

    public Sensors ReadSensors()
    {
        int numCones = 8;

        var plantsInRange = Simulation.GetPlantsInRange(Position, Genome.ForagingRange);
        var plantPositions = plantsInRange.Select(p => p.Position).ToArray();
        float[] plantRetina = Retina.FromTargets(
            Position,
            Heading,
            Genome.ForagingRange,
            Simulation.Parameters.World.WorldWidth,
            Simulation.Parameters.World.WorldHeight,
            numCones,
            plantPositions
        ).Activations;

        var nearbyCreatures = Simulation.GetNearbyCreatures(Position, Genome.ForagingRange, Id);
        var nonParasitePositions = nearbyCreatures
            .Where(c => !c.IsParasite)
            .Select(c => c.Position)
            .ToArray();
        var parasitePositions = nearbyCreatures
            .Where(c => c.IsParasite)
            .Select(c => c.Position)
            .ToArray();
        float[] nonParasiteRetina = Retina.FromTargets(
            Position,
            Heading,
            Genome.ForagingRange,
            Simulation.Parameters.World.WorldWidth,
            Simulation.Parameters.World.WorldHeight,
            numCones,
            nonParasitePositions
        ).Activations;
        float[] parasiteRetina = Retina.FromTargets(
            Position,
            Heading,
            Genome.ForagingRange,
            Simulation.Parameters.World.WorldWidth,
            Simulation.Parameters.World.WorldHeight,
            numCones,
            parasitePositions
        ).Activations;

        var energy = MathHelper.Clamp(Energy / Genome.EnergyStorage, 0, 1);
        LastSensors = new Sensors(plantRetina, nonParasiteRetina, parasiteRetina, energy);
        return LastSensors;
    }

    public float CalculateJetEnergyCost(float dt)
    {
        var costFactor = Simulation.Parameters.Creature.MovementEnergyCostFactor;
        return _backJet.CalculateEnergyCost(dt, costFactor) +
               _frontRightJet.CalculateEnergyCost(dt, costFactor) +
               _frontLeftJet.CalculateEnergyCost(dt, costFactor);
    }

    public BrainTransition BuildTransition(float dt)
    {
        var energyChange = Energy - PreviousEnergy;
        var energySpent = CalculateJetEnergyCost(dt);
        var hunger = 1 - MathHelper.Clamp(Energy / Genome.EnergyStorage, 0, 1);
        var penaltyCoefficient = Simulation.Parameters.Reward.PenaltyCoefficient;
        var penalty = (1 - hunger) * penaltyCoefficient * energySpent;
        var angularPenaltyCoefficient = Simulation.Parameters.Reward.AngularPenaltyCoefficient;
        var angularPenalty = AngularVelocity * AngularVelocity * angularPenaltyCoefficient * dt;

        // Parasite bonus remains.
        var parasiteRewardMultiplier = Simulation.Parameters.Reward.ParasiteRewardMultiplier;
        var instantaneousReward = energyChange - penalty - angularPenalty + parasiteRewardMultiplier * ParasiteEnergyDelta;

        // Add a survival bonus per time step to encourage staying alive.
        var survivalBonus = Simulation.Parameters.Reward.SurvivalBonusPerSecond * dt;
        instantaneousReward += survivalBonus;

        // Smooth the reward signal using an exponential moving average.
        var smoothingFactor = Simulation.Parameters.Reward.RewardSmoothingFactor;
        _smoothedReward = smoothingFactor * _smoothedReward + (1 - smoothingFactor) * instantaneousReward;
        var reward = _smoothedReward;

        var currentSensors = LastSensors;
        var transition = new BrainTransition
        {
            Id = Id,
            State = PreviousSensors.ToArray(),
            Action = LastJetForces.ToArray(),
            Reward = energyChange,
            NextState = currentSensors.ToArray(),
            Done = false
        };

        PreviousSensors = currentSensors;
        PreviousEnergy = Energy;

        return transition;
    }
}