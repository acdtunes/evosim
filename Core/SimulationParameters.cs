namespace EvolutionSim.Core;

public class SimulationParameters
{
    public WorldParameters World { get; set; }

    public PopulationParameters Population { get; set; }

    public float SimulationSpeed { get; set; }

    public float MutationRate { get; set; }

    public CreatureParameters Creature { get; set; }

    public PlantParameters Plant { get; set; }

    public RenderParameters Render { get; set; }

    public PhysicsParameters Physics { get; set; }
}

public class WorldParameters
{
    public int WorldWidth { get; set; }
    public int WorldHeight { get; set; }
}

public class PopulationParameters
{
    public int InitialCreatureCount { get; set; }
    public int InitialPlantCount { get; set; }
    public int MaxPlantCount { get; set; }
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
    public float DragCoefficient { get; set; }
    public float AngularDragCoefficient { get; set; }
}

public class PlantParameters
{
    public float EnergyGain { get; set; }
    public float ReproductionProbability { get; set; }
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