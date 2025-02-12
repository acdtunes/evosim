using System;
using Microsoft.Xna.Framework;
using System.Linq;
using System.Threading.Tasks;

namespace EvolutionSim.Core
{
    public abstract class Creature
    {
        public Genome Genome { get; set; }
        private readonly PhysicalBody _physical;
        private readonly Random _random;
        protected readonly Simulation _simulation;
        private float _backJetTimer;
        private float _bottomLeftJetTimer;
        private float _bottomRightJetTimer;
        private float _cachedBack;
        private float _cachedBottomLeft;
        private float _cachedBottomRight;
        private float _cachedFront;
        private float _cachedTopLeft;
        private float _cachedTopRight;
        private float _frontJetTimer;
        private float _topLeftJetTimer;
        private float _topRightJetTimer;
        
        public float ParasiteEnergyDelta { get; set; }

        public virtual bool IsParasite => false;

        public Creature(Vector2 position, float size, float mass, Random random, Simulation simulation, Genome? genome = null)
        {
            Id = simulation.GetNextCreatureId();
            Size = size;
            Mass = mass;
            _random = random;
            _simulation = simulation;
            Genome = genome ?? new Genome(random, simulation.Parameters.MutationRate);
            var heading = (float)(_random.NextDouble() * MathHelper.TwoPi);
            _physical = new PhysicalBody(position, heading, mass, size, BodyShape.Rod, random, simulation.Parameters);
            Energy = 100f;

            InitializeJetTimers(simulation);
            LastJetForces = new JetForces(0f, 0f, 0f, 0f, 0f, 0f);
            LastSensors = ReadSensors();
            PreviousSensors = LastSensors;
            PreviousEnergy = Energy;
        }
        
        public int Id { get; }
        public float Energy { get; set; }

        public Vector2 Position => _physical.Position;
        public float Heading => _physical.Heading;
        public BodyShape BodyShape => _physical.Shape;
        
        public float AngularVelocity => _physical.AngularVelocity;
        public float Mass { get; }
        public float Size { get; }
        public float Age { get; private set; }

        // For debugging/display purposes.
        public Sensors LastSensors { get; private set; }
        public JetForces LastJetForces { get; private set; }

        public Sensors PreviousSensors { get; set; }
        public float PreviousEnergy { get; set; }

        private void InitializeJetTimers(Simulation simulation)
        {
            _frontJetTimer = (float)_random.NextDouble() * simulation.Parameters.Physics.JetCooldown;
            _backJetTimer = (float)_random.NextDouble() * simulation.Parameters.Physics.JetCooldown;
            _topRightJetTimer = (float)_random.NextDouble() * simulation.Parameters.Physics.JetCooldown;
            _topLeftJetTimer = (float)_random.NextDouble() * simulation.Parameters.Physics.JetCooldown;
            _bottomRightJetTimer = (float)_random.NextDouble() * simulation.Parameters.Physics.JetCooldown;
            _bottomLeftJetTimer = (float)_random.NextDouble() * simulation.Parameters.Physics.JetCooldown;
        }

        public virtual void Update(float dt, JetForces forces)
        {
            ParasiteEnergyDelta = 0;
            
            Age += dt;

            UpdateJetTimers(dt, forces);

            LastJetForces = new JetForces(
                _cachedFront, _cachedBack, _cachedTopRight, _cachedTopLeft, _cachedBottomRight, _cachedBottomLeft);

            Energy -= CalculateJetEnergyCost(dt);

            _physical.ApplyJetForces(LastJetForces);
            _physical.Update(dt);

            if (!IsParasite)
                CheckForPlantCollision();;

            LastSensors = ReadSensors();

            var reproductionProbability = 0.01;
            if (Energy >= _simulation.Parameters.Creature.ReproductionEnergyThreshold && _random.NextDouble() < reproductionProbability * dt)
            {
                Reproduce();
            }
            if (Energy <= 0)
            {
                _simulation.KillCreature(this);
            }
        }
        
        protected virtual void Reproduce()
        {
            // Create a mutated copy of the genome (which includes brain weights)
            var offspringGenome = Genome.Mutate();

            // Deduct half of the parent's energy and assign it to the offspring.
            float offspringEnergy = Energy / 2;
            Energy /= 2;

            // Place the offspring near the parent (using a small random offset)
            var offset = new Vector2((float)_random.NextDouble() - 0.5f, (float)_random.NextDouble() - 0.5f) * Size;
            var offspringPosition = Position + offset;

            Creature offspring;
            if (this is ParasiteCreature)
            {
                offspring = new ParasiteCreature(offspringPosition, _random, _simulation, offspringGenome);
            }
            else
            {
                offspring = new SimpleCreature(offspringPosition, Size, Mass, _random, _simulation, offspringGenome);
            }
            offspring.Energy = offspringEnergy;
            _simulation.AddCreature(offspring);

            Task.Run(async () => await _simulation.InitializeBrainForCreature(offspring));
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

        public float CalculateJetEnergyCost(float dt)
        {
            var forces = LastJetForces;
            var costFactor = _simulation.Parameters.Creature.MovementEnergyCostFactor;
            var turningCostFactor = 50f;
            return (forces.Front + forces.Back +
                    forces.TopRight*turningCostFactor + forces.TopLeft*turningCostFactor +
                    forces.BottomRight*turningCostFactor + forces.BottomLeft*turningCostFactor) * costFactor * dt;
        }

        private void CheckForPlantCollision()
        {
            var eatingRadius = Size / 2;
            var plant = _simulation.GetPlantAtPosition(Position, eatingRadius);
            if (plant != null && Energy < Genome.Fullness * Genome.EnergyStorage)
            {
                Energy = Math.Min(Energy + _simulation.Parameters.Plant.EnergyGain, Genome.EnergyStorage);
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
                var toPlant = Position.TorusDifference(
                    nearestPlant.Position,
                    _simulation.Parameters.World.WorldWidth,
                    _simulation.Parameters.World.WorldHeight);
                var distance = toPlant.Length();
                plantNormalizedDistance = MathHelper.Clamp(distance / Genome.ForagingRange, 0, 1);

                var targetAngle = (float)Math.Atan2(toPlant.Y, toPlant.X);
                var angleDiff = MathHelper.WrapAngle(targetAngle - Heading);
                plantAngleSin = (float)Math.Sin(angleDiff);
                plantAngleCos = (float)Math.Cos(angleDiff);
            }

            float creatureAngleSin = 0;
            float creatureAngleCos = 0;
            float creatureNormalizedDistance = 1;
            var worldWidth = _simulation.Parameters.World.WorldWidth;
            var worldHeight = _simulation.Parameters.World.WorldHeight;
            Vector2 avgOffset = Vector2.Zero;
            int count = 0;
            if (IsParasite)
            {
                // Consider all non-parasite creatures.
                var targets = _simulation.Creatures.Values.Where(c => c.Id != this.Id && !c.IsParasite);
                foreach (var target in targets)
                {
                    float dist = Position.TorusDistance(target.Position, worldWidth, worldHeight);
                    if (dist <= Genome.ForagingRange)
                    {
                        count++;
                        avgOffset += Position.TorusDifference(target.Position, worldWidth, worldHeight);
                    }
                }

                plantNormalizedDistance = 1;
            }
            else
            {
                // Consider all parasite creatures.
                var targets = _simulation.Creatures.Values.Where(c => c.Id != this.Id && c.IsParasite);
                foreach (var target in targets)
                {
                    float dist = Position.TorusDistance(target.Position, worldWidth, worldHeight);
                    if (dist <= Genome.ForagingRange)
                    {
                        count++;
                        avgOffset += Position.TorusDifference(target.Position, worldWidth, worldHeight);
                    }
                }
            }

            if (count > 0)
            {
                avgOffset /= count;
                float distanceCreature = avgOffset.Length();
                creatureNormalizedDistance = MathHelper.Clamp(distanceCreature / Genome.ForagingRange, 0, 1);
                float targetCreatureAngle = (float)Math.Atan2(avgOffset.Y, avgOffset.X);
                float angleDiffCreature = MathHelper.WrapAngle(targetCreatureAngle - Heading);
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
}