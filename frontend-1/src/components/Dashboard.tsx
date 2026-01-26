import React, { useEffect, useState, useRef } from "react";
import { SensorCard } from "./SensorCard";
import { LiveIndicator } from "./LiveIndicator";
import { Thermometer, Wind, Users, DoorOpen, Droplets } from "lucide-react";

interface SensorData {
  temperature: number | null;
  humidity: number | null;
  airQuality: number | null;
  airQualityPercent: number | null;
  peoplePresent: boolean | null;
  windowOpen: boolean | null;
  timestamp: Date;
}

const SSE_ENDPOINT = "http://100.99.94.39:5163/api/sensor-events/stream";

export function Dashboard() {
  const [sensorData, setSensorData] = useState<SensorData>({
    temperature: null,
    humidity: null,
    airQuality: null,
    airQualityPercent: null,
    peoplePresent: null,
    windowOpen: null,
    timestamp: new Date(),
  });
  const [lastUpdate, setLastUpdate] = useState<Date | null>(null);
  const [connectionStatus, setConnectionStatus] = useState<"connecting" | "connected" | "error">("connecting");
  const eventSourceRef = useRef<EventSource | null>(null);

  useEffect(() => {
    // Create EventSource connection for SSE
    const eventSource = new EventSource(SSE_ENDPOINT);
    eventSourceRef.current = eventSource;

    eventSource.onopen = () => {
      setConnectionStatus("connected");
    };

    eventSource.onerror = () => {
      setConnectionStatus("error");
    };

    // Handle temperature updates
    eventSource.addEventListener("temperature-update", (event) => {
      const data = JSON.parse(event.data);
      setSensorData((prev) => ({ ...prev, temperature: data.value, timestamp: new Date() }));
      setLastUpdate(new Date());
    });

    // Handle humidity updates
    eventSource.addEventListener("humidity-update", (event) => {
      const data = JSON.parse(event.data);
      setSensorData((prev) => ({ ...prev, humidity: data.value, timestamp: new Date() }));
      setLastUpdate(new Date());
    });

    // Handle window updates
    eventSource.addEventListener("window-update", (event) => {
      const data = JSON.parse(event.data);
      const isOpen = data.value === "open";
      setSensorData((prev) => ({ ...prev, windowOpen: isOpen, timestamp: new Date() }));
      setLastUpdate(new Date());
    });

    // Handle air quality updates (raw value)
    eventSource.addEventListener("air_quality-update", (event) => {
      const data = JSON.parse(event.data);
      setSensorData((prev) => ({ ...prev, airQuality: data.value, timestamp: new Date() }));
      setLastUpdate(new Date());
    });

    // Handle air quality percent updates
    eventSource.addEventListener("air_quality_percent-update", (event) => {
      const data = JSON.parse(event.data);
      setSensorData((prev) => ({ ...prev, airQualityPercent: data.value, timestamp: new Date() }));
      setLastUpdate(new Date());
    });

    // Handle motion/people updates
    eventSource.addEventListener("motion-update", (event) => {
      const data = JSON.parse(event.data);
      setSensorData((prev) => ({ ...prev, peoplePresent: data.value, timestamp: new Date() }));
      setLastUpdate(new Date());
    });

    // Cleanup on unmount
    return () => {
      eventSource.close();
    };
  }, []);

  const hasAnyData = sensorData.temperature !== null || 
    sensorData.humidity !== null || 
    sensorData.airQuality !== null || 
    sensorData.peoplePresent !== null;

  if (!hasAnyData && connectionStatus === "connecting") {
    return (
      <div className="flex items-center justify-center h-screen">
        <div className="text-muted-foreground">Connecting to sensor stream...</div>
      </div>
    );
  }

  if (connectionStatus === "error" && !hasAnyData) {
    return (
      <div className="flex items-center justify-center h-screen">
        <div className="text-red-500">Failed to connect to sensor stream. Please check the connection.</div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold">IoT Dashboard</h1>
          <p className="text-muted-foreground">Real-time sensor monitoring</p>
        </div>
        <div className="flex flex-col items-end gap-1">
          <LiveIndicator />
          {lastUpdate && (
            <span className="text-xs text-muted-foreground">
              Updated: {lastUpdate.toLocaleTimeString()}
            </span>
          )}
        </div>
      </div>

      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6">
        <SensorCard
          title="Temperature"
          value={sensorData.temperature !== null ? sensorData.temperature.toFixed(1) : "--"}
          unit="°C"
          icon={Thermometer}
          status={sensorData.temperature !== null ? "online" : "offline"}
          trend={
            sensorData.temperature !== null
              ? sensorData.temperature > 25
                ? "up"
                : sensorData.temperature < 20
                ? "down"
                : "stable"
              : undefined
          }
        />
        <SensorCard
          title="Humidity"
          value={sensorData.humidity !== null ? sensorData.humidity.toFixed(1) : "--"}
          unit="%"
          icon={Droplets}
          status={sensorData.humidity !== null ? "online" : "offline"}
          trend={
            sensorData.humidity !== null
              ? sensorData.humidity > 70
                ? "up"
                : sensorData.humidity < 40
                ? "down"
                : "stable"
              : undefined
          }
        />
        <SensorCard
          title="Air Quality"
          value={sensorData.airQualityPercent !== null ? (sensorData.airQualityPercent).toFixed(0) : "--"}
          unit="%"
          icon={Wind}
          status={sensorData.airQualityPercent !== null ? "online" : "offline"}
          trend={
            sensorData.airQualityPercent !== null
              ? sensorData.airQualityPercent > 0.85
                ? "up"
                : "stable"
              : undefined
          }
        />
        <SensorCard
          title="Air Quality (Raw)"
          value={sensorData.airQuality !== null ? sensorData.airQuality.toFixed(0) : "--"}
          unit="PPM"
          icon={Wind}
          status={sensorData.airQuality !== null ? "online" : "offline"}
        />
        <SensorCard
          title="Motion Detected"
          value={sensorData.peoplePresent ?? false}
          icon={Users}
          status={sensorData.peoplePresent === null ? "offline" : sensorData.peoplePresent ? "online" : "offline"}
          isBoolean
        />
        <SensorCard
          title="Window Status"
          value={sensorData.windowOpen ?? false}
          icon={DoorOpen}
          status={sensorData.windowOpen === null ? "offline" : sensorData.windowOpen ? "warning" : "online"}
          isBoolean
        />
      </div>

      <div className="grid gap-4 md:grid-cols-2 mt-6">
        <div className="p-6 border rounded-lg">
          <h3 className="font-semibold mb-2">System Status</h3>
          <div className="space-y-2 text-sm">
            <div className="flex justify-between">
              <span className="text-muted-foreground">Connection</span>
              <span className={`font-medium ${
                connectionStatus === "connected" ? "text-green-500" : 
                connectionStatus === "error" ? "text-red-500" : "text-yellow-500"
              }`}>
                {connectionStatus === "connected" ? "Connected" : 
                 connectionStatus === "error" ? "Disconnected" : "Connecting..."}
              </span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted-foreground">Stream Type</span>
              <span className="font-medium">Server-Sent Events</span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted-foreground">Last Update</span>
              <span className="font-medium">
                {sensorData.timestamp.toLocaleTimeString()}
              </span>
            </div>
          </div>
        </div>

        <div className="p-6 border rounded-lg">
          <h3 className="font-semibold mb-2">Quick Stats</h3>
          <div className="space-y-2 text-sm">
            <div className="flex justify-between">
              <span className="text-muted-foreground">Temperature</span>
              <span className="font-medium">
                {sensorData.temperature !== null ? `${sensorData.temperature.toFixed(1)}°C` : "--"}
              </span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted-foreground">Air Quality</span>
              <span className="font-medium">
                {sensorData.airQualityPercent !== null 
                  ? sensorData.airQualityPercent > 0.85 ? "Excellent" : sensorData.airQualityPercent > 0.6 ? "Good" : "Fair"
                  : "--"}
              </span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted-foreground">Room Occupancy</span>
              <span className="font-medium">
                {sensorData.peoplePresent !== null 
                  ? sensorData.peoplePresent ? "Motion Detected" : "Empty"
                  : "--"}
              </span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
