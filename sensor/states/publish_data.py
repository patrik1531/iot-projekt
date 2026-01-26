from .state import AbstractState


class PublishData(AbstractState):
    def enter(self):
        print(">> PublishData")
        from constants import Color

        self.device.led[0] = Color.MAGENTA
        self.device.led.write()

    def exec(self):
        from states.sleep import Sleep
        from states.error import Error
        from helpers import publish_data, feed_watchdog

        try:
            mqtt = self.device.settings.mqtt

            # Publikuj dáta do gw/thsensor/ps418ph/data
            publish_data(
                self.device.mqtt_client,
                mqtt,
                self.device.measurements
            )

            feed_watchdog()
            print("Dáta úspešne odoslané")

        except Exception as e:
            print(f"Chyba publikovania: {e}")
            self.device.error_code = 7
            self.device.change_state(Error)
            return

        self.device.change_state(Sleep)

    def exit(self):
        pass