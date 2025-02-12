using System;
using EvolutionSim;
using EvolutionSim.Core;

static SimulationParameters CreateSimulationParameters()
{
    return new SimulationParameters
    {
        World = new WorldParameters
        {
            WorldWidth = 1200,
            WorldHeight = 800
        },
        MutationRate = 0.02f,
        SimulationSpeed = 15.0f,
        Population = new PopulationParameters
        {
            InitialCreatureCount = 100,
            InitialParasiteCount = 10,
            InitialPlantClusters = 20,
            MaxPlantsPerCluster = 15,
            GlobalMaxPlantCount = 500,
            InitialPlantClusterRadius = 10f
        },
        Creature = new CreatureParameters
        {
            MovementEnergyCostFactor = 0.01f,
            ReproductionEnergyThreshold = 60,
            DragCoefficient = 0.1f,
            AngularDragCoefficient = 0f
        },
        Plant = new PlantParameters
        {
            EnergyGain = 12f,
            ReproductionProbability = 1f
        },
        Render = new RenderParameters
        {
            CreatureRenderRadius = 5,
            PlantRenderRadius = 3
        },
        Physics = new PhysicsParameters
        {
            LinearForceScaling = 100f,
            AngularForceScaling = 20f,
            LinearDragCoefficient = 0.3f,
            TorqueDragCoefficient = 50f,
            JetCooldown = 0.05f
        }
    };
}

var simParams = CreateSimulationParameters();

new Game1(simParams, new Random()).Run();