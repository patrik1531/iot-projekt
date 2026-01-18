from .state import AbstractState
import time


class Error(AbstractState):
    def enter(self):
        print(f">> Error (kód: {self.device.error_code})")
        from constants import Color

        self.device.led[0] = Color.RED
        self.device.led.write()

    def exec(self):
        from states.sleep import Sleep
        from constants import Color

        for _ in range(self.device.error_code or 1):
            self.device.led[0] = Color.RED
            self.device.led.write()
            time.sleep(0.3)
            self.device.led[0] = Color.OFF
            self.device.led.write()
            time.sleep(0.3)

        time.sleep(1)
        self.device.change_state(Sleep)

    def exit(self):
        pass