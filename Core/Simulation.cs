using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EvolutionSim.Configuration;
using Microsoft.Xna.Framework;

namespace EvolutionSim.Core;

public class Simulation
{
    private const float GridCellSize = 50f;
    private readonly SpatialHashGrid<Creature> _creatureGrid;
    private readonly Dictionary<int, Creature> _deadCreatures;
    private readonly Dictionary<int, Plant> _deadPlants;
    private readonly object _forcesLock = new();

    private readonly SpatialHashGrid<Plant> _plantGrid;
    private readonly Random _random;
    private Dictionary<int, JetForces> _forces = new();
    private int _nextCreatureId;
    private int _nextPlantId;

    public Simulation(SimulationParameters parameters, Random random)
    {
        _random = random;
        Parameters = parameters;

        Creatures = new Dictionary<int, Creature>();
        Plants = new Dictionary<int, Plant>();
        _deadCreatures = new Dictionary<int, Creature>();
        _deadPlants = new Dictionary<int, Plant>();

        Client = new BrainClient("localhost", 5000);

        PlantSpawner = new PlantSpawner(this, _random);
        PlantSpawner.PopulateInitialPlants();

        _plantGrid = new SpatialHashGrid<Plant>(
            GridCellSize,
            plant => plant.Position,
            Parameters.World.WorldWidth,
            Parameters.World.WorldHeight);

        _creatureGrid = new SpatialHashGrid<Creature>(
            GridCellSize,
            creature => creature.Position,
            Parameters.World.WorldWidth,
            Parameters.World.WorldHeight);

        PopulateCreatures();
        InitializeBrains();
    }

    public BrainClient Client { get; init; }

    public SimulationParameters Parameters { get; }
    public Dictionary<int, Creature> Creatures { get; }
    public Dictionary<int, Plant> Plants { get; }

    public PlantSpawner PlantSpawner { get; }

    private void PopulateCreatures()
    {
        for (var i = 0; i < Parameters.Population.InitialCreatureCount; i++)
        {
            var position = GetRandomVector2();
            var creature = new SimpleCreature(position, 15f, 5f, _random, this);
            Creatures.Add(creature.Id, creature);
        }

        for (var i = 0; i < Parameters.Population.InitialParasiteCount; i++)
        {
            var position = GetRandomVector2();
            var parasite = new ParasiteCreature(position, _random, this);
            Creatures.Add(parasite.Id, parasite);
        }
    }

    public void Update(float dt)
    {
        dt *= Parameters.SimulationSpeed;

        _plantGrid.Rebuild(Plants.Values);
        _creatureGrid.Rebuild(Creatures.Values);

        SetForces();
        
        TrainCreatures(dt);

        UpdatePlants(dt);
        
        CleanupDeadEntities();
    }

    private void TrainCreatures(float dt)
    {
        List<BrainTransition> transitions = new List<BrainTransition>();
        foreach (var creature in Creatures.Values.ToList())
        {
            var forces = _forces.TryGetValue(creature.Id, out var f) ? f : new JetForces(0, 0, 0);
            creature.Update(dt, forces);
            var transition = creature.BuildTransition(dt);
            transitions.Add(transition);
        }

        if (transitions.Count > 0)
        {
            _ = Client.TrainAsync(transitions);
        }
    }

    private void SetForces()
    {
        Task.Run(async () =>
        {
            var sensors = new Dictionary<int, Sensors>();
            foreach (var creature in Creatures.Values)
                sensors[creature.Id] = creature.ReadSensors();
            
            try
            {
                var forces = await Client.EvaluateSensorsAsync(sensors);
                lock (_forcesLock)
                {
                    _forces = forces;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error evaluating sensors: " + ex.Message);
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

    public Plant? GetPlantAtPosition(Vector2 position, float radius)
    {
        var candidates = _plantGrid.Query(position, radius);
        return candidates.FirstOrDefault(plant =>
            plant.Position.TorusDistance(position, Parameters.World.WorldWidth, Parameters.World.WorldHeight) <=
            radius);
    }

    public int GetNextPlantId()
    {
        return _nextPlantId++;
    }

    public List<Creature> GetNearbyCreatures<T>(Vector2 position, float radius, int excludeId = -1)
    {
        return GetNearbyCreatures(position, radius, excludeId)
            .Where(c => c is T)
            .ToList();
    }

    public List<Creature> GetNearbyCreatures(Vector2 position, float radius, int excludeId = -1)
    {
        var candidates = _creatureGrid.Query(position, radius)
            .Where(c =>
                c.Position.TorusDistance(position, Parameters.World.WorldWidth, Parameters.World.WorldHeight) <=
                radius);

        return excludeId >= 0
            ? candidates.Where(c => c.Id != excludeId).ToList()
            : candidates.ToList();
    }

    private void UpdatePlants(float dt)
    {
        PlantSpawner.DisperseSeeds(dt);
        foreach (var plant in Plants.Values.ToList()) 
            plant.Update(dt);
    }

    private void CleanupDeadEntities()
    {
        foreach (var plant in _deadPlants.Values)
            Plants.Remove(plant.Id);
        _deadPlants.Clear();

        foreach (var creature in _deadCreatures.Values)
            Creatures.Remove(creature.Id);
        _deadCreatures.Clear();
    }

    private Vector2 GetRandomVector2()
    {
        return new Vector2(
            _random.Next(Parameters.World.WorldWidth),
            _random.Next(Parameters.World.WorldHeight)
        );
    }

    public List<Plant> GetPlantsInRange(Vector2 position, float range)
    {
        var candidates = _plantGrid.Query(position, range);
        return candidates.Where(plant =>
            plant.Position.TorusDistance(position, Parameters.World.WorldWidth, Parameters.World.WorldHeight) <= range
        ).ToList();
    }

    private void InitializeBrains()
    {
        Task.Run(async () =>
        {
            var brainWeights = Creatures.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Genome.BrainWeights
            );
            try
            {
                await Client.InitBrainsAsync(brainWeights);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error initializing brains: " + ex.Message);
            }
        });
    }
}