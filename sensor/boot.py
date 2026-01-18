# boot.py - spustí sa ako prvý pri štarte

import network
from env import STUDENT_ID

# Nastav hostname
hostname = f"thsensor-{STUDENT_ID}"
network.hostname(hostname)
print(f"Hostname: {hostname}")