using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

namespace EvolutionSim.Core;

public class Simulation
{
    private readonly Dictionary<int, Creature> _deadCreatures;
    private readonly Dictionary<int, Plant> _deadPlants;

    private readonly object _jetForcesLock = new();
    private readonly Random _random;

    private readonly BrainClient _client;

    private Dictionary<int, JetForces> _jetForces = new();
    private int _nextCreatureId;
    private int _nextPlantId;
    
    public SimulationParameters Parameters { get; }
    public Dictionary<int, Creature> Creatures { get; }
    public Dictionary<int, Plant> Plants { get; }

    public Simulation(SimulationParameters parameters, Random random)
    {
        _random = random;
        Parameters = parameters;
        Creatures = new Dictionary<int, Creature>();
        Plants = new Dictionary<int, Plant>();
        _deadCreatures = new Dictionary<int, Creature>();
        _deadPlants = new Dictionary<int, Plant>();

        _client = new BrainClient("localhost", 5000);

        PopulateCreatures();

        PopulatePlants();
        
        InitializeBrains();
    }

    private void InitializeBrains()
    {
        var weights = new Dictionary<int, float[]>();
        foreach (var creature in Creatures.Values)
            weights[creature.Id] = creature.Genome.BrainWeights;

        var initResults = _client.InitializeBrainsAsync(weights).Result;

        foreach (var kvp in initResults.Where(kvp => !kvp.Value))
        {
            Console.WriteLine($"Initialization failed for creature {kvp.Key}");
        }
    }

    private void PopulatePlants()
    {
        for (var cluster = 0; cluster < Parameters.Population.InitialPlantClusters; cluster++)
        {
            var clusterCenter = new Vector2(
                _random.Next(Parameters.World.WorldWidth),
                _random.Next(Parameters.World.WorldHeight)
            );

            var plantCount = _random.Next(1, Parameters.Population.MaxPlantsPerCluster + 1);

            for (var i = 0; i < plantCount; i++)
            {
                var angle = (float)(_random.NextDouble() * MathHelper.TwoPi);
                var distance = (float)(_random.NextDouble() * Parameters.Population.InitialPlantClusterRadius);
                var offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * distance;
                var pos = clusterCenter + offset;

                pos.X = (pos.X % Parameters.World.WorldWidth + Parameters.World.WorldWidth) %
                        Parameters.World.WorldWidth;
                pos.Y = (pos.Y % Parameters.World.WorldHeight + Parameters.World.WorldHeight) %
                        Parameters.World.WorldHeight;

                var plant = new Plant(pos, _random, this);
                Plants.Add(plant.Id, plant);
            }
        }
    }

    private void PopulateCreatures()
    {
        for (var i = 0; i < Parameters.Population.InitialCreatureCount; i++)
        {
            var position = new Vector2(
                (float)_random.NextDouble() * Parameters.World.WorldWidth,
                (float)_random.NextDouble() * Parameters.World.WorldHeight);
            var creature = new SimpleCreature(position, 15f, 5f, _random, this);
            Creatures.Add(creature.Id, creature);
        }
    }

    public void Update(float dt)
    {
        dt *= Parameters.SimulationSpeed;

        EvaluateForces();

        Dictionary<int, JetForces> forcesBatch;
        lock (_jetForcesLock)
        {
            forcesBatch = new Dictionary<int, JetForces>(_jetForces);
        }

        foreach (var creature in new List<Creature>(Creatures.Values))
        {
            var forces = forcesBatch.TryGetValue(creature.Id, out var f) ? f : new JetForces(0, 0, 0, 0, 0, 0);
            creature.Update(dt, forces);
        }

        foreach (var creature in _deadCreatures.Values)
            Creatures.Remove(creature.Id);

        _deadCreatures.Clear();

        foreach (var plant in new List<Plant>(Plants.Values))
            plant.Update(dt);

        foreach (var plant in _deadPlants.Values)
            Plants.Remove(plant.Id);

        _deadPlants.Clear();
    }

    private void EvaluateForces()
    {
        Task.Run(async () =>
        {
            var sensors = new Dictionary<int, Sensors>();
            foreach (var creature in Creatures.Values)
                sensors[creature.Id] = creature.ReadSensors();

            try
            {
                var forces = await _client.EvaluateBrainsAsync(sensors);
                lock (_jetForcesLock)
                {
                    _jetForces = forces;
                }
            }
            catch (Exception)
            {
                // ignored
            }
        });
    }

    public void KillCreature(Creature creature)
    {
        _deadCreatures.TryAdd(creature.Id, creature);
    }

    public void KillPlant(Plant plant)
    {
        _deadPlants.TryAdd(plant.Id, plant);
    }

    public void AddCreature(Creature creature)
    {
        Creatures.Add(creature.Id, creature);
    }

    public void AddPlant(Plant plant)
    {
        Plants.Add(plant.Id, plant);
    }

    public int GetNextCreatureId()
    {
        return _nextCreatureId++;
    }

    public Plant GetPlantAtPosition(Vector2 position, float radius)
    {
        return Plants.Values.FirstOrDefault(plant =>
            plant.Position.TorusDistance(position, Parameters.World.WorldWidth, Parameters.World.WorldHeight) <=
            radius);
    }


    public int GetNextPlantId()
    {
        return _nextPlantId++;
    }

    public Plant GetNearestPlant(Vector2 position)
    {
        return Plants.Values.MinBy(plant =>
            plant.Position.TorusDistance(position, Parameters.World.WorldWidth, Parameters.World.WorldHeight));
    }

    public Creature GetNearestCreature(Vector2 position, int excludeId)
    {
        return Creatures.Values
            .Where(c => c.Id != excludeId)
            .MinBy(c => c.Position.TorusDistance(position, Parameters.World.WorldWidth, Parameters.World.WorldHeight));
    }
}