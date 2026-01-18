from .state import AbstractState
import time


class Operation(AbstractState):
    def enter(self):
        print(">> Operation")

    def exec(self):
        from states.sleep import Sleep
        from constants import MEASUREMENTS_FILE
        import json

        time.sleep(2)  # Pauza medzi meraniami

        try:
            self.device.sensor.measure()
            temp = self.device.sensor.temperature()
            hum = self.device.sensor.humidity()
        except Exception as e:
            print(f"Chyba merania: {e}")
            self.device.change_state(Sleep)
            return

        # Ulož meranie
        measurement = {"temp": temp, "hum": hum}

        try:
            with open(MEASUREMENTS_FILE, 'r') as f:
                data = json.load(f)
        except:
            data = []

        data.append(measurement)

        with open(MEASUREMENTS_FILE, 'w') as f:
            json.dump(data, f)

        print(f"Meranie: {temp}°C, {hum}%")

        self.device.change_state(Sleep)

    def exit(self):
        pass