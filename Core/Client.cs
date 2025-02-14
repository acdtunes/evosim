using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace EvolutionSim.Core;

public class BrainTransition
{
    public int Id { get; set; }
    public float[] State { get; set; } = null!;
    public float[] Action { get; set; } = null!;
    public float Reward { get; set; }
    public float[] NextState { get; set; } = null!;
    public bool Done { get; set; }
}

public class BrainClient : IDisposable
{
    private readonly StreamReader _reader;
    private readonly SemaphoreSlim _requestLock = new(1, 1);
    private readonly TcpClient _tcpClient;
    private readonly StreamWriter _writer;

    public BrainClient(string host, int port)
    {
        _tcpClient = new TcpClient(host, port);
        var stream = _tcpClient.GetStream();
        _reader = new StreamReader(stream);
        _writer = new StreamWriter(stream) { AutoFlush = true };
    }

    public void Dispose()
    {
        _reader?.Dispose();
        _writer?.Dispose();
        _tcpClient?.Close();
    }

    public async Task<Dictionary<int, JetForces>> EvaluateBrainsAsync(Dictionary<int, Sensors> sensorBatch)
    {
        var sensors = new List<object>();
        foreach (var kvp in sensorBatch)
        {
            var creatureId = kvp.Key;
            var s = kvp.Value;
            sensors.Add(new
            {
                id = creatureId,
                PlantNormalizedDistance = s.PlantVisionSensor.NormalizedDistance,
                PlantNormalizedAngleSin = s.PlantVisionSensor.NormalizedAngleSin,
                PlantNormalizedAngleCos = s.PlantVisionSensor.NormalizedAngleCos,
                CreatureNormalizedDistance = s.CreatureVisionSensor.NormalizedDistance,
                CreatureNormalizedAngleSin = s.CreatureVisionSensor.NormalizedAngleSin,
                CreatureNormalizedAngleCos = s.CreatureVisionSensor.NormalizedAngleCos,
                s.Energy
            });
        }

        var batchMessage = new
        {
            type = "evaluate", sensors
        };

        var json = JsonConvert.SerializeObject(batchMessage);

        await _requestLock.WaitAsync();
        try
        {
            await _writer.WriteLineAsync(json);
            var responseLine = await _reader.ReadLineAsync();
            if (string.IsNullOrEmpty(responseLine))
                throw new Exception("Received empty response from RL server during evaluation.");

            var responseObj = JsonConvert.DeserializeObject<EvaluationResponse>(responseLine);
            if (responseObj.results == null)
                throw new Exception("Invalid response from server: " + responseLine);

            return responseObj.results;
        }
        finally
        {
            _requestLock.Release();
        }
    }

    public async Task InitBrainAsync(int creatureId, float[] brainWeights)
    {
        var initMessage = new
        {
            type = "init",
            id = creatureId,
            brain_weights = brainWeights
        };

        var json = JsonConvert.SerializeObject(initMessage);

        await _requestLock.WaitAsync();
        try
        {
            await _writer.WriteLineAsync(json);
            var responseLine = await _reader.ReadLineAsync();
            if (string.IsNullOrEmpty(responseLine))
                throw new Exception("Received empty response from RL server during brain initialization.");
        }
        finally
        {
            _requestLock.Release();
        }
    }

    public async Task TrainBrainsAsync(List<BrainTransition> trainingData)
    {
        var batchMessage = new
        {
            type = "train",
            training = trainingData
        };

        var json = JsonConvert.SerializeObject(batchMessage);

        await _requestLock.WaitAsync();
        try
        {
            await _writer.WriteLineAsync(json);
            var responseLine = await _reader.ReadLineAsync();
            if (string.IsNullOrEmpty(responseLine))
                throw new Exception("Received empty response from RL server during training.");

            var responseObj = JsonConvert.DeserializeObject<TrainResponse>(responseLine);
            if (responseObj?.info == null)
                throw new Exception("Invalid response from server: " + responseLine);
        }
        finally
        {
            _requestLock.Release();
        }
    }

    private class EvaluationResponse
    {
        public string status { get; set; }
        public Dictionary<int, JetForces> results { get; set; }
        public string error { get; set; }
    }

    private class TrainResponse
    {
        public string status { get; set; }
        public Dictionary<int, object> info { get; set; }
        public string error { get; set; }
    }
}