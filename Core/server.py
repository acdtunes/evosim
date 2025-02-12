import socket
import json
import threading
import random

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
                    continue  # Skip empty lines
                try:
                    request = json.loads(line)
                    sensors = request.get("sensors", [])
                    jetForces = {}
                    for sensor in sensors:
                        sensor_id = sensor["id"]
                        jetForces[sensor_id] = {
                            "Front": random.random(),
                            "Back": random.random(),
                            "TopRight": random.random(),
                            "TopLeft": random.random(),
                            "BottomRight": random.random(),
                            "BottomLeft": random.random()
                        }
                    
                    response = jetForces
                    
                    response_str = json.dumps(response) + "\n"
                    conn.sendall(response_str.encode('utf-8'))
                except Exception as e:
                    print(f"Error processing request from {addr}: {e}")
    except Exception as e:
        print(f"Error with connection {addr}: {e}")
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