using System;
using Microsoft.Xna.Framework;

namespace EvolutionSim.Core;

public class Brain(int inputCount, int hiddenCount, int outputCount, float[] weights)
{
    private readonly NeuralNetwork _network = new(inputCount, hiddenCount, outputCount, weights);

    public JetForces Evaluate(Sensors sensors)
    {
        var inputs = new[]
        {
            sensors.PlantNormalizedDistance,
            sensors.PlantAngleSin,
            sensors.PlantAngleCos,
            sensors.CreatureNormalizedDistance,
            sensors.CreatureAngleSin,
            sensors.CreatureAngleCos,
            sensors.Hunger
        };

        var outputs = _network.Evaluate(inputs);

        if (outputs.Length < 6)
            throw new Exception("Neural network output must have at least 6 values.");

        return new JetForces(
            MathHelper.Clamp(outputs[0], 0f, 1f),
            MathHelper.Clamp(outputs[1], 0f, 1f),
            MathHelper.Clamp(outputs[2], 0f, 1f),
            MathHelper.Clamp(outputs[3], 0f, 1f),
            MathHelper.Clamp(outputs[4], 0f, 1f),
            MathHelper.Clamp(outputs[5], 0f, 1f)
        );
    }
}