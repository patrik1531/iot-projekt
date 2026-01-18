from .state import AbstractState


class Sleep(AbstractState):
    def enter(self):
        print(">> Sleep")
        from constants import Color
        from helpers import disconnect_mqtt, disconnect_wifi, set_low_freq

        set_low_freq()

        # Odpoj MQTT
        if self.device.mqtt_client:
            disconnect_mqtt(self.device.mqtt_client)
            self.device.mqtt_client = None

        # Odpoj WiFi
        disconnect_wifi()

        # Zhasni LED
        self.device.led[0] = Color.OFF
        self.device.led.write()

    def exec(self):
        from states.measurement import Measurement
        from helpers import setup_wake_pin, feed_watchdog
        from constants import BTN_PIN
        import machine
        import time

        # Získaj interval z nastavení
        interval_ms = self.device.settings.measurement_interval
        interval_s = interval_ms // 1000

        print(f"Spánok na {interval_s} sekúnd...")
        print(f"(Stlač tlačidlo pre okamžité prebudenie)")

        # Nastav wake pin pre tlačidlo
        setup_wake_pin(BTN_PIN)

        # Čakaj (lightsleep alebo jednoduché čakanie)
        # Pre testovanie použijeme time.sleep namiesto deepsleep
        # aby sme nemuseli reštartovať

        for i in range(interval_s):
            time.sleep(1)
            feed_watchdog()

            # Skontroluj tlačidlo
            btn = machine.Pin(BTN_PIN, machine.Pin.IN, machine.Pin.PULL_UP)
            if btn.value() == 0:
                print("Tlačidlo stlačené - prebúdzam")
                break

        print("Prebúdzam sa...")

        # Späť na meranie
        self.device.change_state(Measurement)

    def exit(self):
        pass