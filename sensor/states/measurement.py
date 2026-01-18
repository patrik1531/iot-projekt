from .state import AbstractState
import time


class Measurement(AbstractState):
    def enter(self):
        print(">> Measurement")
        from helpers import feed_watchdog, set_low_freq

        set_low_freq()
        feed_watchdog()

    def exec(self):
        from states.connecting_wifi import ConnectingWifi
        from states.error import Error
        from helpers import to_iso8601, feed_watchdog
        from constants import PIR_PIN, MQ135_PIN, REED_PIN, DHT_PIN
        from machine import Pin, ADC
        from dht import DHT22
        import machine

        time.sleep(2)
        feed_watchdog()

        rtc = machine.RTC()
        timestamp = to_iso8601(rtc.datetime())

        measurements = []

        # Inicializuj senzor ak treba
        if not self.device.sensor:
            try:
                self.device.sensor = DHT22(Pin(DHT_PIN, Pin.IN, Pin.PULL_UP))
            except Exception as e:
                print(f"DHT22 init chyba: {e}")

        # 1. DHT22
        try:
            self.device.sensor.measure()
            temp = self.device.sensor.temperature()
            hum = self.device.sensor.humidity()

            measurements.append({
                "name": "temperature",
                "value": temp,
                "units": self.device.settings.units,
                "dt": timestamp
            })
            measurements.append({
                "name": "humidity",
                "value": hum,
                "units": "%",
                "dt": timestamp
            })
            print(f"DHT22: {temp}°C, {hum}%")
        except Exception as e:
            print(f"DHT22 chyba: {e}")
            self.device.error_code = 5
            self.device.change_state(Error)
            return

        feed_watchdog()

        # 2. Reed switch
        try:
            reed = Pin(REED_PIN, Pin.IN, Pin.PULL_UP)
            window_open = reed.value() == 1

            measurements.append({
                "name": "window",
                "value": "open" if window_open else "closed",
                "units": "state",
                "dt": timestamp
            })
            print(f"Reed: {'otvorené' if window_open else 'zatvorené'}")
        except Exception as e:
            print(f"Reed chyba: {e}")

        # 3. MQ-135
        try:
            mq135 = ADC(Pin(MQ135_PIN))
            air_quality_raw = mq135.read_u16()
            air_quality_percent = round((air_quality_raw / 65535) * 100, 1)

            measurements.append({
                "name": "air_quality",
                "value": air_quality_raw,
                "units": "raw",
                "dt": timestamp
            })
            measurements.append({
                "name": "air_quality_percent",
                "value": air_quality_percent,
                "units": "%",
                "dt": timestamp
            })
            print(f"MQ-135: {air_quality_raw} raw ({air_quality_percent}%)")
        except Exception as e:
            print(f"MQ-135 chyba: {e}")

        # 4. PIR
        try:
            pir = Pin(PIR_PIN, Pin.IN)
            motion_detected = pir.value() == 1

            measurements.append({
                "name": "motion",
                "value": motion_detected,
                "units": "boolean",
                "dt": timestamp
            })
            print(f"PIR: {'pohyb' if motion_detected else 'žiadny pohyb'}")
        except Exception as e:
            print(f"PIR chyba: {e}")

        self.device.measurements = measurements
        print(f"Celkom {len(measurements)} meraní @ {timestamp}")

        feed_watchdog()
        self.device.change_state(ConnectingWifi)

    def exit(self):
        pass