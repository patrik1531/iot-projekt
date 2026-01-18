# constants.py

DHT_PIN = 2
PIR_PIN = 8
MQ135_PIN = 26
REED_PIN = 0

NP_PIN = 28      # NeoPixel na Cytron doske
BTN_PIN = 20     # Tlačidlo na GPIO 20

I2C_SDA = 4
I2C_SCL = 5

APP_VERSION = "1.0.0"

SHORT_PRESS_DURATION = 3  # sekundy
LONG_PRESS_DURATION = 6   # sekundy

SETTINGS_FILE = "/settings.json"
MEASUREMENTS_FILE = "/measurements.json"

class TempUnit:
    IMPERIAL = 'imperial'
    STANDARD = 'standard'
    METRIC = 'metric'

class Color:
    RED = (255, 0, 0)
    GREEN = (0, 255, 0)
    BLUE = (0, 0, 255)
    CYAN = (0, 255, 255)
    ORANGE = (255, 165, 0)
    MAGENTA = (255, 0, 255)
    OFF = (0, 0, 0)