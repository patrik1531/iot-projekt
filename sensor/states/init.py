from .state import AbstractState
from machine import Pin, I2C
import time


class Init(AbstractState):
    def enter(self):
        print(">> Init")
        from constants import Color

        self.device.led[0] = Color.GREEN
        self.device.led.write()

    def exec(self):
        from states.diagnostics import Diagnostics
        from states.configuration import Configuration
        from states.factory_reset import FactoryReset
        from helpers import get_settings, get_time_from_external_rtc
        from constants import BTN_PIN, SHORT_PRESS_DURATION, LONG_PRESS_DURATION, Color
        from constants import I2C_SDA, I2C_SCL

        # Inicializuj DS3231
        try:
            from lib.ds3231_gen import DS3231
            i2c = I2C(0, sda=Pin(I2C_SDA), scl=Pin(I2C_SCL))
            self.device.rtc = DS3231(i2c)
            get_time_from_external_rtc(self.device.rtc)
            print("DS3231 OK")
        except Exception as e:
            print(f"DS3231: {e}")
            self.device.rtc = None

        btn = Pin(BTN_PIN, Pin.IN, Pin.PULL_UP)

        print("Stlač tlačidlo pre konfiguráciu (2s)...")
        wait_time = 0
        while wait_time < 2:
            if btn.value() == 0:
                break
            time.sleep(0.1)
            wait_time += 0.1

        press_time = 0
        while btn.value() == 0:
            time.sleep(0.1)
            press_time += 0.1

            if press_time >= LONG_PRESS_DURATION:
                self.device.led[0] = Color.ORANGE
                self.device.led.write()
                print("Factory Reset...")
            elif press_time >= SHORT_PRESS_DURATION:
                self.device.led[0] = Color.CYAN
                self.device.led.write()
                print("Configuration...")

        if press_time >= LONG_PRESS_DURATION:
            self.device.change_state(FactoryReset)
            return

        if press_time >= SHORT_PRESS_DURATION:
            self.device.change_state(Configuration)
            return

        try:
            self.device.settings = get_settings()
            print("Nastavenia načítané")
            self.device.change_state(Diagnostics)
        except Exception as e:
            print(f"Chyba nastavení: {e}")
            self.device.change_state(Configuration)

    def exit(self):
        pass