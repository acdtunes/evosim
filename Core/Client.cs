using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace EvolutionSim.Core
{
    public class TransitionData
    {
        public int Id { get; set; }
        public float[] State { get; set; }      // 7–element sensor state
        public float[] Action { get; set; }     // 6–element jet–force action
        public float Reward { get; set; }
        public float[] NextState { get; set; }  // 7–element sensor state after update
        public bool Done { get; set; }
    }
    
    public class BrainClient : IDisposable
    {
        private readonly TcpClient _tcpClient;
        private readonly StreamReader _reader;
        private readonly StreamWriter _writer;
        // Lock to serialize read/write operations.
        private readonly SemaphoreSlim _requestLock = new SemaphoreSlim(1, 1);

        public BrainClient(string host, int port)
        {
            _tcpClient = new TcpClient(host, port);
            var stream = _tcpClient.GetStream();
            _reader = new StreamReader(stream);
            _writer = new StreamWriter(stream) { AutoFlush = true };
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

            await _requestLock.WaitAsync();
            try
            {
                _writer.WriteLine(json);
                string responseLine = await _reader.ReadLineAsync();
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

        public async Task<Dictionary<int, object>> TrainBrainsAsync(List<TransitionData> trainingData)
        {
            var batchMessage = new
            {
                type = "train",
                training = trainingData
            };

            string json = JsonConvert.SerializeObject(batchMessage);

            await _requestLock.WaitAsync();
            try
            {
                _writer.WriteLine(json);
                string responseLine = await _reader.ReadLineAsync();
                if (string.IsNullOrEmpty(responseLine))
                    throw new Exception("Received empty response from RL server during training.");

                var responseObj = JsonConvert.DeserializeObject<TrainResponse>(responseLine);
                if (responseObj.info == null)
                    throw new Exception("Invalid response from server: " + responseLine);

                return responseObj.info;
            }
            finally
            {
                _requestLock.Release();
            }
        }

        public void Dispose()
        {
            _reader?.Dispose();
            _writer?.Dispose();
            _tcpClient?.Close();
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
}