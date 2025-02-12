using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace EvolutionSim.Core
{
    /// <summary>
    /// Updated BrainClient that decouples network code from neural network evaluation.
    /// Creatures are initialized in batch (sending their genome weights once) and later
    /// evaluation of sensor data is batched as well.
    /// </summary>
    public class BrainClient : IDisposable
    {
        private readonly TcpClient _tcpClient;
        private readonly StreamReader _reader;
        private readonly StreamWriter _writer;
        private readonly object _lock = new();

        public BrainClient(string host, int port)
        {
            _tcpClient = new TcpClient(host, port);
            var stream = _tcpClient.GetStream();
            _reader = new StreamReader(stream);
            _writer = new StreamWriter(stream) { AutoFlush = true };
        }

        /// <summary>
        /// Sends a batched initialization message containing all creatures’ genome weights.
        /// Each entry must contain an id and a BrainWeights array (330 floats).
        /// </summary>
        /// <param name="brains">Mapping from creature id to its genome weights.</param>
        /// <returns>
        /// A dictionary mapping creature id to a boolean indicating whether initialization
        /// succeeded for that creature.
        /// </returns>
        public async Task<Dictionary<int, bool>> InitializeBrainsAsync(Dictionary<int, float[]> brains)
        {
            var brainList = new List<object>();
            foreach (var kvp in brains)
            {
                brainList.Add(new
                {
                    id = kvp.Key,
                    BrainWeights = kvp.Value
                });
            }

            var batchMessage = new
            {
                type = "init",
                brains = brainList
            };

            var json = JsonConvert.SerializeObject(batchMessage);
            lock (_lock)
            {
                _writer.WriteLine(json);
            }

            string responseLine = await _reader.ReadLineAsync();
            if (string.IsNullOrEmpty(responseLine))
                throw new Exception("Received empty response from neural network server during initialization.");

            var responseObj = JsonConvert.DeserializeObject<InitResponse>(responseLine);
            if (responseObj.results == null)
                throw new Exception("Invalid response from server: " + responseLine);

            return responseObj.results;
        }

        public async Task<Dictionary<int, JetForces>> EvaluateBrainsAsync(Dictionary<int, Sensors> sensorBatch)
        {
            var sensorList = new List<object>();
            foreach (var kvp in sensorBatch)
            {
                int creatureId = kvp.Key;
                Sensors s = kvp.Value;
                sensorList.Add(new
                {
                    id = creatureId,
                    s.PlantNormalizedDistance,
                    s.PlantAngleSin,
                    s.PlantAngleCos,
                    s.CreatureNormalizedDistance,
                    s.CreatureAngleSin,
                    s.CreatureAngleCos,
                    s.Hunger
                });
            }

            var batchMessage = new
            {
                type = "evaluate",
                sensors = sensorList
            };

            string json = JsonConvert.SerializeObject(batchMessage);
            lock (_lock)
            {
                _writer.WriteLine(json);
            }

            string responseLine = await _reader.ReadLineAsync();
            if (string.IsNullOrEmpty(responseLine))
                throw new Exception("Received empty response from neural network server during evaluation.");

            var batchResponse = JsonConvert.DeserializeObject<EvaluationResponse>(responseLine);
            if (batchResponse.results == null)
                throw new Exception("Invalid response from server: " + responseLine);

            return batchResponse.results;
        }

        public void Dispose()
        {
            _reader?.Dispose();
            _writer?.Dispose();
            _tcpClient?.Close();
        }

        private class InitResponse
        {
            public string status { get; set; }
            public Dictionary<int, bool> results { get; set; }
            
            public string error { get; set; }
        }

        private class EvaluationResponse
        {
            public string status { get; set; }
            public Dictionary<int, JetForces> results { get; set; }
            
            public string error { get; set; }
        }
    }
}