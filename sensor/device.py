# device.py

from machine import Pin
from neopixel import NeoPixel
from constants import NP_PIN


class Device:
    def __init__(self):
        self.state = None
        self.settings = None
        self.sensor = None
        self.rtc = None
        self.led = NeoPixel(Pin(NP_PIN, Pin.OUT), 1)
        self.error_code = None
        self.config_message = None
        self.mqtt_client = None  # MQTT klient
        self.measurements = []  # Zoznam meraní na odoslanie

        from states.init import Init
        self.change_state(Init)

    def change_state(self, state_class):
        if self.state:
            self.state.exit()
        self.state = state_class(self)
        self.state.enter()

    def run(self):
        while True:
            try:
                self.state.exec()
            except SystemExit:
                break