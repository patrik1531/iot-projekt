import React, { useState, useEffect } from "react";
import { Card, CardContent, CardHeader, CardTitle } from "./ui/card";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "./ui/tabs";
import { Button } from "./ui/button";
import { Calendar } from "./ui/calendar";
import { Popover, PopoverContent, PopoverTrigger } from "./ui/popover";
import { CalendarIcon, Loader2 } from "lucide-react";
import {
  LineChart,
  Line,
  AreaChart,
  Area,
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  Legend,
  ResponsiveContainer,
  ReferenceLine,
} from "recharts";

// API response type
interface SensorApiResponse {
  avg: number;
  min: number;
  max: number;
  data: {
    label: string;
    avgValue: number;
  }[];
}

// Chart data type
interface ChartData {
  name: string;
  value: number;
}

// API endpoints for all sensors
const sensorEndpoints: Record<string, Record<string, string>> = {
  temperature: {
    days: "/api/sensors/temperature/last-24h",
    weeks: "/api/sensors/temperature/last-week",
    months: "/api/sensors/temperature/last-month",
    yearly: "/api/sensors/temperature/last-year",
  },
  airQuality: {
    days: "/api/sensors/air-quality-percent/last-24h",
    weeks: "/api/sensors/air-quality-percent/last-week",
    months: "/api/sensors/air-quality-percent/last-month",
    yearly: "/api/sensors/air-quality-percent/last-year",
  },
  peoplePresent: {
    days: "/api/sensors/motion/last-24h",
    weeks: "/api/sensors/motion/last-week",
    months: "/api/sensors/motion/last-month",
    yearly: "/api/sensors/motion/last-year",
  },
  windowOpen: {
    days: "/api/sensors/window/last-24h",
    weeks: "/api/sensors/window/last-week",
    months: "/api/sensors/window/last-month",
    yearly: "/api/sensors/window/last-year",
  },
};

// Fallback mock data generator (used when API fails)
function generateMockData(range: string, metric: string): ChartData[] {
  const dataPoints = {
    days: 24,
    weeks: 7,
    months: 30,
    yearly: 12,
  };

  const points = dataPoints[range as keyof typeof dataPoints] || 24;
  const data: ChartData[] = [];

  for (let i = 0; i < points; i++) {
    let value;
    if (metric === "temperature") {
      value = 18 + Math.random() * 10;
    } else if (metric === "airQuality") {
      value = 70 + Math.random() * 25;
    } else if (metric === "peoplePresent") {
      value = Math.random() > 0.5 ? 1 : 0;
    } else {
      value = Math.random() > 0.7 ? 1 : 0;
    }

    data.push({
      name:
        range === "yearly"
          ? `M${i + 1}`
          : range === "days"
          ? `${i}:00`
          : `Day ${i + 1}`,
      value: parseFloat(value.toFixed(2)),
    });
  }

  return data;
}

// Custom tooltip for boolean charts
const CustomTooltip = ({ active, payload, label }: any) => {
  if (active && payload && payload.length) {
    return (
      <div className="bg-background border rounded-lg p-3 shadow-lg">
        <p className="font-semibold">{label}</p>
        <p className="text-sm" style={{ color: payload[0].color }}>
          {payload[0].name}: {payload[0].value === 1 ? "Yes" : "No"}
        </p>
      </div>
    );
  }
  return null;
};

export function Statistics() {
  const [selectedMetric, setSelectedMetric] = useState("temperature");
  const [timeRange, setTimeRange] = useState("days");
  const [customDate, setCustomDate] = useState<Date | undefined>(undefined);
  const [apiData, setApiData] = useState<SensorApiResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Fetch sensor data from API
  useEffect(() => {
    const fetchData = async () => {
      setLoading(true);
      setError(null);
      try {
        const endpoints = sensorEndpoints[selectedMetric];
        if (!endpoints || !endpoints[timeRange]) {
          throw new Error("No endpoint configured");
        }
        const endpoint = endpoints[timeRange];
        const response = await fetch(endpoint);
        
        if (!response.ok) {
          throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }
        
        // Check content type to ensure it's JSON
        const contentType = response.headers.get("content-type");
        if (!contentType || !contentType.includes("application/json")) {
          const text = await response.text();
          console.error("Non-JSON response:", text.substring(0, 200));
          throw new Error("Server returned non-JSON response");
        }
        
        const data: SensorApiResponse = await response.json();
        setApiData(data);
      } catch (err) {
        console.error("Fetch error:", err);
        setError(err instanceof Error ? err.message : "An error occurred");
        setApiData(null);
      } finally {
        setLoading(false);
      }
    };
    fetchData();
  }, [selectedMetric, timeRange]);

  // Transform API data or use mock data as fallback
  const data: ChartData[] = apiData
    ? apiData.data.map((item) => ({
        name: item.label,
        value: item.avgValue,
      }))
    : generateMockData(timeRange, selectedMetric);

  // Get statistics (from API if available, calculated otherwise)
  const stats = {
    avg: apiData
      ? apiData.avg
      : data.reduce((acc, d) => acc + d.value, 0) / data.length,
    max: apiData ? apiData.max : Math.max(...data.map((d) => d.value)),
    min: apiData ? apiData.min : Math.min(...data.map((d) => d.value)),
  };

  const metricConfig = {
    temperature: {
      title: "Temperature History",
      unit: "°C",
      color: "#ef4444",
    },
    airQuality: {
      title: "Air Quality History",
      unit: "%",
      color: "#3b82f6",
    },
    peoplePresent: {
      title: "People Presence History",
      unit: "",
      color: "#10b981",
    },
    windowOpen: {
      title: "Window Status History",
      unit: "",
      color: "#f59e0b",
    },
  };

  const config = metricConfig[selectedMetric as keyof typeof metricConfig];

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold">Statistics</h1>
        <p className="text-muted-foreground">
          Historical sensor data and trends
        </p>
      </div>

      <Tabs value={selectedMetric} onValueChange={setSelectedMetric}>
        <TabsList className="grid w-full grid-cols-4">
          <TabsTrigger value="temperature">Temperature</TabsTrigger>
          <TabsTrigger value="airQuality">Air Quality</TabsTrigger>
          <TabsTrigger value="peoplePresent">People</TabsTrigger>
          <TabsTrigger value="windowOpen">Window</TabsTrigger>
        </TabsList>

        <TabsContent value={selectedMetric} className="space-y-4">
          <div className="flex flex-wrap gap-2 items-center">
            <Button
              variant={timeRange === "days" ? "default" : "outline"}
              onClick={() => setTimeRange("days")}
              size="sm"
            >
              Last 24 Hours
            </Button>
            <Button
              variant={timeRange === "weeks" ? "default" : "outline"}
              onClick={() => setTimeRange("weeks")}
              size="sm"
            >
              Last 7 Days
            </Button>
            <Button
              variant={timeRange === "months" ? "default" : "outline"}
              onClick={() => setTimeRange("months")}
              size="sm"
            >
              Last 30 Days
            </Button>
            <Button
              variant={timeRange === "yearly" ? "default" : "outline"}
              onClick={() => setTimeRange("yearly")}
              size="sm"
            >
              Yearly
            </Button>
            <Popover>
              <PopoverTrigger asChild>
                <Button variant="outline" size="sm">
                  <CalendarIcon className="mr-2 h-4 w-4" />
                  Custom Range
                </Button>
              </PopoverTrigger>
              <PopoverContent className="w-auto p-0" align="start">
                <Calendar
                  mode="single"
                  selected={customDate}
                  onSelect={setCustomDate}
                  initialFocus
                />
              </PopoverContent>
            </Popover>
          </div>

          <Card>
            <CardHeader>
              <CardTitle>{config.title}</CardTitle>
            </CardHeader>
            <CardContent>
              {loading ? (
                <div className="flex items-center justify-center h-[400px]">
                  <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
                </div>
              ) : error ? (
                <div className="flex items-center justify-center h-[400px] text-destructive">
                  {error}
                </div>
              ) : (
              <ResponsiveContainer width="100%" height={400}>
                {selectedMetric === "peoplePresent" ||
                selectedMetric === "windowOpen" ? (
                  <LineChart data={data}>
                    <defs>
                      <linearGradient
                        id={`gradient${selectedMetric}`}
                        x1="0"
                        y1="0"
                        x2="0"
                        y2="1"
                      >
                        <stop
                          offset="5%"
                          stopColor={config.color}
                          stopOpacity={0.6}
                        />
                        <stop
                          offset="95%"
                          stopColor={config.color}
                          stopOpacity={0.1}
                        />
                      </linearGradient>
                    </defs>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis dataKey="name" />
                    <YAxis domain={[0, 1.2]} ticks={[0, 1]} />
                    <Tooltip content={<CustomTooltip />} />
                    <Legend />
                    <ReferenceLine y={0.5} stroke="#ccc" strokeDasharray="3 3" />
                    <Line
                      type="stepAfter"
                      dataKey="value"
                      stroke={config.color}
                      strokeWidth={2}
                      strokeDasharray="5 5"
                      dot={{ fill: config.color, r: 4 }}
                      name={
                        selectedMetric === "peoplePresent"
                          ? "Present"
                          : "Open"
                      }
                    />
                    <Area
                      type="stepAfter"
                      dataKey="value"
                      stroke="none"
                      fill={`url(#gradient${selectedMetric})`}
                      fillOpacity={1}
                    />
                  </LineChart>
                ) : (
                  <AreaChart data={data}>
                    <defs>
                      <linearGradient
                        id={`color${selectedMetric}`}
                        x1="0"
                        y1="0"
                        x2="0"
                        y2="1"
                      >
                        <stop
                          offset="5%"
                          stopColor={config.color}
                          stopOpacity={0.8}
                        />
                        <stop
                          offset="95%"
                          stopColor={config.color}
                          stopOpacity={0}
                        />
                      </linearGradient>
                    </defs>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis dataKey="name" />
                    <YAxis />
                    <Tooltip />
                    <Legend />
                    <Area
                      type="monotone"
                      dataKey="value"
                      stroke={config.color}
                      fillOpacity={1}
                      fill={`url(#color${selectedMetric})`}
                      name={`${
                        selectedMetric === "temperature"
                          ? "Temperature"
                          : "Air Quality"
                      } ${config.unit}`}
                    />
                  </AreaChart>
                )}
              </ResponsiveContainer>
              )}
            </CardContent>
          </Card>

          <div className="grid gap-4 md:grid-cols-3">
            <Card>
              <CardHeader>
                <CardTitle className="text-sm">Average</CardTitle>
              </CardHeader>
              <CardContent>
                <div className="text-2xl font-bold">
                  {loading ? "—" : stats.avg.toFixed(2)}
                  {config.unit}
                </div>
              </CardContent>
            </Card>
            <Card>
              <CardHeader>
                <CardTitle className="text-sm">Maximum</CardTitle>
              </CardHeader>
              <CardContent>
                <div className="text-2xl font-bold">
                  {loading ? "—" : stats.max.toFixed(2)}
                  {config.unit}
                </div>
              </CardContent>
            </Card>
            <Card>
              <CardHeader>
                <CardTitle className="text-sm">Minimum</CardTitle>
              </CardHeader>
              <CardContent>
                <div className="text-2xl font-bold">
                  {loading ? "—" : stats.min.toFixed(2)}
                  {config.unit}
                </div>
              </CardContent>
            </Card>
          </div>
        </TabsContent>
      </Tabs>
    </div>
  );
}