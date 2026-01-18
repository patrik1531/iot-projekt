#!/usr/bin/env python3
"""
MQTT Monitor pre IoT senzor
===========================
Pripojí sa na lokálny MQTT broker a zobrazuje správy od zariadenia.

Použitie:
    python mqtt_monitor.py
"""

import json
from datetime import datetime
import paho.mqtt.client as mqtt

# === TVOJA KONFIGURÁCIA ===
MQTT_SERVER = "192.168.0.143"
MQTT_PORT = 1884
MQTT_USER = None
MQTT_PASSWORD = None

# MQTT téma
MQTT_DEPARTMENT = "test"
MQTT_ROOM = "ps418ph"
DEVICE_ID = "ps418ph"

# Posledné hodnoty senzorov
sensor_data = {}
message_count = 0


class Colors:
    HEADER = '\033[95m'
    BLUE = '\033[94m'
    CYAN = '\033[96m'
    GREEN = '\033[92m'
    YELLOW = '\033[93m'
    RED = '\033[91m'
    ENDC = '\033[0m'
    BOLD = '\033[1m'


def print_header(text):
    print(f"\n{Colors.HEADER}{Colors.BOLD}{'=' * 60}{Colors.ENDC}")
    print(f"{Colors.HEADER}{Colors.BOLD}{text:^60}{Colors.ENDC}")
    print(f"{Colors.HEADER}{Colors.BOLD}{'=' * 60}{Colors.ENDC}\n")


def print_sensor_table():
    """Vypíš prehľadnú tabuľku všetkých senzorov"""
    global sensor_data, message_count

    if not sensor_data:
        return

    print(f"\n{Colors.BOLD}{'─' * 60}{Colors.ENDC}")
    print(f"{Colors.BOLD}📊 AKTUÁLNE HODNOTY SENZOROV (správ: {message_count}){Colors.ENDC}")
    print(f"{Colors.BOLD}{'─' * 60}{Colors.ENDC}")

    emoji_map = {
        "temperature": "🌡️ ",
        "humidity": "💧",
        "window": "🪟",
        "air_quality": "🌬️ ",
        "air_quality_percent": "🌬️ ",
        "motion": "🚶",
        "status": "📡",
    }

    for sensor, data in sorted(sensor_data.items()):
        emoji = emoji_map.get(sensor, "📊")
        value = data.get("value", "N/A")
        units = data.get("units", "")
        dt = data.get("dt", "")

        # Formátuj hodnotu
        if isinstance(value, bool):
            value_str = f"{Colors.GREEN}Áno{Colors.ENDC}" if value else f"{Colors.RED}Nie{Colors.ENDC}"
        elif isinstance(value, float):
            value_str = f"{value:.1f}"
        elif value == "open":
            value_str = f"{Colors.YELLOW}Otvorené{Colors.ENDC}"
        elif value == "closed":
            value_str = f"{Colors.GREEN}Zatvorené{Colors.ENDC}"
        elif value == "online":
            value_str = f"{Colors.GREEN}Online{Colors.ENDC}"
        elif value == "offline":
            value_str = f"{Colors.RED}Offline{Colors.ENDC}"
        else:
            value_str = str(value)

        # Skráť názov senzora
        sensor_name = sensor.replace("air_quality_percent", "vzduch %").replace("air_quality", "vzduch raw").replace(
            "temperature", "teplota").replace("humidity", "vlhkosť").replace("window", "okno").replace("motion",
                                                                                                       "pohyb")

        print(f"  {emoji} {sensor_name:15} │ {value_str:20} {units}")

    print(f"{Colors.BOLD}{'─' * 60}{Colors.ENDC}")
    now = datetime.now().strftime("%H:%M:%S")
    print(f"{Colors.BLUE}Posledná aktualizácia: {now}{Colors.ENDC}\n")


def on_connect(client, userdata, flags, rc, properties=None):
    if rc == 0:
        print_header("MQTT Monitor - Pripojený")
        print(f"{Colors.GREEN}✅ Pripojené k {MQTT_SERVER}:{MQTT_PORT}{Colors.ENDC}")

        # Subscribe na každý senzor
        sensors = ["temperature", "humidity", "window", "air_quality", "air_quality_percent", "motion", "status"]
        for sensor in sensors:
            topic = f"{DEVICE_ID}-{sensor}"
            client.subscribe(topic)
            print(f"{Colors.CYAN}📡 Počúvam na: {topic}{Colors.ENDC}")

        print(f"\n{Colors.YELLOW}Čakám na správy... (CTRL+C pre ukončenie){Colors.ENDC}\n")
    else:
        # ... error handling
        error_messages = {
            1: "Nesprávna verzia protokolu",
            2: "Neplatný client ID",
            3: "Server nedostupný",
            4: "Zlé meno/heslo",
            5: "Nie si autorizovaný",
        }
        msg = error_messages.get(rc, f"Neznáma chyba ({rc})")
        print(f"{Colors.RED}❌ Chyba pripojenia: {msg}{Colors.ENDC}")


def on_message(client, userdata, msg):
    global sensor_data, message_count

    message_count += 1
    now = datetime.now().strftime("%H:%M:%S")

    try:
        payload_str = msg.payload.decode('utf-8')
        data = json.loads(payload_str)

        # Extrahuj názov senzora z topicu (ps418ph-temperature -> temperature)
        topic = msg.topic
        if topic.startswith(f"{DEVICE_ID}-"):
            sensor_name = topic.replace(f"{DEVICE_ID}-", "")
        else:
            sensor_name = topic.split("/")[-1]

        # Ulož dáta
        sensor_data[sensor_name] = data

        # Jednoriadkový výpis
        value = data.get("value", "N/A")
        units = data.get("units", "")

        if isinstance(value, bool):
            value_str = "Áno" if value else "Nie"
        elif isinstance(value, float):
            value_str = f"{value:.1f}"
        else:
            value_str = str(value)

        print(f"{Colors.GREEN}[{now}]{Colors.ENDC} {Colors.CYAN}{sensor_name:20}{Colors.ENDC} = {value_str} {units}")

        # Každých 6 správ (1 cyklus) vypíš tabuľku
        if message_count % 6 == 0:
            print_sensor_table()

    except json.JSONDecodeError as e:
        print(f"{Colors.RED}[{now}] JSON chyba: {e}{Colors.ENDC}")
    except UnicodeDecodeError:
        print(f"{Colors.RED}[{now}] Chyba dekódovania{Colors.ENDC}")


def on_disconnect(client, userdata, rc, properties=None):
    if rc != 0:
        print(f"\n{Colors.RED}⚠️ Odpojené, kód: {rc}{Colors.ENDC}")


def main():
    print_header("MQTT Monitor - Štartujem")
    print(f"📡 Server: {MQTT_SERVER}:{MQTT_PORT}")
    print(f"📟 Device ID: {DEVICE_ID}")
    print(f"\n{Colors.YELLOW}Pripájam sa...{Colors.ENDC}")

    client = mqtt.Client(
        client_id=f"monitor-{DEVICE_ID}-{datetime.now().timestamp():.0f}",
        callback_api_version=mqtt.CallbackAPIVersion.VERSION2
    )

    client.on_connect = on_connect
    client.on_message = on_message
    client.on_disconnect = on_disconnect

    if MQTT_USER and MQTT_PASSWORD:
        client.username_pw_set(MQTT_USER, MQTT_PASSWORD)

    try:
        client.connect(MQTT_SERVER, MQTT_PORT, 60)
        client.loop_forever()

    except KeyboardInterrupt:
        print(f"\n\n{Colors.YELLOW}👋 Ukončujem...{Colors.ENDC}")
        print_sensor_table()
        client.disconnect()
    except Exception as e:
        print(f"\n{Colors.RED}❌ Chyba: {e}{Colors.ENDC}")


if __name__ == "__main__":
    main()