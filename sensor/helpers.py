# helpers.py

import json
import network
import time

from constants import SETTINGS_FILE, TempUnit


CPU_FREQ_LOW = 70_000_000      # 70 MHz pre meranie
CPU_FREQ_WIFI = 125_000_000    # 125 MHz pre WiFi

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


def setup_wake_pin(pin_num, handler=None):
    """Nastav pin pre prebudenie zo spánku"""
    from machine import Pin

    pin = Pin(pin_num, Pin.IN, Pin.PULL_UP)

    def wake_handler(p):
        if handler:
            handler(p)

    pin.irq(trigger=Pin.IRQ_FALLING, handler=wake_handler)

    return pin


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
        "mqtt": {
            "server": MQTT_SERVER,
            "port": MQTT_PORT,
            "user": MQTT_USER,
            "password": MQTT_PASSWORD,
            "ssl": MQTT_SSL,
            "department": MQTT_DEPARTMENT,
            "room": MQTT_ROOM,
            "id": STUDENT_ID
        }
    }
    with open(SETTINGS_FILE, 'w') as file:
        json.dump(default, file)
    return default


def convert_temp(value: float, units: str) -> float:
    if units == TempUnit.METRIC:
        return value
    if units == TempUnit.IMPERIAL:
        return value * 9 / 5 + 32
    if units == TempUnit.STANDARD:
        return value + 273.15
    raise ValueError(f'Unit "{units}" is invalid.')


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
    return f"{dt[0]:04d}-{dt[1]:02d}-{dt[2]:02d}T{dt[4]:02d}:{dt[5]:02d}:{dt[6]:02d}Z"


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


# MQTT funkcie
def get_mqtt_topic(mqtt_settings, suffix):
    """Vytvor MQTT tému"""
    return f"{mqtt_settings.department}/{mqtt_settings.room}/{mqtt_settings.id}/{suffix}"


def connect_mqtt(mqtt_settings):
    """Pripoj sa k MQTT brokeru"""
    from umqtt.simple import MQTTClient
    import ssl

    client_id = f"thsensor-{mqtt_settings.id}"

    # Last Will - offline status
    will_topic = get_mqtt_topic(mqtt_settings, "status")
    will_msg = '{"status": "offline"}'

    ssl_context = None
    if mqtt_settings.ssl:
        ssl_context = ssl.SSLContext(ssl.PROTOCOL_TLS_CLIENT)
        ssl_context.verify_mode = ssl.CERT_NONE

    client = MQTTClient(
        client_id,
        mqtt_settings.server,
        port=mqtt_settings.port,
        user=mqtt_settings.user,
        password=mqtt_settings.password,
        ssl=ssl_context
    )

    # Nastav Last Will
    client.set_last_will(will_topic, will_msg, retain=True)

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
    """Publikuj status zariadenia"""
    from models.payload import StatusPayload
    import machine
    from constants import APP_VERSION

    topic = get_mqtt_topic(mqtt_settings, "status")

    if online:
        # Pri online poslať aj ďalšie info
        wlan = network.WLAN(network.STA_IF)
        ip = wlan.ifconfig()[0] if wlan.isconnected() else "N/A"

        payload = StatusPayload(
            status="online",
            ip=ip,
            version=APP_VERSION,
            freq_mhz=machine.freq() // 1000000
        )
    else:
        payload = StatusPayload(status="offline")

    client.publish(topic, payload.to_json(), retain=True)
    print(f"Status publikovaný: {payload.to_dict()}")


def publish_data(client, mqtt_settings, measurements):
    """Publikuj namerané dáta"""
    from models.payload import Payload
    import machine

    topic = get_mqtt_topic(mqtt_settings, "data")

    rtc = machine.RTC()
    dt = to_iso8601(rtc.datetime())

    payload = Payload(dt=dt)

    for m in measurements:
        payload.add_metric(
            name=m.get("name", "unknown"),
            value=m.get("value"),
            units=m.get("units", ""),
            dt=m.get("dt")
        )

    msg = payload.to_json()
    client.publish(topic, msg)
    print(f"Dáta publikované do {topic}")
    print(f"Payload: {msg}")


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


def publish_sensor_data(client, device_id, sensor_name, value, units, timestamp):
    """Publikuj dáta z jedného senzora do samostatného topicu"""
    topic = f"{device_id}-{sensor_name}"

    payload = {
        "value": value,
        "units": units,
        "dt": timestamp
    }

    msg = json.dumps(payload)
    client.publish(topic, msg)
    print(f"  → {topic}: {value} {units}")


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