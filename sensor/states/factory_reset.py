from .state import AbstractState
import os


class FactoryReset(AbstractState):
    def enter(self):
        print(">> Factory Reset")
        from constants import Color

        self.device.led[0] = Color.ORANGE
        self.device.led.write()

    def exec(self):
        from constants import SETTINGS_FILE, MEASUREMENTS_FILE
        from states.sleep import Sleep

        try:
            os.remove(SETTINGS_FILE)
            print("settings.json zmazaný")
        except:
            pass

        try:
            os.remove(MEASUREMENTS_FILE)
            print("measurements.json zmazaný")
        except:
            pass

        print("Factory reset hotový")
        self.device.change_state(Sleep)

    def exit(self):
        pass