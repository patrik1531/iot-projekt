from .state import AbstractState
import time


class Diagnostics(AbstractState):
    def enter(self):
        print(">> Diagnostics")

    def exec(self):
        from states.measurement import Measurement
        from states.error import Error
        from machine import Pin
        from dht import DHT22
        from constants import DHT_PIN

        time.sleep(2)

        try:
            self.device.sensor = DHT22(Pin(DHT_PIN, Pin.IN, Pin.PULL_UP))
            self.device.sensor.measure()
            temp = self.device.sensor.temperature()
            hum = self.device.sensor.humidity()

            print(f"DHT22: {temp}°C, {hum}%")

            if not (0 <= temp <= 50):
                self.device.error_code = 1
                self.device.change_state(Error)
                return

            if not (20 <= hum <= 90):
                self.device.error_code = 2
                self.device.change_state(Error)
                return
        except Exception as e:
            print(f"DHT22 chyba: {e}")
            self.device.error_code = 3
            self.device.change_state(Error)
            return

        if self.device.rtc:
            try:
                t = self.device.rtc.get_time()
                print(f"DS3231: {t[4]:02d}:{t[5]:02d}:{t[6]:02d}")
            except Exception as e:
                print(f"DS3231: {e}")

        print("Diagnostika OK")
        self.device.change_state(Measurement)

    def exit(self):
        pass