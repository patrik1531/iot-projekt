import { useState } from "react";
import { Card, CardContent, CardHeader, CardTitle } from "./ui/card";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "./ui/tabs";
import { Button } from "./ui/button";
import { Calendar } from "./ui/calendar";
import { Popover, PopoverContent, PopoverTrigger } from "./ui/popover";
import { CalendarIcon } from "lucide-react";
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

// Mock data generator for different time ranges
function generateMockData(range: string, metric: string) {
  const dataPoints = {
    days: 24,
    weeks: 7,
    months: 30,
    yearly: 12,
  };

  const points = dataPoints[range as keyof typeof dataPoints] || 24;
  const data = [];

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
      timestamp: new Date(Date.now() - (points - i) * 3600000).toISOString(),
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

  const data = generateMockData(timeRange, selectedMetric);

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
            </CardContent>
          </Card>

          <div className="grid gap-4 md:grid-cols-3">
            <Card>
              <CardHeader>
                <CardTitle className="text-sm">Average</CardTitle>
              </CardHeader>
              <CardContent>
                <div className="text-2xl font-bold">
                  {(
                    data.reduce((acc, d) => acc + d.value, 0) / data.length
                  ).toFixed(2)}
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
                  {Math.max(...data.map((d) => d.value)).toFixed(2)}
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
                  {Math.min(...data.map((d) => d.value)).toFixed(2)}
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