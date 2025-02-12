namespace EvolutionSim.Core;

public class SimulationParameters
{
    public WorldParameters World { get; set; } = new WorldParameters();

    public PopulationParameters Population { get; set; } = new PopulationParameters();

    public float SimulationSpeed { get; set; } = 1.0f;

    public float MutationRate { get; set; } = 0.02f;

    public CreatureParameters Creature { get; set; } = new CreatureParameters();

    public PlantParameters Plant { get; set; } = new PlantParameters();

    public RenderParameters Render { get; set; } = new RenderParameters();
}

public class WorldParameters
{
    public int WorldWidth { get; set; } = 1200;
    public int WorldHeight { get; set; } = 1200;
}

public class PopulationParameters
{
    public int InitialCreatureCount { get; set; } = 100;
    public int InitialPlantCount { get; set; } = 300;
    public int MaxPlantCount { get; set; } = 100;
    public int InitialPlantClusters { get; set; }
    public int MaxPlantsPerCluster { get; set; }
    public int GlobalMaxPlantCount { get; set; }
    public float InitialPlantClusterRadius { get; set; }
}

public class CreatureParameters
{
    public float MovementEnergyCostFactor { get; set; } = 0.0001f;
    public float ReproductionEnergyThreshold { get; set; } = 5f;
    public float DragCoefficient { get; set; }
    public float AngularDragCoefficient { get; set; }
}

public class PlantParameters
{
    public float EnergyGain { get; set; } = 30f;
    public float ReproductionProbability { get; set; } = 0.05f;
}

public class RenderParameters
{
    public int CreatureRenderRadius { get; set; } = 5;
    public int PlantRenderRadius { get; set; } = 2;
}