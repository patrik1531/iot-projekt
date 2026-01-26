# helpers.py

import json
import network
import time

from constants import SETTINGS_FILE, TempUnit

# === FREKVENCIA ===
CPU_FREQ_LOW = 70_000_000  # 70 MHz pre meranie
CPU_FREQ_WIFI = 125_000_000  # 125 MHz pre WiFi


def set_low_freq():
    """Nastav nízku frekvenciu pre meranie"""
    import machine
    machine.freq(CPU_FREQ_LOW)
    print(f"CPU freq: {machine.freq() // 1_000_000} MHz (low)")


def set_wifi_freq():
    """Nastav frekvenciu pre WiFi"""
    import machine
    machine.freq(CPU_FREQ_WIFI)
    print(f"CPU freq: {machine.freq() // 1_000_000} MHz (wifi)")


# === WAKE PIN ===
def setup_wake_pin(pin_num, handler=None):
    """Nastav pin pre prebudenie zo spánku"""
    from machine import Pin

    pin = Pin(pin_num, Pin.IN, Pin.PULL_UP)

    def wake_handler(p):
        if handler:
            handler(p)

    pin.irq(trigger=Pin.IRQ_FALLING, handler=wake_handler)

    return pin


# === SETTINGS ===
def get_settings():
    from models.settings import Settings
    with open(SETTINGS_FILE, 'r') as file:
        settings = json.load(file)
        return Settings(**settings)


def save_settings(settings_dict):
    with open(SETTINGS_FILE, 'w') as file:
        json.dump(settings_dict, file)


def create_default_settings():
    from env import MQTT_SERVER, MQTT_PORT, MQTT_USER, MQTT_PASSWORD, MQTT_SSL
    from env import MQTT_DEPARTMENT, MQTT_ROOM, STUDENT_ID

    default = {
        "units": "metric",
        "wifi_ssid": "",
        "wifi_password": "",
        "ntp_host": "pool.ntp.org",
        "measurement_interval": 30000,
        "mqtt": {
            "server": MQTT_SERVER,
            "port": MQTT_PORT,
            "user": MQTT_USER if MQTT_USER else "",
            "password": MQTT_PASSWORD if MQTT_PASSWORD else "",
            "ssl": MQTT_SSL,
            "department": MQTT_DEPARTMENT,
            "room": MQTT_ROOM,
            "id": STUDENT_ID
        }
    }
    with open(SETTINGS_FILE, 'w') as file:
        json.dump(default, file)
    print(f"Vytvorené nové settings.json")
    return default


def convert_temp(value: float, units: str) -> float:
    if units == TempUnit.METRIC:
        return value
    if units == TempUnit.IMPERIAL:
        return value * 9 / 5 + 32
    if units == TempUnit.STANDARD:
        return value + 273.15
    raise ValueError(f'Unit "{units}" is invalid.')


# === WIFI ===
def do_connect(ssid, password):
    wlan = network.WLAN(network.STA_IF)
    wlan.active(True)

    if wlan.isconnected():
        print(f"Už pripojené: {wlan.ifconfig()[0]}")
        return wlan

    print(f"Pripájam k {ssid}...")
    wlan.connect(ssid, password)

    for attempt in range(3):
        for i in range(10):
            if wlan.isconnected():
                print(f"\nPripojené! IP: {wlan.ifconfig()[0]}")
                return wlan
            print(".", end="")
            feed_watchdog()
            time.sleep(1)
        print(f"\nPokus {attempt + 1}/3 zlyhal")

    wlan.active(False)
    return None


def disconnect_wifi():
    wlan = network.WLAN(network.STA_IF)
    if wlan.isconnected():
        wlan.disconnect()
    wlan.active(False)
    print("WiFi odpojené")


# === NTP & RTC ===
def sync_ntp(host="pool.ntp.org"):
    import ntptime
    try:
        ntptime.host = host
        ntptime.settime()
        print(f"NTP synchronizované z {host}")
        return True
    except Exception as e:
        print(f"NTP chyba: {e}")
        return False


def set_external_rtc(ds3231):
    import machine
    rtc = machine.RTC()
    dt = rtc.datetime()
    ds3231.set_time(dt)
    print("DS3231 nastavený")


def get_time_from_external_rtc(ds3231):
    import machine
    t = ds3231.get_time()
    rtc = machine.RTC()
    rtc.datetime(t)
    print(f"Čas z DS3231: {t}")
    return t


def to_iso8601(dt):
    """Konvertuj datetime tuple na ISO 8601 string (UTC)"""
    return f"{dt[0]:04d}-{dt[1]:02d}-{dt[2]:02d}T{dt[4]:02d}:{dt[5]:02d}:{dt[6]:02d}Z"


# === VALIDÁCIA ===
def validate_host_address(host):
    if not host or len(host) < 3:
        return False
    if " " in host:
        return False
    if "." not in host:
        return False
    parts = host.split(".")
    if len(parts) == 4:
        try:
            for p in parts:
                n = int(p)
                if n < 0 or n > 255:
                    return False
            return True
        except:
            pass
    if len(host) > 3 and "." in host:
        return True
    return False


def get_reset_cause():
    import machine
    causes = {
        machine.PWRON_RESET: "Power on",
        machine.WDT_RESET: "Watchdog",
        machine.SOFT_RESET: "Soft reset"
    }
    return causes.get(machine.reset_cause(), "Unknown")


# === MQTT - SMART DEPARTMENT ŠTANDARD ===
# Formát témy: gw/<device_type>/<device_id>/<action>
# device_type = "thsensor"
# action = data | status | set | cmd

DEVICE_TYPE = "thsensor"


def get_mqtt_topic(mqtt_settings, action):
    """
    Vytvor MQTT tému podľa Smart Department štandardu.

    Formát: gw/<device_type>/<device_id>/<action>

    Príklad: gw/thsensor/ps418ph/data
    """
    return f"gw/{mqtt_settings.device_type}/{mqtt_settings.device_id}/{action}"


def connect_mqtt(mqtt_settings):
    """Pripoj sa k MQTT brokeru s Last Will."""
    from umqtt.simple import MQTTClient

    client_id = f"{mqtt_settings.device_type}-{mqtt_settings.device_id}"

    # Last Will - offline status
    will_topic = get_mqtt_topic(mqtt_settings, "status")
    will_msg = '{"status": "offline"}'

    # SSL ak je potrebné
    ssl_context = None
    if mqtt_settings.ssl:
        import ssl
        ssl_context = ssl.SSLContext(ssl.PROTOCOL_TLS_CLIENT)
        ssl_context.verify_mode = ssl.CERT_NONE

    # User/password len ak sú zadané
    user = mqtt_settings.user if mqtt_settings.user else None
    pwd = mqtt_settings.password if mqtt_settings.password else None

    client = MQTTClient(
        client_id,
        mqtt_settings.server,
        port=mqtt_settings.port,
        user=user,
        password=pwd,
        ssl=ssl_context
    )

    client.set_last_will(will_topic, will_msg, retain=True)

    feed_watchdog()
    client.connect()
    print(f"MQTT pripojené k {mqtt_settings.server}:{mqtt_settings.port}")

    return client


def disconnect_mqtt(client):
    """Odpoj sa od MQTT brokera"""
    if client:
        try:
            client.disconnect()
            print("MQTT odpojené")
        except:
            pass


def publish_status(client, mqtt_settings, online=True):
    """
    Publikuj status zariadenia podľa Smart Department štandardu.

    Topic: gw/thsensor/<id>/status

    Payload online:  {"status": "online"}
    Payload offline: {"status": "offline"}
    """
    topic = get_mqtt_topic(mqtt_settings, "status")

    if online:
        payload = {"status": "online"}
    else:
        payload = {"status": "offline"}

    msg = json.dumps(payload)
    client.publish(topic, msg, retain=True)
    print(f"Status: {topic} → {payload}")


def publish_data(client, mqtt_settings, measurements):
    """
    Publikuj namerané dáta podľa Smart Department štandardu.

    Topic: gw/thsensor/<id>/data

    Payload formát:
    {
        "dt": "2026-01-12T15:30:00Z",
        "metrics": [
            {"dt": "...", "name": "temperature", "value": 23.4, "units": "metric"},
            {"dt": "...", "name": "humidity", "value": 65, "units": "percent"},
            ...
        ]
    }
    """
    import machine

    topic = get_mqtt_topic(mqtt_settings, "data")

    # Aktuálny čas
    rtc = machine.RTC()
    dt = to_iso8601(rtc.datetime())

    # Vytvor payload podľa štandardu
    payload = {
        "dt": dt,
        "metrics": []
    }

    # Pridaj všetky merania
    for m in measurements:
        metric = {
            "dt": m.get("dt", dt),
            "name": m.get("name", "unknown"),
            "value": m.get("value"),
            "units": m.get("units", "")
        }
        payload["metrics"].append(metric)

    msg = json.dumps(payload)
    client.publish(topic, msg)
    print(f"Dáta: {topic}")
    print(f"  {len(payload['metrics'])} metrík odoslaných")


def install_mqtt_package():
    """Nainštaluj umqtt.simple ak neexistuje"""
    try:
        from umqtt.simple import MQTTClient
        print("umqtt.simple už nainštalované")
        return True
    except ImportError:
        print("Inštalujem umqtt.simple...")
        try:
            import mip
            mip.install("umqtt.simple")
            print("umqtt.simple nainštalované!")
            return True
        except Exception as e:
            print(f"Chyba inštalácie: {e}")
            return False


# === WATCHDOG ===
_wdt = None


def start_watchdog(timeout_ms=30000):
    """Spusti watchdog timer"""
    global _wdt
    from machine import WDT
    _wdt = WDT(timeout=timeout_ms)
    print(f"Watchdog spustený ({timeout_ms}ms)")


def feed_watchdog():
    """Nakŕm watchdog (reset timeout)"""
    global _wdt
    if _wdt:
        _wdt.feed()
