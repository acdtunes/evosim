import socket
import json
import torch
import torch.nn as nn
import torch.nn.functional as F

# -------------------------------
# Define the neural network model.
# -------------------------------
class CreatureNet(nn.Module):
    def __init__(self):
        super(CreatureNet, self).__init__()
        self.fc1 = nn.Linear(25, 128)
        self.fc2 = nn.Linear(128, 128)
        self.out = nn.Linear(128, 3)

    def forward(self, x):
        x = F.relu(self.fc1(x))
        x = F.relu(self.fc2(x))
        x = self.out(x)
        x = torch.sigmoid(x)  # Normalize outputs to be between 0 and 1.
        return x

# -------------------------------
# Helper: Load weights into the network.
# -------------------------------
def create_network_from_weights(weight_list):
    weight_tensor = torch.tensor(weight_list, dtype=torch.float32)
    model = CreatureNet()

    # Update fc1 parameters: new input dimension is 25 instead of 17.
    fc1_w_num = 128 * 25  # Was: 128 * 17
    fc1_b_num = 128
    fc1_weight = weight_tensor[0:fc1_w_num].view(128, 25)
    fc1_bias = weight_tensor[fc1_w_num:fc1_w_num + fc1_b_num]

    # fc2 parameters
    offset = fc1_w_num + fc1_b_num
    fc2_w_num = 128 * 128
    fc2_b_num = 128
    fc2_weight = weight_tensor[offset:offset + fc2_w_num].view(128, 128)
    fc2_bias = weight_tensor[offset + fc2_w_num:offset + fc2_w_num + fc2_b_num]

    # out parameters
    offset += fc2_w_num + fc2_b_num
    fc3_w_num = 3 * 128
    fc3_b_num = 3
    fc3_weight = weight_tensor[offset:offset + fc3_w_num].view(3, 128)
    fc3_bias = weight_tensor[offset + fc3_w_num:offset + fc3_w_num + fc3_b_num]

    with torch.no_grad():
        model.fc1.weight.copy_(fc1_weight)
        model.fc1.bias.copy_(fc1_bias)
        model.fc2.weight.copy_(fc2_weight)
        model.fc2.bias.copy_(fc2_bias)
        model.out.weight.copy_(fc3_weight)
        model.out.bias.copy_(fc3_bias)

    return model

# -------------------------------
# Global storage for networks by creature ID.
# -------------------------------
networks = {}

# -------------------------------
# Process a received JSON command.
# -------------------------------
def process_command(request):
    command_type = request.get("type")
    if command_type == "init":
        creature_id = request.get("id")
        brain_weights = request.get("weights")
        if creature_id is None or brain_weights is None:
            return {"Error": "Missing 'id' or 'brain_weights'"}
        try:
            model = create_network_from_weights(brain_weights)
            networks[int(creature_id)] = model
            return {"Status": "ok", "Message": f"Initialized brain for creature {creature_id}"}
        except Exception as e:
            return {"Error": f"Failed to initialize brain: {str(e)}"}
    elif command_type == "evaluate":
        sensors_batch = request.get("sensors")
        if sensors_batch is None:
            return {"Error": "Missing 'sensors' field in evaluate command."}
        results = {}
        for sensor in sensors_batch:
            creature_id = sensor.get("id")
            if creature_id is None:
                continue
            try:
                plant_retina = sensor["PlantRetina"]
                non_parasite_retina = sensor["NonParasiteCreatureRetina"]
                parasite_retina = sensor["ParasiteCreatureRetina"]
                energy = sensor["Energy"]
                combined_creature_retina = non_parasite_retina + parasite_retina
                sensor_values = plant_retina + combined_creature_retina + [energy]
            except KeyError as e:
                print(f"Missing sensor field: {e}")
                continue
            input_tensor = torch.tensor(sensor_values, dtype=torch.float32).unsqueeze(0)
            model = networks.get(int(creature_id))
            if model is None:
                output = torch.zeros(1, 3)
            else:
                model.eval()
                with torch.no_grad():
                    output = model(input_tensor)
            output_values = output.squeeze(0).tolist()
            results[int(creature_id)] = {
                "Back": output_values[0],
                "FrontRight": output_values[1],
                "FrontLeft": output_values[2]
            }
        return {"Status": "ok", "Results": results}
    else:
        return {"Error": f"Unknown command type: {command_type}"}

# -------------------------------
# The main blocking server.
# -------------------------------
def run_server(host="localhost", port=5000):
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        s.bind((host, port))
        s.listen(1)  # Listen for a single connection.
        print(f"Server listening on {host}:{port}")
        while True:
            conn, addr = s.accept()
            print(f"Connected by {addr}")
            with conn:
                buffer = ""
                while True:
                    data = conn.recv(4096)
                    if not data:
                        break  # Client disconnected.
                    buffer += data.decode("utf-8")
                    # Process messages terminated by newline.
                    while "\n" in buffer:
                        line, buffer = buffer.split("\n", 1)
                        if not line.strip():
                            continue
                        try:
                            request = json.loads(line)
                            response = process_command(request)
                        except Exception as e:
                            response = {"Error": str(e)}
                        response_line = json.dumps(response) + "\n"
                        conn.sendall(response_line.encode("utf-8"))
            print("Client disconnected.")

if __name__ == "__main__":
    run_server()