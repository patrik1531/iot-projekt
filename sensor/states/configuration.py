from .state import AbstractState
import network


class Configuration(AbstractState):
    def enter(self):
        print(">> Configuration")
        from constants import Color, DHT_PIN
        from env import STUDENT_ID, AP_PASSWORD
        from helpers import create_default_settings
        from machine import Pin
        from dht import DHT22

        self.device.led[0] = Color.CYAN
        self.device.led.write()

        # Inicializuj senzor ak ešte nie je
        if not self.device.sensor:
            try:
                self.device.sensor = DHT22(Pin(DHT_PIN, Pin.IN, Pin.PULL_UP))
                print("DHT22 inicializovaný")
            except Exception as e:
                print(f"DHT22 chyba: {e}")

        # Vytvor predvolené nastavenia ak neexistujú
        try:
            with open('/settings.json', 'r') as f:
                pass
        except:
            create_default_settings()

        # Vypni STA
        sta = network.WLAN(network.STA_IF)
        sta.active(False)

        # Zapni AP
        self.ap = network.WLAN(network.AP_IF)
        self.ap.active(True)

        ssid = f"thsensor-{STUDENT_ID}"
        self.ap.config(essid=ssid, password=AP_PASSWORD)

        print(f"AP SSID: {ssid}")
        print(f"AP heslo: {AP_PASSWORD}")
        print(f"IP: {self.ap.ifconfig()[0]}")

    def exec(self):
        from states.commissioning import Commissioning
        from webserver import run_server

        print("Spúšťam web server...")
        print("Pripoj sa k WiFi a otvor http://192.168.4.1")
        print("Pre ukončenie stlač CTRL+C alebo reštartuj cez web")

        try:
            run_server(self.device, port=80)
        except KeyboardInterrupt:
            print("Web server zastavený")

        self.device.change_state(Commissioning)

    def exit(self):
        self.ap.active(False)
        print("AP vypnutý")