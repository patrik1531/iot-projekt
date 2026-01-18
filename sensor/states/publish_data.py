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
        from helpers import publish_sensor_data, feed_watchdog
        from env import STUDENT_ID

        try:
            mqtt = self.device.settings.mqtt
            device_id = mqtt.id  # ps418ph

            print(f"Publikujem do samostatných topicov:")

            # Každý senzor do vlastného topicu
            for m in self.device.measurements:
                name = m.get("name", "unknown")
                value = m.get("value")
                units = m.get("units", "")
                dt = m.get("dt", "")

                publish_sensor_data(
                    self.device.mqtt_client,
                    device_id,
                    name,
                    value,
                    units,
                    dt
                )
                feed_watchdog()

            print("Všetky dáta odoslané")

        except Exception as e:
            print(f"Chyba publikovania: {e}")
            self.device.error_code = 7
            self.device.change_state(Error)
            return

        self.device.change_state(Sleep)

    def exit(self):
        pass