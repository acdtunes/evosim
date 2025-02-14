using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

namespace EvolutionSim.Core;

public abstract class Creature
{
    private readonly Jet _backJet;
    private readonly Jet _bottomLeftJet;
    private readonly Jet _bottomRightJet;
    private readonly Jet _frontJet;
    private readonly PhysicalBody _physical;
    private readonly Random _random;
    private readonly Jet _topLeftJet;
    private readonly Jet _topRightJet;
    protected readonly Simulation Simulation;

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

        _frontJet = new Jet(random, simulation.Parameters.Physics.JetCooldown, 1f);
        _backJet = new Jet(random, simulation.Parameters.Physics.JetCooldown, 1f);
        _topRightJet = new Jet(random, simulation.Parameters.Physics.JetCooldown, turningCostFactor);
        _topLeftJet = new Jet(random, simulation.Parameters.Physics.JetCooldown, turningCostFactor);
        _bottomRightJet = new Jet(random, simulation.Parameters.Physics.JetCooldown, turningCostFactor);
        _bottomLeftJet = new Jet(random, simulation.Parameters.Physics.JetCooldown, turningCostFactor);

        Task.Run(async () => await InitializeBrain());
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

    public JetForces LastJetForces => new(
        _frontJet.LastForce, _backJet.LastForce,
        _topRightJet.LastForce, _topLeftJet.LastForce,
        _bottomRightJet.LastForce, _bottomLeftJet.LastForce);

    public Sensors PreviousSensors { get; set; }
    public float PreviousEnergy { get; set; }

    private async Task InitializeBrain()
    {
        try
        {
            await Simulation.Client.InitBrainAsync(Id, Genome.BrainWeights);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error initializing brain for creature " + Id + ": " + ex.Message);
        }
    }

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
            _random.NextDouble() < reproductionProbability * dt) Reproduce();
        if (Energy <= 0) Simulation.KillCreature(this);
    }

    private void UpdateJetForces(float dt, JetForces forces)
    {
        _frontJet.Update(dt, forces.Front);
        //_backJet.Update(dt, forces.Back);
        _topRightJet.Update(dt, forces.TopRight);
        _topLeftJet.Update(dt, forces.TopLeft);
        //_bottomRightJet.Update(dt, forces.BottomRight);
        //_bottomLeftJet.Update(dt, forces.BottomLeft);
    }

    protected virtual void Reproduce()
    {
        var offspringGenome = Genome.Mutate();

        var offspringEnergy = Energy / 2;
        Energy /= 2;

        var offset = new Vector2((float)_random.NextDouble() - 0.5f, (float)_random.NextDouble() - 0.5f) * Size;
        var offspringPosition = Position + offset;

        Creature offspring;
        if (this is ParasiteCreature)
            offspring = new ParasiteCreature(offspringPosition, _random, Simulation, offspringGenome);
        else
            offspring = new SimpleCreature(offspringPosition, Size, Mass, _random, Simulation, offspringGenome);
        offspring.Energy = offspringEnergy;
        Simulation.AddCreature(offspring);

        Task.Run(async () => await offspring.InitializeBrain());
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
        var nearestPlant = Simulation.GetNearestPlant(Position);
        var plantVisionSensor = VisionSensor.FromTargets(
            Position,
            Heading,
            Genome.ForagingRange,
            Simulation.Parameters.World.WorldWidth,
            Simulation.Parameters.World.WorldHeight,
            nearestPlant?.Position ?? Position
        );

        var creatureSensor = ReadVisionSensor(c => c.IsParasite);

        var energy = MathHelper.Clamp(Energy / Genome.EnergyStorage, 0, 1);
        
        LastSensors = new Sensors(plantVisionSensor, creatureSensor, energy);
        
        return LastSensors;
    }

    protected virtual VisionSensor ReadVisionSensor(Func<Creature, bool> predicate)
    {
        var targets = Simulation.Creatures.Values
            .Where(c => c.Id != Id && predicate(c))
            .Select(c => c.Position)
            .ToArray();

        return VisionSensor.FromTargets(
            Position,
            Heading,
            Genome.ForagingRange,
            Simulation.Parameters.World.WorldWidth,
            Simulation.Parameters.World.WorldHeight,
            targets
        );
    }

    public float CalculateJetEnergyCost(float dt)
    {
        var costFactor = Simulation.Parameters.Creature.MovementEnergyCostFactor;
        return _frontJet.CalculateEnergyCost(dt, costFactor) +
               _backJet.CalculateEnergyCost(dt, costFactor) +
               _topRightJet.CalculateEnergyCost(dt, costFactor) +
               _topLeftJet.CalculateEnergyCost(dt, costFactor) +
               _bottomRightJet.CalculateEnergyCost(dt, costFactor) +
               _bottomLeftJet.CalculateEnergyCost(dt, costFactor);
    }

    public BrainTransition BuildTransition(float dt)
    {
        var currentSensors = LastSensors;
        var energySpent = CalculateJetEnergyCost(dt);
        var hunger = 1 - MathHelper.Clamp(Energy / Genome.EnergyStorage, 0, 1);
        var penaltyCoefficient = 1000f;
        var penalty = (1 - hunger) * penaltyCoefficient * energySpent;
        var angularPenaltyCoefficient = 800f;
        var angularPenalty = AngularVelocity * AngularVelocity * angularPenaltyCoefficient * dt;

        var baseReward = Energy - PreviousEnergy - penalty - angularPenalty;
        var parasiteRewardMultiplier = 20f;
        var reward = baseReward + parasiteRewardMultiplier * ParasiteEnergyDelta;

        var transition = new BrainTransition
        {
            Id = Id,
            State = PreviousSensors.ToArray(),
            Action = LastJetForces.ToArray(),
            Reward = reward,
            NextState = currentSensors.ToArray(),
            Done = false
        };

        PreviousSensors = currentSensors;
        PreviousEnergy = Energy;

        return transition;
    }
}