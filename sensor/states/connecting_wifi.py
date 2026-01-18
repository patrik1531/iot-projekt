from .state import AbstractState


class ConnectingWifi(AbstractState):
    def enter(self):
        print(">> Connecting WiFi")
        from constants import Color

        self.device.led[0] = Color.BLUE
        self.device.led.write()

    def exec(self):
        from states.publish_data import PublishData
        from states.error import Error
        from helpers import do_connect, sync_ntp, set_external_rtc, connect_mqtt, publish_status

        # Pripoj WiFi
        wlan = do_connect(self.device.settings.wifi_ssid, self.device.settings.wifi_password)

        if wlan is None:
            print("WiFi zlyhalo!")
            self.device.error_code = 4
            self.device.change_state(Error)
            return

        # Synchronizuj čas
        if sync_ntp(self.device.settings.ntp_host):
            if self.device.rtc:
                set_external_rtc(self.device.rtc)

        # Pripoj MQTT
        try:
            mqtt = self.device.settings.mqtt
            self.device.mqtt_client = connect_mqtt(mqtt)

            # Publikuj online status
            publish_status(self.device.mqtt_client, mqtt, online=True)

        except Exception as e:
            print(f"MQTT chyba: {e}")
            self.device.error_code = 6
            self.device.change_state(Error)
            return

        self.device.change_state(PublishData)

    def exit(self):
        pass