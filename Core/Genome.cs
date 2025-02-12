using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace EvolutionSim.Core;

public class Genome
{
    public const int InputCount = 6;
    public const int HiddenCount = 12;
    public const int OutputCount = 6;
    private const int TotalBrainWeights = (InputCount + 1) * HiddenCount + (HiddenCount + 1) * HiddenCount + (HiddenCount + 1) * OutputCount;

    private static readonly Dictionary<string, (float min, float max)> GeneRanges = new()
    {
        { "MaxSpeed", (30f, 100f) },
        { "EnergyStorage", (100f, 200f) },
        { "MetabolicRate", (0.1f, 2.0f) },
        { "MaturityAge", (50f, 100f) },
        { "ReproductiveCost", (0.01f, 0.1f) },
        { "Lifespan", (500f, 1000f) },
        { "ForagingRange", (20f, 150f) },
        { "MovementEfficiency", (0.8f, 1.2f) },
        { "MaxAngularThrust", (2f, 10f) },
        { "MaxThrust", (50f, 200f) }
    };

    private readonly float _mutationRate;
    private readonly Random _random;

    public Genome(Random random, float mutationRate)
    {
        _random = random;
        _mutationRate = mutationRate;

        MaxSpeed = RandomRange("MaxSpeed");
        EnergyStorage = RandomRange("EnergyStorage");
        MetabolicRate = RandomRange("MetabolicRate");
        MaturityAge = RandomRange("MaturityAge");
        Lifespan = RandomRange("Lifespan");
        ForagingRange = RandomRange("ForagingRange");
        ReproductiveCost = RandomRange("ReproductiveCost");
        MovementEfficiency = RandomRange("MovementEfficiency");
        MaxAngularThrust = RandomRange("MaxAngularThrust");
        MaxThrust = RandomRange("MaxThrust");

        BrainWeights = new float[TotalBrainWeights];
        // Scale weights by 0.1 to avoid saturation in the neural network
        for (var i = 0; i < BrainWeights.Length; i++) 
            BrainWeights[i] = (float)((_random.NextDouble() * 2 - 1) * 0.1);
    }

    private Genome(float maxSpeed, float energyStorage, float metabolicRate, float maturityAge, float reproductiveCost,
        float lifespan, float foragingRange, float mutationRate, float movementEfficiency, float maxThrust, float maxAngularThrust, Random random, float[] brainWeights)
    {
        _mutationRate = mutationRate;
        _random = random;
        MaxSpeed = maxSpeed;
        EnergyStorage = energyStorage;
        MetabolicRate = metabolicRate;
        MaturityAge = maturityAge;
        ReproductiveCost = reproductiveCost;
        Lifespan = lifespan;
        ForagingRange = foragingRange;
        MovementEfficiency = movementEfficiency;
        MaxThrust = maxThrust;
        MaxAngularThrust = maxAngularThrust;
        BrainWeights = brainWeights;
    }

    public float MaxSpeed { get; }
    public float EnergyStorage { get; }
    public float MetabolicRate { get; }
    public float MaturityAge { get; }
    public float ReproductiveCost { get; set; }
    public float Lifespan { get; }
    public float ForagingRange { get; }
    
    public float MaxThrust { get; set; }
    
    public float MaxAngularThrust { get; set; }
    
    public float MovementEfficiency { get; }

    public float[] BrainWeights { get; }

    private float RandomRange(string gene)
    {
        var range = GeneRanges[gene];
        var mean = (range.min + range.max) / 2;
        var stddev = (range.max - range.min) / 6f;

        var u1 = 1.0 - _random.NextDouble();
        var u2 = 1.0 - _random.NextDouble();
        var randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        var result = (float)(mean + stddev * randStdNormal);

        return MathHelper.Clamp(result, range.min, range.max);
    }

    public Genome Clone()
    {
        var brainWeightsCopy = (float[])BrainWeights.Clone();
        return new Genome(MaxSpeed, EnergyStorage, MetabolicRate, MaturityAge, ReproductiveCost, Lifespan, ForagingRange, 
            _mutationRate, MovementEfficiency, MaxThrust, MaxAngularThrust, _random, brainWeightsCopy);
    }

    public Genome Mutate()
    {
        var newMaxSpeed = MutateGene(MaxSpeed, "MaxSpeed");
        var newEnergyStorage = MutateGene(EnergyStorage, "EnergyStorage");
        var newMetabolicRate = MutateGene(MetabolicRate, "MetabolicRate");
        var newMaturityAge = MutateGene(MaturityAge, "MaturityAge");
        var newLifespan = MutateGene(Lifespan, "Lifespan");
        var newForagingRange = MutateGene(ForagingRange, "ForagingRange");
        var newReproductiveCost = MutateGene(ReproductiveCost, "ReproductiveCost");

        var newBrainWeights = (float[])BrainWeights.Clone();
        for (var i = 0; i < newBrainWeights.Length; i++)
            if (_random.NextDouble() < _mutationRate)
            {
                var delta = (float)(0.1 * (_random.NextDouble() * 2 - 1));
                newBrainWeights[i] += delta;
                newBrainWeights[i] = MathHelper.Clamp(newBrainWeights[i], -1, 1);
            }

        return new Genome(newMaxSpeed, newEnergyStorage, newMetabolicRate, newMaturityAge, newReproductiveCost, newLifespan, 
            newForagingRange, _mutationRate, MovementEfficiency, MaxThrust, MaxAngularThrust, _random, newBrainWeights);
    }

    private float MutateGene(float geneValue, string gene)
    {
        if (_random.NextDouble() < _mutationRate)
        {
            var range = GeneRanges[gene];
            var delta = (float)(0.05f * (range.max - range.min) * (_random.NextDouble() * 2 - 1));
            geneValue += delta;
            geneValue = MathHelper.Clamp(geneValue, range.min, range.max);
        }

        return geneValue;
    }
}