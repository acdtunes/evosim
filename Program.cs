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

    SimulationSpeed = 6.0f,

    Population = new PopulationParameters
    {
        InitialCreatureCount = 20,
        InitialParasiteCount = 60,
        InitialPlantClusters = 10,
        MaxPlantsPerCluster = 60,
        GlobalMaxPlantCount = 500,
        InitialPlantClusterRadius = 20f
    },

    Creature = new CreatureParameters
    {
        MovementEnergyCostFactor = 0.1f,
        ReproductionEnergyThreshold = 50,
        DragCoefficient = 0.1f,
        AngularDragCoefficient = 0f
    },

    Plant = new PlantParameters
    {
        EnergyGain = 10f,
        ReproductionProbability = 0.5f
    },

    Render = new RenderParameters
    {
        CreatureRenderRadius = 5,
        PlantRenderRadius = 2
    },

    Physics = new PhysicsParameters
    {
        LinearForceScaling = 10000f,
        AngularForceScaling = 2000f,
        LinearDragCoefficient = 0.5f,
        TorqueDragCoefficient = 20f,
        JetCooldown = 0.02f
    }
};

new Game1(simParams, new Random()).Run();