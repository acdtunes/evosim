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
        _reader.Dispose();
        _writer.Dispose();
        _tcpClient.Close();
    }

    public async Task<Dictionary<int, JetForces>> EvaluateSensorsAsync(Dictionary<int, Sensors> sensorBatch)
    {
        var sensors = new List<object>();
        foreach (var kvp in sensorBatch)
        {
            var id = kvp.Key;
            var s = kvp.Value;
            sensors.Add(new
            {
                id,
                s.PlantRetina,
                s.NonParasiteCreatureRetina,
                s.ParasiteCreatureRetina,
                s.Energy
            });
        }

        var batchMessage = new
        {
            type = "evaluate",
            sensors
        };

        var json = JsonConvert.SerializeObject(batchMessage);

        await _requestLock.WaitAsync();
        try
        {
            await _writer.WriteLineAsync(json);
            var responseLine = await _reader.ReadLineAsync();
            if (string.IsNullOrEmpty(responseLine))
                throw new Exception("Received empty response from RL server during evaluation.");

            var response = JsonConvert.DeserializeObject<EvaluationResponse>(responseLine);
            if (response?.Results == null)
                throw new Exception("Invalid response from server: " + responseLine);

            return response.Results;
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
            weights = brainWeights
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

    public async Task TrainBrainAsync(List<BrainTransition> transitions)
    {
        var trainMessage = new
        {
            type = "train",
            training = transitions
        };

        var json = JsonConvert.SerializeObject(trainMessage);

        await _requestLock.WaitAsync();
        try
        {
            await _writer.WriteLineAsync(json);
            var responseLine = await _reader.ReadLineAsync();
            if (string.IsNullOrEmpty(responseLine))
                throw new Exception("Received empty response from RL server during training.");
        }
        finally
        {
            _requestLock.Release();
        }
    }

    private class EvaluationResponse
    {
        public string Status { get; set; } = null!;
        public Dictionary<int, JetForces> Results { get; set; } = new();

        public string Error { get; set; } = string.Empty;
    }
}