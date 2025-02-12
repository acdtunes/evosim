using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace EvolutionSim.Core;

public class BrainClient : IDisposable
{
    private readonly StreamReader _reader;
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

    public async Task<Dictionary<int, JetForces>> EvaluateBrainsBatchAsync(Dictionary<int, Sensors> sensorBatch)
    {
        var payload = new
        {
            sensors = sensorBatch.Select(kvp => new
            {
                Id = kvp.Key,
                kvp.Value.PlantNormalizedDistance,
                plant_anPlantAngleSingle_sin = kvp.Value.PlantAngleSin,
                kvp.Value.PlantAngleCos,
                kvp.Value.CreatureNormalizedDistance,
                kvp.Value.CreatureAngleSin,
                kvp.Value.CreatureAngleCos,
                kvp.Value.Hunger
            }).ToList()
        };

        var jsonPayload = JsonConvert.SerializeObject(payload);
        await _writer.WriteLineAsync(jsonPayload);

        var responseLine = await _reader.ReadLineAsync();
        if (string.IsNullOrEmpty(responseLine))
            throw new Exception("Received empty response from neural network server.");

        return JsonConvert.DeserializeObject<Dictionary<int, JetForces>>(responseLine);
    }
}