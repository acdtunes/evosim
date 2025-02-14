namespace EvolutionSim.Core;

public class SimulationParameters
{
    public WorldParameters World { get; set; } = new();

    public PopulationParameters Population { get; set; } = new();

    public float SimulationSpeed { get; set; }

    public float MutationRate { get; set; }

    public CreatureParameters Creature { get; set; } = new();

    public PlantParameters Plant { get; set; } = new();

    public RenderParameters Render { get; set; } = new();

    public PhysicsParameters Physics { get; set; } = new();

    public bool LearningEnabled { get; set; } = true;
}

public class WorldParameters
{
    public int WorldWidth { get; set; }
    public int WorldHeight { get; set; }
}

public class PopulationParameters
{
    public int InitialCreatureCount { get; set; }
    public int InitialPlantClusters { get; set; }
    public int MaxPlantsPerCluster { get; set; }
    public int GlobalMaxPlantCount { get; set; }
    public float InitialPlantClusterRadius { get; set; }
    public int InitialParasiteCount { get; set; }
}

public class CreatureParameters
{
    public float MovementEnergyCostFactor { get; set; }
    public float ReproductionEnergyThreshold { get; set; }
    public double ReproductionProbability { get; set; }
}

public class PlantParameters
{
    public float EnergyGain { get; set; }
    public float ReproductionProbability { get; set; }
    public float SeedMaturityThreshold { get; set; }
    public float SeedDispersalRadius { get; set; }
    public double SeedDispersalProbability { get; set; }
}

public class RenderParameters
{
    public int CreatureRenderRadius { get; set; }
    public int PlantRenderRadius { get; set; }
}

public class PhysicsParameters
{
    public float LinearDragCoefficient { get; set; }
    public float TorqueDragCoefficient { get; set; }
    public float JetCooldown { get; set; }
    public float LinearForceScaling { get; set; }
    public float AngularForceScaling { get; set; }
}