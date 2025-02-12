using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace EvolutionSim.Core;

public class Genome
{
    public const int InputCount = 7;
    public const int HiddenCount = 12;
    public const int OutputCount = 6;

    private const int TotalBrainWeights = (InputCount + 1) * HiddenCount + (HiddenCount + 1) * HiddenCount +
                                          (HiddenCount + 1) * OutputCount;

    private static readonly Dictionary<string, (float min, float max)> GeneRanges = new()
    {
        { "EnergyStorage", (70f, 200f) },
        { "MetabolicRate", (0.1f, 2.0f) },
        { "ForagingRange", (100f, 300f) },
        { "Fullness", (0.7f, 1.0f) }
    };

    private readonly float _mutationRate;
    private readonly Random _random;

    public Genome(Random random, float mutationRate)
    {
        _random = random;
        _mutationRate = mutationRate;

        MetabolicRate = RandomRange("MetabolicRate");
        EnergyStorage = RandomRange("EnergyStorage");
        ForagingRange = RandomRange("ForagingRange");
        Fullness = RandomRange("Fullness");

        BrainWeights = new float[TotalBrainWeights];

        for (var i = 0; i < BrainWeights.Length; i++)
            BrainWeights[i] = (float)((_random.NextDouble() * 2 - 1) * 0.1);
    }

    private Genome(float energyStorage, float metabolicRate, float foragingRange, float fullness, float mutationRate,
        Random random, float[] brainWeights)
    {
        _mutationRate = mutationRate;
        _random = random;
        EnergyStorage = energyStorage;
        MetabolicRate = metabolicRate;
        ForagingRange = foragingRange;
        Fullness = fullness;
        BrainWeights = brainWeights;
    }

    public float EnergyStorage { get; }

    public float[] BrainWeights { get; }

    public float MetabolicRate { get; set; }
    public float ForagingRange { get; set; }
    public float Fullness { get; set; }


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
        return new Genome(EnergyStorage, MetabolicRate, ForagingRange, Fullness, _mutationRate, _random,
            (float[])BrainWeights.Clone());
    }

    public Genome Mutate()
    {
        var newEnergyStorage = MutateGene(EnergyStorage, "EnergyStorage");
        var newMetabolicRate = MutateGene(MetabolicRate, "MetabolicRate");
        var newForagingRange = MutateGene(ForagingRange, "ForagingRange");
        var newFullness = MutateGene(Fullness, "Fullness");

        var newBrainWeights = (float[])BrainWeights.Clone();
        for (var i = 0; i < newBrainWeights.Length; i++)
            if (_random.NextDouble() < _mutationRate)
            {
                var delta = (float)(0.1 * (_random.NextDouble() * 2 - 1));
                newBrainWeights[i] += delta;
                newBrainWeights[i] = MathHelper.Clamp(newBrainWeights[i], -1, 1);
            }

        return new Genome(newEnergyStorage, newMetabolicRate, newForagingRange, newFullness, _mutationRate, _random,
            newBrainWeights);
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