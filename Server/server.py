# rl_server.py
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

# Hyperparameters
STATE_DIM = 7
ACTION_DIM = 6
BATCH_SIZE = 64       # Per-creature mini-batch size
GAMMA = 0.1
TAU = 0.005           # Soft-update factor
LR_ACTOR = 1e-3
LR_CRITIC = 1e-3
REPLAY_BUFFER_CAPACITY = 1000  # Per–creature replay memory capacity

# Transition tuple for replay buffer
Transition = namedtuple('Transition', ('state', 'action', 'reward', 'next_state', 'done'))

class ReplayBuffer:
    def __init__(self, capacity):
        self.buffer = deque(maxlen=capacity)
    def push(self, state, action, reward, next_state, done):
        self.buffer.append(Transition(state, action, reward, next_state, done))
    def sample(self, batch_size):
        return random.sample(self.buffer, min(batch_size, len(self.buffer)))
    def __len__(self):
        return len(self.buffer)

# Define the actor network (maps sensor state to jet–force actions)
class Actor(nn.Module):
    def __init__(self, input_dim=STATE_DIM, output_dim=ACTION_DIM):
        super(Actor, self).__init__()
        self.fc1 = nn.Linear(input_dim, 128)
        self.fc2 = nn.Linear(128, 128)
        self.fc3 = nn.Linear(128, output_dim)
    def forward(self, x):
        x = F.relu(self.fc1(x))
        x = F.relu(self.fc2(x))
        # Use sigmoid to constrain outputs between 0 and 1.
        return torch.sigmoid(self.fc3(x))

# Define the critic network (estimates Q–value given state and action)
class Critic(nn.Module):
    def __init__(self, state_dim=STATE_DIM, action_dim=ACTION_DIM):
        super(Critic, self).__init__()
        self.fc1 = nn.Linear(state_dim + action_dim, 128)
        self.fc2 = nn.Linear(128, 128)
        self.fc3 = nn.Linear(128, 1)
    def forward(self, state, action):
        x = torch.cat([state, action], dim=-1)
        x = F.relu(self.fc1(x))
        x = F.relu(self.fc2(x))
        return self.fc3(x)

# Each creature's brain contains its own networks, optimizers, and replay buffer.
class Brain:
    def __init__(self):
        self.actor = Actor()
        self.critic = Critic()
        self.actor_target = Actor()
        self.critic_target = Critic()
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
            target_param.data.copy_(target_param.data*(1-TAU) + param.data*TAU)
        for target_param, param in zip(self.critic_target.parameters(), self.critic.parameters()):
            target_param.data.copy_(target_param.data*(1-TAU) + param.data*TAU)
        return {'actor_loss': actor_loss.item(), 'critic_loss': critic_loss.item()}

# Global dictionary mapping creature ID to its own Brain.
brains = {}
brains_lock = threading.Lock()

def get_brain(creature_id):
    with brains_lock:
        if creature_id not in brains:
            brains[creature_id] = Brain()
        return brains[creature_id]

def set_weights_from_flat_list(model, flat_list):
    flat_tensor = torch.tensor(flat_list, dtype=torch.float32)
    pointer = 0
    for param in model.parameters():
        numel = param.numel()
        param.data.copy_(flat_tensor[pointer:pointer+numel].view_as(param))
        pointer += numel

# Helper function to send a response over the connection.
def send_response(conn, response):
    conn.sendall((json.dumps(response) + "\n").encode('utf-8'))

def handle_init(message):
    creature_id = message.get("id")
    brain_weights = message.get("brain_weights")
    if brain_weights is None:
        return {"error": "Missing brain_weights"}
    # Create a new brain and initialize the actor using the provided weights.
    brain = Brain()
    set_weights_from_flat_list(brain.actor, brain_weights)
    brains[creature_id] = brain
    return {"status": "initialized", "id": creature_id}

def handle_evaluate(message):
    sensor_list = message.get("sensors", [])
    results = {}
    for sensor in sensor_list:
        creature_id = sensor.get("id")
        state = [
            sensor.get("PlantNormalizedDistance", 1.0),
            sensor.get("PlantAngleSin", 0.0),
            sensor.get("PlantAngleCos", 0.0),
            sensor.get("CreatureNormalizedDistance", 1.0),
            sensor.get("CreatureAngleSin", 0.0),
            sensor.get("CreatureAngleCos", 0.0),
            sensor.get("Energy", 0.0)
        ]
        brain = get_brain(creature_id)
        state_tensor = torch.tensor(state, dtype=torch.float32).unsqueeze(0)
        with torch.no_grad():
            action_tensor = brain.actor(state_tensor)
        action = action_tensor.squeeze(0).tolist()
        results[creature_id] = {
            "Front": action[0],
            "Back": action[1],
            "TopRight": action[2],
            "TopLeft": action[3],
            "BottomRight": action[4],
            "BottomLeft": action[5]
        }
    return {"status": "evaluated", "results": results}

def handle_train(message):
    training_list = message.get("training", [])
    train_info = {}
    for item in training_list:
        creature_id = item.get("id")
        brain = get_brain(creature_id)
        state = item.get("state")
        action = item.get("action")
        reward = item.get("reward")
        next_state = item.get("next_state")
        done = item.get("done", False)
        if state is not None and action is not None and reward is not None and next_state is not None:
            brain.replay_buffer.push(state, action, reward, next_state, done)
            info = []
            for _ in range(5):
                update_info = brain.train_step()
                if update_info is not None:
                    info.append(update_info)
            train_info[creature_id] = info
    return {"status": "trained", "info": train_info}

# Dispatch function to process any message based on its type.
def process_message(message):
    msg_type = message.get("type")
    if msg_type == "init":
        return handle_init(message)
    elif msg_type == "evaluate":
        return handle_evaluate(message)
    elif msg_type == "train":
        return handle_train(message)
    else:
        return {"error": "Unknown message type"}

def handle_client(conn, addr):
    print(f"Connected by {addr}")
    buffer = ""
    try:
        while True:
            data = conn.recv(4096)
            if not data:
                break
            buffer += data.decode('utf-8')
            while "\n" in buffer:
                line, buffer = buffer.split("\n", 1)
                if not line.strip():
                    continue
                try:
                    message = json.loads(line)
                    response = process_message(message)
                    send_response(conn, response)
                except Exception as e:
                    print(f"Error processing message: {e}")
                    send_response(conn, {"error": str(e)})
    except Exception as e:
        print(f"Connection error: {e}")
    finally:
        print(f"Connection closed: {addr}")
        conn.close()

def main():
    host = "0.0.0.0"
    port = 5000
    server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    server_socket.bind((host, port))
    server_socket.listen(5)
    print(f"RL Server listening on {host}:{port}")
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