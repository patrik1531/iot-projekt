# env.py - VŠETKY PREMENNÉ NA JEDNOM MIESTE

# Tvoj študentský identifikátor
STUDENT_ID = "ps418ph"

# WiFi pripojenie
WIFI_SSID = "Hera koka hasz LSD"
WIFI_PASSWORD = "kiaceed73"

# AP režim heslo
AP_PASSWORD = "thsensor"

# NTP server
NTP_HOST = "pool.ntp.org"

# Web rozhranie
WEB_USERNAME = "admin"
WEB_PASSWORD = "admin"
SECRET_KEY = "tajny-kluc-zmen-ma"

# MQTT nastavenia
##MQTT_SERVER = "147.232.205.176"
##MQTT_PORT = 8883
##MQTT_USER = "student"
##MQTT_PASSWORD = "dgRk9cQ8sU4fCO"
##MQTT_SSL = True

MQTT_SERVER = "192.168.0.143"
MQTT_PORT = 1884
MQTT_USER = None
MQTT_PASSWORD = None
MQTT_SSL = False

# MQTT téma
##MQTT_DEPARTMENT = "kpi"
##MQTT_ROOM = "n9-412"
##MQTT_TYPE = "thsensor"

MQTT_DEPARTMENT = "test"
MQTT_ROOM = "ps418ph"
MQTT_TYPE = "pico"