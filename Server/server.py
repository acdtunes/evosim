import socket
import json
import threading
import torch
import torch.nn as nn
import torch.optim as optim
import torch.nn.functional as F
import random
import numpy as np
from collections import deque, namedtuple

# -------------------------------
# Hyperparameters
# -------------------------------
STATE_DIM = 25   # Change this to the appropriate sensor vector size
ACTION_DIM = 3   # Number of continuous outputs (for example, jet forces)
BATCH_SIZE = 64
GAMMA = 0.99
TAU = 0.005
LR_ACTOR = 2e-3
LR_CRITIC = 2e-3
REPLAY_BUFFER_CAPACITY = 2000

# Transition tuple for replay buffer
Transition = namedtuple('Transition', ('state', 'action', 'reward', 'next_state', 'done'))

# -------------------------------
# Replay Buffer Class
# -------------------------------
class ReplayBuffer:
    def __init__(self, capacity):
        self.buffer = deque(maxlen=capacity)
    def push(self, state, action, reward, next_state, done):
        self.buffer.append(Transition(state, action, reward, next_state, done))
    def sample(self, batch_size):
        return random.sample(self.buffer, min(batch_size, len(self.buffer)))
    def __len__(self):
        return len(self.buffer)

# -------------------------------
# Actor Network (maps state to continuous actions)
# -------------------------------
class Actor(nn.Module):
    def __init__(self, input_dim=STATE_DIM, output_dim=ACTION_DIM):
        super(Actor, self).__init__()
        self.fc1 = nn.Linear(input_dim, 128)
        self.fc2 = nn.Linear(128, 128)
        self.out = nn.Linear(128, output_dim)
    def forward(self, x):
        x = F.relu(self.fc1(x))
        x = F.relu(self.fc2(x))
        # Use sigmoid to constrain outputs between 0 and 1
        return torch.sigmoid(self.out(x))

# -------------------------------
# Critic Network (estimates Q–value given state and action)
# -------------------------------
class Critic(nn.Module):
    def __init__(self, state_dim=STATE_DIM, action_dim=ACTION_DIM):
        super(Critic, self).__init__()
        self.fc1 = nn.Linear(state_dim + action_dim, 128)
        self.fc2 = nn.Linear(128, 128)
        self.out = nn.Linear(128, 1)
    def forward(self, state, action):
        x = torch.cat([state, action], dim=-1)
        x = F.relu(self.fc1(x))
        x = F.relu(self.fc2(x))
        return self.out(x)

# -------------------------------
# Brain class for each creature
# -------------------------------
class Brain:
    def __init__(self):
        self.actor = Actor()
        self.critic = Critic()
        self.actor_target = Actor()
        self.critic_target = Critic()
        # Initialize target networks with the same weights
        self.actor_target.load_state_dict(self.actor.state_dict())
        self.critic_target.load_state_dict(self.critic.state_dict())
        self.optimizer_actor = optim.Adam(self.actor.parameters(), lr=LR_ACTOR)
        self.optimizer_critic = optim.Adam(self.critic.parameters(), lr=LR_CRITIC)
        self.replay_buffer = ReplayBuffer(REPLAY_BUFFER_CAPACITY)
    def train_step(self):
        if len(self.replay_buffer) < BATCH_SIZE:
            return None
        transitions = self.replay_buffer.sample(BATCH_SIZE)
        batch = Transition(*zip(*transitions))
        state_batch = torch.tensor(np.array(batch.state), dtype=torch.float32)
        action_batch = torch.tensor(np.array(batch.action), dtype=torch.float32)
        reward_batch = torch.tensor(np.array(batch.reward), dtype=torch.float32).unsqueeze(1)
        next_state_batch = torch.tensor(np.array(batch.next_state), dtype=torch.float32)
        done_batch = torch.tensor(np.array(batch.done), dtype=torch.float32).unsqueeze(1)
        # Critic update:
        with torch.no_grad():
            next_action = self.actor_target(next_state_batch)
            target_q = self.critic_target(next_state_batch, next_action)
            y = reward_batch + GAMMA * (1 - done_batch) * target_q
        q_val = self.critic(state_batch, action_batch)
        critic_loss = F.mse_loss(q_val, y)
        self.optimizer_critic.zero_grad()
        critic_loss.backward()
        self.optimizer_critic.step()
        # Actor update:
        actor_loss = -self.critic(state_batch, self.actor(state_batch)).mean()
        self.optimizer_actor.zero_grad()
        actor_loss.backward()
        self.optimizer_actor.step()
        # Soft-update target networks:
        for target_param, param in zip(self.actor_target.parameters(), self.actor.parameters()):
            target_param.data.copy_(target_param.data * (1 - TAU) + param.data * TAU)
        for target_param, param in zip(self.critic_target.parameters(), self.critic.parameters()):
            target_param.data.copy_(target_param.data * (1 - TAU) + param.data * TAU)
        return {'actor_loss': actor_loss.item(), 'critic_loss': critic_loss.item()}

# -------------------------------
# Global dictionary for brains by creature ID and a lock
# -------------------------------
brains = {}
brains_lock = threading.Lock()

def get_brain(creature_id):
    with brains_lock:
        if creature_id not in brains:
            brains[creature_id] = Brain()
        return brains[creature_id]

# -------------------------------
# Helper: Load weights into an actor network from a flat list
# -------------------------------
def set_actor_weights_from_flat_list(actor, flat_list):
    flat_tensor = torch.tensor(flat_list, dtype=torch.float32)
    pointer = 0
    for param in actor.parameters():
        numel = param.numel()
        param.data.copy_(flat_tensor[pointer:pointer+numel].view_as(param))
        pointer += numel

# -------------------------------
# Process a received JSON command.
# -------------------------------
def process_command(request):
    command_type = request.get("type")
    print(f"Request size: {len(json.dumps(request))}")
    if command_type == "init":
        creature_id = request.get("id")
        flat_weights = request.get("weights")
        if creature_id is None or flat_weights is None:
            return {"Error": "Missing 'id' or 'weights' field"}
        try:
            brain = Brain()
            set_actor_weights_from_flat_list(brain.actor, flat_weights)
            brains[int(creature_id)] = brain
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
            # Construct the input state vector.
            try:
                # Example: combine sensor fields into a vector.
                # Adjust the list below to match your actual sensor configuration.
                plant_retina = sensor["PlantRetina"]
                non_parasite_retina = sensor["NonParasiteCreatureRetina"]
                parasite_retina = sensor["ParasiteCreatureRetina"]
                energy = sensor["Energy"]
                combined_creature_retina = non_parasite_retina + parasite_retina
                sensor_values = plant_retina + combined_creature_retina + [energy]
                # Ensure the state vector is of length STATE_DIM.
                if len(sensor_values) != STATE_DIM:
                    print(f"Invalid sensor vector length for creature {creature_id}")
                    continue

            except KeyError as e:
                print(f"Missing sensor field: {e}")
                continue
            state_tensor = torch.tensor(sensor_values, dtype=torch.float32).unsqueeze(0)
            brain = get_brain(creature_id)
            brain.actor.eval()
            with torch.no_grad():
                action_tensor = brain.actor(state_tensor)
            action = action_tensor.squeeze(0).tolist()
            results[int(creature_id)] = {
                "Back": action[0],
                "FrontRight": action[1],
                "FrontLeft": action[2]
            }
        return {"Status": "ok", "Results": results}
    elif command_type == "train":
        # The train command includes a batch of transitions for one or more creatures.
        training_batch = request.get("training", [])
        train_info = {}
        for item in training_batch:
            creature_id = item.get("id")
            brain = get_brain(creature_id)
            state = item.get("state")
            action = item.get("action")
            reward = item.get("reward")
            next_state = item.get("next_state")
            done = item.get("done", False)
            if state is None or action is None or reward is None or next_state is None:
                continue
            brain.replay_buffer.push(state, action, reward, next_state, done)
            info = []
            for _ in range(5):
                update_info = brain.train_step()
                if update_info is not None:
                    info.append(update_info)
            train_info[creature_id] = info
        return {"Status": "ok", "Info": train_info}
    else:
        return {"Error": f"Unknown command type: {command_type}"}

# -------------------------------
# Helper function to send a response over the connection.
# -------------------------------
def send_response(conn, response):
    conn.sendall((json.dumps(response) + "\n").encode('utf-8'))

# -------------------------------
# Main server loop.
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
                    try:
                        data = conn.recv(4096)
                    except ConnectionResetError as cre:
                        print("Connection reset by peer, closing connection.")
                        break  # Gracefully exit the connection loop.

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