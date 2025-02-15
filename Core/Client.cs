using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;

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
    private readonly NetworkStream _stream;
    private readonly SemaphoreSlim _requestLock = new(1, 1);
    private readonly TcpClient _tcpClient;

    public BrainClient(string host, int port)
    {
        _tcpClient = new TcpClient(host, port);
        _stream = _tcpClient.GetStream();
    }

    public void Dispose()
    {
        _stream.Dispose();
        _tcpClient.Close();
    }

    public async Task<Dictionary<int, JetForces>> EvaluateSensorsAsync(Dictionary<int, Sensors> sensorBatch)
    {
        var sensors = new List<object>();
        foreach (var kvp in sensorBatch)
        {
            var id = kvp.Key;
            sensors.Add(new
            {
                id,
                kvp.Value.PlantRetina,
                kvp.Value.NonParasiteCreatureRetina,
                kvp.Value.ParasiteCreatureRetina,
                kvp.Value.Energy
            });
        }

        var batchMessage = new
        {
            type = "evaluate",
            sensors
        };

        await _requestLock.WaitAsync();
        try
        {
            await WriteMessageAsync(batchMessage);
            var response = await ReadMessageAsync<Response>();
            if (response.Status != "ok")
                throw new Exception("Invalid response from server.");
            
            return response.Results;
        }
        finally
        {
            _requestLock.Release();
        }
    }


    public async Task InitBrainsAsync(Dictionary<int, float[]> brainWeights)
    {
        var initBatchMessage = new
        {
            type = "init",
            brains = brainWeights.Select(kvp => new { Id = kvp.Key, Weights = kvp.Value })
        };

        await _requestLock.WaitAsync();
        try
        {
            await WriteMessageAsync(initBatchMessage);
            var response = await ReadMessageAsync<Response>();
            if (!response.Status.Equals("ok", StringComparison.OrdinalIgnoreCase))
                throw new Exception("Batch initialization failed: " + response.Error);
        }
        finally
        {
            _requestLock.Release();
        }
    }

    public async Task TrainAsync(List<BrainTransition> transitions)
    {
        var trainingBatchMessage = new
        {
            type = "train",
            training = transitions.Select(t => new {
                id = t.Id,
                state = t.State,
                action = t.Action,
                reward = t.Reward,
                next_state = t.NextState,
                done = t.Done
            }).ToList()
        };

        await _requestLock.WaitAsync();
        try
        {
            await WriteMessageAsync(trainingBatchMessage);
            var response = await ReadMessageAsync<Response>();
            if (!response.Status.Equals("ok", StringComparison.OrdinalIgnoreCase))
                throw new Exception("Training failed: " + response.Error);
        }
        finally
        {
            _requestLock.Release();
        }
    }

    private async Task WriteMessageAsync<T>(T message)
    {
        byte[] bytes = MessagePackSerializer.Serialize(message, MessagePack.Resolvers.ContractlessStandardResolver.Options);
        byte[] lengthBytes = BitConverter.GetBytes(bytes.Length);
        await _stream.WriteAsync(lengthBytes, 0, lengthBytes.Length);
        await _stream.WriteAsync(bytes, 0, bytes.Length);
    }

    private async Task<T> ReadMessageAsync<T>()
    {
        byte[] lengthBytes = new byte[4];
        int read = await _stream.ReadAsync(lengthBytes, 0, lengthBytes.Length);
        if (read < 4)
        {
            throw new Exception("Failed to read the full message length.");
        }
        int messageLength = BitConverter.ToInt32(lengthBytes, 0);
        byte[] messageBytes = new byte[messageLength];
        int offset = 0;
        while (offset < messageLength)
        {
            int r = await _stream.ReadAsync(messageBytes, offset, messageLength - offset);
            if (r == 0)
            {
                throw new Exception("Connection closed while waiting for a complete message.");
            }
            offset += r;
        }
        return MessagePackSerializer.Deserialize<T>(messageBytes, MessagePack.Resolvers.ContractlessStandardResolver.Options);
    }

    [MessagePackObject(keyAsPropertyName: true)]
    public class Response
    {
        public string Status { get; set; } = null!;
        public Dictionary<int, JetForces> Results { get; set; } = new();
        public string Error { get; set; } = string.Empty;
    }
}