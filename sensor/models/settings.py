from .udataclasses import Dataclass, validator
from constants import TempUnit


class MqttSettings(Dataclass):
    server: str = ""
    port: int = 1883
    user: str = ""
    password: str = ""
    ssl: bool = False
    device_type: str = "thsensor"
    device_id: str = ""


class Settings(Dataclass):
    units: str = TempUnit.METRIC
    wifi_ssid: str = ""
    wifi_password: str = ""
    ntp_host: str = "pool.ntp.org"
    measurement_interval: int = 30000
    mqtt: MqttSettings = None

    def __init__(self, **kwargs):
        if 'mqtt' in kwargs and isinstance(kwargs['mqtt'], dict):
            kwargs['mqtt'] = MqttSettings(**kwargs['mqtt'])
        super().__init__(**kwargs)

    @validator('units')
    def check_units(self, value):
        if value not in (TempUnit.METRIC, TempUnit.STANDARD, TempUnit.IMPERIAL):
            raise ValueError(f'Unit "{value}" is invalid.')