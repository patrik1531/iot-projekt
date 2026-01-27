import { useState } from "react";
import { Card, CardContent, CardHeader, CardTitle } from "./ui/card";
import { Button } from "./ui/button";
import { Badge } from "./ui/badge";
import { Switch } from "./ui/switch";
import { Slider } from "./ui/slider";
import { Progress } from "./ui/progress";
import {
  Lightbulb,
  Fan,
  Thermometer,
  DoorOpen,
  Camera,
  Speaker,
  Wifi,
  Battery,
} from "lucide-react";
import { motion } from "motion/react";

interface Device {
  id: string;
  name: string;
  type: string;
  status: "online" | "offline" | "warning";
  enabled: boolean;
  value?: number;
  battery?: number;
  icon: any;
  lastUpdate: Date;
}

export function Devices() {
  const [devices, setDevices] = useState<Device[]>([
    {
      id: "1",
      name: "LED Strip",
      type: "Light",
      status: "online",
      enabled: true,
      value: 75,
      battery: 100,
      icon: Lightbulb,
      lastUpdate: new Date(),
    },
    {
      id: "2",
      name: "Ceiling Fan",
      type: "Fan",
      status: "online",
      enabled: false,
      value: 0,
      icon: Fan,
      lastUpdate: new Date(),
    },
    {
      id: "3",
      name: "Smart Thermostat",
      type: "Climate",
      status: "online",
      enabled: true,
      value: 22,
      icon: Thermometer,
      lastUpdate: new Date(),
    },
    {
      id: "4",
      name: "Window Actuator",
      type: "Window",
      status: "online",
      enabled: false,
      battery: 85,
      icon: DoorOpen,
      lastUpdate: new Date(),
    },
    {
      id: "5",
      name: "Security Camera",
      type: "Camera",
      status: "warning",
      enabled: true,
      battery: 45,
      icon: Camera,
      lastUpdate: new Date(),
    },
    {
      id: "6",
      name: "Smart Speaker",
      type: "Audio",
      status: "online",
      enabled: true,
      value: 60,
      icon: Speaker,
      lastUpdate: new Date(),
    },
  ]);

  const toggleDevice = (id: string) => {
    setDevices((prev) =>
      prev.map((device) =>
        device.id === id ? { ...device, enabled: !device.enabled } : device
      )
    );
  };

  const updateDeviceValue = (id: string, value: number) => {
    setDevices((prev) =>
      prev.map((device) => (device.id === id ? { ...device, value } : device))
    );
  };

  const statusColors = {
    online: "bg-green-500",
    offline: "bg-gray-400",
    warning: "bg-yellow-500",
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold">Devices</h1>
          <p className="text-muted-foreground">Manage your IoT devices</p>
        </div>
        <Button>
          <Wifi className="h-4 w-4 mr-2" />
          Add Device
        </Button>
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        {devices.map((device, index) => {
          const Icon = device.icon;
          return (
            <motion.div
              key={device.id}
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ delay: index * 0.05 }}
            >
              <Card>
                <CardHeader>
                  <div className="flex items-start justify-between">
                    <div className="flex items-center gap-3">
                      <div
                        className={`p-2 rounded-lg ${
                          device.enabled ? "bg-primary/10" : "bg-muted"
                        }`}
                      >
                        <Icon
                          className={`h-5 w-5 ${
                            device.enabled ? "text-primary" : "text-muted-foreground"
                          }`}
                        />
                      </div>
                      <div>
                        <CardTitle className="text-base">{device.name}</CardTitle>
                        <p className="text-sm text-muted-foreground">{device.type}</p>
                      </div>
                    </div>
                    <div className="flex items-center gap-2">
                      <div
                        className={`w-2 h-2 rounded-full ${
                          statusColors[device.status]
                        }`}
                      />
                      <Switch
                        checked={device.enabled}
                        onCheckedChange={() => toggleDevice(device.id)}
                      />
                    </div>
                  </div>
                </CardHeader>
                <CardContent className="space-y-4">
                  {device.value !== undefined && (
                    <div className="space-y-2">
                      <div className="flex justify-between text-sm">
                        <span className="text-muted-foreground">
                          {device.type === "Light"
                            ? "Brightness"
                            : device.type === "Climate"
                            ? "Temperature"
                            : device.type === "Audio"
                            ? "Volume"
                            : "Level"}
                        </span>
                        <span className="font-medium">
                          {device.value}
                          {device.type === "Climate" ? "°C" : "%"}
                        </span>
                      </div>
                      <Slider
                        value={[device.value]}
                        onValueChange={(values) =>
                          updateDeviceValue(device.id, values[0])
                        }
                        max={device.type === "Climate" ? 30 : 100}
                        min={device.type === "Climate" ? 15 : 0}
                        step={1}
                        disabled={!device.enabled}
                      />
                    </div>
                  )}

                  <div className="flex items-center justify-between text-sm">
                    <span className="text-muted-foreground">Status</span>
                    <Badge
                      variant={
                        device.status === "online"
                          ? "default"
                          : device.status === "warning"
                          ? "secondary"
                          : "outline"
                      }
                    >
                      {device.status}
                    </Badge>
                  </div>

                  {device.battery !== undefined && (
                    <div className="space-y-2">
                      <div className="flex items-center justify-between text-sm">
                        <span className="text-muted-foreground flex items-center gap-1">
                          <Battery className="h-3 w-3" />
                          Battery
                        </span>
                        <span className="font-medium">{device.battery}%</span>
                      </div>
                      <Progress value={device.battery} />
                    </div>
                  )}

                  <div className="flex justify-between text-xs text-muted-foreground">
                    <span>Last Update</span>
                    <span>{device.lastUpdate.toLocaleTimeString()}</span>
                  </div>
                </CardContent>
              </Card>
            </motion.div>
          );
        })}
      </div>
    </div>
  );
}
