# models/payload.py

import json


class Metric:
    """Jedna metrika/meranie"""

    def __init__(self, name, value, units, dt=None):
        self.name = name
        self.value = value
        self.units = units
        self.dt = dt

    def to_dict(self):
        d = {
            "name": self.name,
            "value": self.value,
            "units": self.units
        }
        if self.dt:
            d["dt"] = self.dt
        return d


class Payload:
    """MQTT payload s metriky"""

    def __init__(self, dt=None):
        self.dt = dt
        self.metrics = []

    def add_metric(self, name, value, units, dt=None):
        self.metrics.append(Metric(name, value, units, dt))

    def to_dict(self):
        return {
            "dt": self.dt,
            "metrics": [m.to_dict() for m in self.metrics]
        }

    def to_json(self):
        return json.dumps(self.to_dict())


class StatusPayload:
    """MQTT status payload"""

    def __init__(self, status="online", **kwargs):
        self.status = status
        self.extra = kwargs

    def to_dict(self):
        d = {"status": self.status}
        d.update(self.extra)
        return d

    def to_json(self):
        return json.dumps(self.to_dict())