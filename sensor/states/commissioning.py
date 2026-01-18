from .state import AbstractState


class Commissioning(AbstractState):
    def enter(self):
        print(">> Commissioning")
        from constants import Color

        self.device.led[0] = Color.YELLOW
        self.device.led.write()

    def exec(self):
        from states.configuration import Configuration
        from helpers import do_connect, sync_ntp, set_external_rtc
        from helpers import install_mqtt_package, connect_mqtt, disconnect_mqtt
        from env import WIFI_SSID, WIFI_PASSWORD, NTP_HOST
        import machine

        print("Overujem WiFi nastavenia...")

        wlan = do_connect(WIFI_SSID, WIFI_PASSWORD)

        if wlan is None:
            print("WiFi nefunguje - vraciam sa do Configuration")
            self.device.config_message = "WiFi pripojenie zlyhalo!"
            self.device.change_state(Configuration)
            return

        # Synchronizuj čas
        if sync_ntp(NTP_HOST):
            if self.device.rtc:
                set_external_rtc(self.device.rtc)

        # Nainštaluj MQTT balík
        install_mqtt_package()

        # Over MQTT pripojenie
        print("Overujem MQTT pripojenie...")
        try:
            from helpers import get_settings
            settings = get_settings()
            if settings.mqtt and settings.mqtt.server:
                client = connect_mqtt(settings.mqtt)
                disconnect_mqtt(client)
                print("MQTT OK")
        except Exception as e:
            print(f"MQTT test zlyhal: {e}")
            # Neukončuj - MQTT nie je kritické

        print("Commissioning OK - reštartujem")

        from helpers import disconnect_wifi
        disconnect_wifi()

        machine.reset()

    def exit(self):
        pass