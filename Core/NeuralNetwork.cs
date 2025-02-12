using System;
using System.Collections.Generic;

namespace EvolutionSim.Core;

public class NeuralNetwork
{
    private readonly int _hiddenCount;
    private readonly int _inputCount;
    private readonly int _outputCount;
    private readonly float[,] _weightsInputHidden;
    private readonly float[,] _weightsHiddenHidden;
    private readonly float[,] _weightsHiddenOutput;

    public NeuralNetwork(int inputCount, int hiddenCount, int outputCount, IReadOnlyList<float> weights)
    {
        _inputCount = inputCount;
        _hiddenCount = hiddenCount;
        _outputCount = outputCount;
        var expectedLength = (inputCount + 1) * hiddenCount + (hiddenCount + 1) * hiddenCount + (hiddenCount + 1) * outputCount;
        if (weights.Count != expectedLength)
            throw new ArgumentException($"Expected {expectedLength} weights, got {weights.Count}");

        _weightsInputHidden = new float[inputCount + 1, hiddenCount];
        _weightsHiddenHidden = new float[hiddenCount + 1, hiddenCount];
        _weightsHiddenOutput = new float[hiddenCount + 1, outputCount];
        var index = 0;

        for (var i = 0; i < inputCount + 1; i++)
        for (var j = 0; j < hiddenCount; j++)
            _weightsInputHidden[i, j] = weights[index++];

        for (var i = 0; i < hiddenCount + 1; i++)
        for (var j = 0; j < hiddenCount; j++)
            _weightsHiddenHidden[i, j] = weights[index++];

        for (var i = 0; i < hiddenCount + 1; i++)
        for (var j = 0; j < outputCount; j++)
            _weightsHiddenOutput[i, j] = weights[index++];
    }

    public float[] Evaluate(float[] inputs)
    {
        if (inputs.Length != _inputCount)
            throw new ArgumentException($"Expected {_inputCount} inputs, got {inputs.Length}");

        var hidden1 = new float[_hiddenCount];
        for (var j = 0; j < _hiddenCount; j++)
        {
            var sum = 0f;
            for (var i = 0; i < _inputCount; i++) sum += inputs[i] * _weightsInputHidden[i, j];
            sum += 1f * _weightsInputHidden[_inputCount, j];
            hidden1[j] = (float)Math.Tanh(sum);
        }

        var hidden2 = new float[_hiddenCount];
        for (var j = 0; j < _hiddenCount; j++)
        {
            var sum = 0f;
            for (var i = 0; i < _hiddenCount; i++) sum += hidden1[i] * _weightsHiddenHidden[i, j];
            sum += 1f * _weightsHiddenHidden[_hiddenCount, j];
            hidden2[j] = (float)Math.Tanh(sum);
        }

        var output = new float[_outputCount];
        for (var k = 0; k < _outputCount; k++)
        {
            var sum = 0f;
            for (var j = 0; j < _hiddenCount; j++) sum += hidden2[j] * _weightsHiddenOutput[j, k];
            sum += 1f * _weightsHiddenOutput[_hiddenCount, k];
            output[k] = (float)Math.Tanh(sum);
        }

        return output;
    }
}