import { useEffect, useState } from "react";
import { SensorCard } from "./SensorCard";
import { LiveIndicator } from "./LiveIndicator";
import { Thermometer, Wind, Users, DoorOpen } from "lucide-react";

interface SensorData {
  temperature: number;
  airQuality: number;
  peoplePresent: boolean;
  windowOpen: boolean;
  timestamp: Date;
}

// Mock API call - replace with your actual API endpoints
async function fetchCurrentData(): Promise<SensorData> {
  // Simulate API delay
  await new Promise((resolve) => setTimeout(resolve, 100));
  
  return {
    temperature: 20 + Math.random() * 10,
    airQuality: 70 + Math.random() * 25,
    peoplePresent: Math.random() > 0.5,
    windowOpen: Math.random() > 0.7,
    timestamp: new Date(),
  };
}

export function Dashboard() {
  const [sensorData, setSensorData] = useState<SensorData | null>(null);
  const [lastUpdate, setLastUpdate] = useState<Date | null>(null);

  useEffect(() => {
    // Initial fetch
    const loadData = async () => {
      const data = await fetchCurrentData();
      setSensorData(data);
      setLastUpdate(new Date());
    };

    loadData();

    // Update every 5 seconds
    const interval = setInterval(loadData, 5000);

    return () => clearInterval(interval);
  }, []);

  if (!sensorData) {
    return (
      <div className="flex items-center justify-center h-screen">
        <div className="text-muted-foreground">Loading sensor data...</div>
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

      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
        <SensorCard
          title="Temperature"
          value={sensorData.temperature.toFixed(1)}
          unit="°C"
          icon={Thermometer}
          status="online"
          trend={
            sensorData.temperature > 25
              ? "up"
              : sensorData.temperature < 20
              ? "down"
              : "stable"
          }
        />
        <SensorCard
          title="Air Quality"
          value={sensorData.airQuality.toFixed(0)}
          unit="%"
          icon={Wind}
          status="online"
          trend={sensorData.airQuality > 85 ? "up" : "stable"}
        />
        <SensorCard
          title="People Present"
          value={sensorData.peoplePresent}
          icon={Users}
          status={sensorData.peoplePresent ? "online" : "offline"}
          isBoolean
        />
        <SensorCard
          title="Window Status"
          value={sensorData.windowOpen}
          icon={DoorOpen}
          status={sensorData.windowOpen ? "warning" : "online"}
          isBoolean
        />
      </div>

      <div className="grid gap-4 md:grid-cols-2 mt-6">
        <div className="p-6 border rounded-lg">
          <h3 className="font-semibold mb-2">System Status</h3>
          <div className="space-y-2 text-sm">
            <div className="flex justify-between">
              <span className="text-muted-foreground">All Sensors</span>
              <span className="text-green-500 font-medium">Online</span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted-foreground">Data Refresh</span>
              <span className="font-medium">5s interval</span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted-foreground">Last Sync</span>
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
              <span className="text-muted-foreground">Avg Temperature</span>
              <span className="font-medium">
                {sensorData.temperature.toFixed(1)}°C
              </span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted-foreground">Air Quality Index</span>
              <span className="font-medium">
                {sensorData.airQuality > 85 ? "Excellent" : "Good"}
              </span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted-foreground">Room Occupancy</span>
              <span className="font-medium">
                {sensorData.peoplePresent ? "Occupied" : "Empty"}
              </span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
