using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace EvolutionSim.Core;

public class Simulation
{
    private readonly Dictionary<int, Creature> _deadCreatures;
    private readonly Dictionary<int, Plant> _deadPlants;
    private readonly Random _random;
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

        for (var i = 0; i < Parameters.Population.InitialCreatureCount; i++)
        {
            var position = new Vector2(
                (float)_random.NextDouble() * Parameters.World.WorldWidth,
                (float)_random.NextDouble() * Parameters.World.WorldHeight);
            var creature = new SimpleCreature(position, 15f, 5f, _random, this);
            Creatures.Add(creature.Id, creature);
        }

        for (var cluster = 0; cluster < Parameters.Population.InitialPlantClusters; cluster++)
        {
            var clusterCenter = new Vector2(
                _random.Next(Parameters.World.WorldWidth),
                _random.Next(Parameters.World.WorldHeight)
            );

            int plantCount = _random.Next(1, Parameters.Population.MaxPlantsPerCluster + 1);

            for (int i = 0; i < plantCount; i++)
            {
                float angle = (float)(_random.NextDouble() * MathHelper.TwoPi);
                float distance = (float)(_random.NextDouble() * Parameters.Population.InitialPlantClusterRadius);
                Vector2 offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * distance;
                Vector2 pos = clusterCenter + offset;
                
                pos.X = (pos.X % Parameters.World.WorldWidth + Parameters.World.WorldWidth) % Parameters.World.WorldWidth;
                pos.Y = (pos.Y % Parameters.World.WorldHeight + Parameters.World.WorldHeight) % Parameters.World.WorldHeight;
                
                var plant = new Plant(pos, _random, this);
                Plants.Add(plant.Id, plant);
            }
        }
    }

    public SimulationParameters Parameters { get; }
    public Dictionary<int, Creature> Creatures { get; }
    public Dictionary<int, Plant> Plants { get; }

    public void Update(float dt)
    {
        dt *= Parameters.SimulationSpeed;

        var creatureCopy = new List<Creature>(Creatures.Values);
        foreach (var creature in creatureCopy) 
            creature.Update(dt);

        foreach (var creature in _deadCreatures.Values) 
            Creatures.Remove(creature.Id);
        
        _deadCreatures.Clear();

        var plantCopy = new List<Plant>(Plants.Values);
        foreach (var plant in plantCopy) 
            plant.Update(dt);

        foreach (var plant in _deadPlants.Values) 
            Plants.Remove(plant.Id);
        
        _deadPlants.Clear();
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
        Console.WriteLine($"Creature count: {Creatures.Count}");
    }

    public void AddPlant(Plant plant)
    {
        Plants.Add(plant.Id, plant);
        Console.WriteLine($"Plant count: {Plants.Count}");
    }

    public int GetNextCreatureId()
    {
        return _nextCreatureId++;
    }

    public Plant GetPlantAtPosition(Vector2 position, float radius)
    {
        return Plants.Values.FirstOrDefault(plant => plant.Position.TorusDistance(position, Parameters.World.WorldWidth, Parameters.World.WorldHeight) <= radius);
    }


    public int GetNextPlantId()
    {
        return _nextPlantId++;
    }

    public Plant GetNearestPlant(Vector2 position)
    {
        return Plants.Values.MinBy(plant => plant.Position.TorusDistance(position, Parameters.World.WorldWidth, Parameters.World.WorldHeight));
    }
    
    public Creature GetNearestCreature(Vector2 position, int excludeId)
    {
        return Creatures.Values
            .Where(c => c.Id != excludeId)
            .MinBy(c => c.Position.TorusDistance(position, Parameters.World.WorldWidth, Parameters.World.WorldHeight));
    }
}