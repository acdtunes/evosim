using System;
using EvolutionSim;
using EvolutionSim.Core;

var simParams = new SimulationParameters
{
    World = new WorldParameters
    {
        WorldWidth = 1200,
        WorldHeight = 800
    },

    MutationRate = 0.02f,

    SimulationSpeed = 1.0f,

    Population = new PopulationParameters
    {
        InitialCreatureCount = 20,
        InitialPlantClusters = 10,
        MaxPlantsPerCluster = 60,
        GlobalMaxPlantCount = 500,
        InitialPlantClusterRadius = 20f
    },

    Creature = new CreatureParameters
    {
        MovementEnergyCostFactor = 10f,
        ReproductionEnergyThreshold = 50,
        DragCoefficient = 0.1f,
        AngularDragCoefficient = 0f
    },

    Plant = new PlantParameters
    {
        EnergyGain = 20f,
        ReproductionProbability = 0.5f
    },

    Render = new RenderParameters
    {
        CreatureRenderRadius = 5,
        PlantRenderRadius = 2
    },

    Physics = new PhysicsParameters
    {
        ForceScaling = 10000f,
        LinearDragCoefficient = 0.2f,
        AngularDragCoefficient = 50f,
        JetCooldown = 0.2f
    }
};

new Game1(simParams, new Random()).Run();