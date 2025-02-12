import socket
import json
import threading

import torch
import torch.nn as nn
import torch.nn.functional as F

# =============================
# Neural Network Code (PyTorch)
# =============================

class SimpleNet(nn.Module):
    """
    A simple feed-forward network with architecture:
      Input (7) -> fc1 (12) -> tanh -> fc2 (12) -> tanh -> fc3 (6) -> sigmoid
      
    The network’s parameters are initialized from a flat list of 330 weights:
      - fc1: 7 weights per neuron + bias (12 neurons): 7*12 + 12 = 84 + 12 = 96
      - fc2: 12 weights per neuron + bias (12 neurons): 12*12 + 12 = 144 + 12 = 156
      - fc3: 12 weights per neuron + bias (6 neurons): 12*6 + 6 = 72 + 6 = 78
      Total: 96 + 156 + 78 = 330.
    """
    def __init__(self, genome_weights):
        super(SimpleNet, self).__init__()
        self.fc1 = nn.Linear(7, 12)  # automatically creates bias parameters
        self.fc2 = nn.Linear(12, 12)
        self.fc3 = nn.Linear(12, 6)
        self.load_genome_weights(genome_weights)

    def load_genome_weights(self, genome_weights):
        # Convert to a tensor if necessary.
        if not isinstance(genome_weights, torch.Tensor):
            genome_weights = torch.tensor(genome_weights, dtype=torch.float32)
        if genome_weights.numel() != 330:
            raise ValueError(f"Expected 330 weights, got {genome_weights.numel()}")
        
        # fc1: first 96 numbers: 84 for weights (12 x 7) and 12 for biases.
        fc1_flat = genome_weights[:96]
        fc1_weight = fc1_flat[:84].reshape(12, 7)
        fc1_bias = fc1_flat[84:96]
        self.fc1.weight.data = fc1_weight
        self.fc1.bias.data = fc1_bias

        # fc2: next 156 numbers: 144 for weights (12 x 12) and 12 for biases.
        fc2_flat = genome_weights[96:96+156]
        fc2_weight = fc2_flat[:144].reshape(12, 12)
        fc2_bias = fc2_flat[144:156]
        self.fc2.weight.data = fc2_weight
        self.fc2.bias.data = fc2_bias

        # fc3: final 78 numbers: 72 for weights (6 x 12) and 6 for biases.
        fc3_flat = genome_weights[96+156:]
        fc3_weight = fc3_flat[:72].reshape(6, 12)
        fc3_bias = fc3_flat[72:78]
        self.fc3.weight.data = fc3_weight
        self.fc3.bias.data = fc3_bias

    def forward(self, x):
        x = torch.tanh(self.fc1(x))
        x = torch.tanh(self.fc2(x))
        x = torch.sigmoid(self.fc3(x))
        return x

class NeuralNetworkManager:
    """
    Manages a cache of neural network models (one per creature). Each creature’s model is 
    built once during the initialization phase (via an "initBatch" message) and is cached
    for subsequent sensor evaluations.
    """
    def __init__(self):
        self.models = {}  # maps creature id to SimpleNet instances
        self.lock = threading.Lock()

    def register_model(self, creature_id, genome_weights):
        with self.lock:
            try:
                model = SimpleNet(genome_weights)
                self.models[creature_id] = model
                return True
            except Exception as e:
                print(f"Error registering model for creature {creature_id}: {e}")
                return False

    def evaluate(self, creature_id, sensor_input):
        with self.lock:
            if creature_id not in self.models:
                raise ValueError(f"Model for creature {creature_id} not registered.")
            model = self.models[creature_id]
        x = torch.tensor(sensor_input, dtype=torch.float32)
        with torch.no_grad():
            output = model(x)
        return output.tolist()  # Convert tensor to list for JSON serialization

# Global instance of NeuralNetworkManager.
nn_manager = NeuralNetworkManager()

# =============================
# Networking Code (Socket Server)
# =============================

def handle_client(conn, addr):
    print(f"Connected by {addr}")
    buffer = ""
    try:
        while True:
            data = conn.recv(4096)
            if not data:
                break  # Connection closed by client
            buffer += data.decode('utf-8')
            while "\n" in buffer:
                line, buffer = buffer.split("\n", 1)
                if not line.strip():
                    continue  # Skip empty lines
                try:
                    message = json.loads(line)
                    msg_type = message.get("type")
                    print(f"Received message type: {msg_type}")
                    
                    if msg_type == "init":
                        # Expect a list of brains under the key "brains"
                        brains = message.get("brains", [])
                        results = {}
                        for brain in brains:
                            creature_id = brain.get("id")
                            genome_weights = brain.get("BrainWeights")
                            if genome_weights is None or len(genome_weights) != 330:
                                results[creature_id] = False
                            else:
                                success = nn_manager.register_model(creature_id, genome_weights)
                                results[creature_id] = success
                        response = {"status": "initialized", "results": results}
                        response_str = json.dumps(response) + "\n"
                        conn.sendall(response_str.encode('utf-8'))
                    
                    elif msg_type == "evaluate":
                        # Expect a list of sensor objects under the key "sensors"
                        sensors_list = message.get("sensors", [])
                        results = {}
                        for sensor in sensors_list:
                            creature_id = sensor.get("id")
                            # Build the sensor input vector in the expected order:
                            sensor_input = [
                                sensor.get("PlantNormalizedDistance", 1.0),
                                sensor.get("PlantAngleSin", 0.0),
                                sensor.get("PlantAngleCos", 0.0),
                                sensor.get("CreatureNormalizedDistance", 1.0),
                                sensor.get("CreatureAngleSin", 0.0),
                                sensor.get("CreatureAngleCos", 0.0),
                                sensor.get("Hunger", 0.0)
                            ]
                            try:
                                output = nn_manager.evaluate(creature_id, sensor_input)
                                jet_forces = {
                                    "Front": output[0],
                                    "Back": output[1],
                                    "TopRight": output[2],
                                    "TopLeft": output[3],
                                    "BottomRight": output[4],
                                    "BottomLeft": output[5]
                                }
                                results[creature_id] = jet_forces
                            except Exception as e:
                                print(f"Error evaluating creature {creature_id}: {e}")
                                results[creature_id] = {
                                    "Front": 0,
                                    "Back": 0,
                                    "TopRight": 0,
                                    "TopLeft": 0,
                                    "BottomRight": 0,
                                    "BottomLeft": 0
                                }

                        response = {"status": "evaluated", "results": results}
                        response_str = json.dumps(response) + "\n"
                        conn.sendall(response_str.encode('utf-8'))
                    
                    else:
                        response = {"error": "Unknown message type."}
                        conn.sendall((json.dumps(response) + "\n").encode('utf-8'))
                
                except Exception as e:
                    print(f"Error processing message from {addr}: {e}")
                    error_response = {"error": str(e)}
                    conn.sendall((json.dumps(error_response) + "\n").encode('utf-8'))
    except Exception as e:
        print(f"Connection error with {addr}: {e}")
    finally:
        print(f"Connection closed: {addr}")
        conn.close()

def main():
    host = "0.0.0.0"
    port = 5000
    server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    server_socket.bind((host, port))
    server_socket.listen(5)
    print(f"Neural network server listening on {host}:{port}")

    try:
        while True:
            conn, addr = server_socket.accept()
            client_thread = threading.Thread(target=handle_client, args=(conn, addr))
            client_thread.daemon = True
            client_thread.start()
    except KeyboardInterrupt:
        print("Server shutting down.")
    finally:
        server_socket.close()

if __name__ == '__main__':
    main()