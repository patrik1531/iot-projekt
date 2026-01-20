import { Card, CardContent, CardHeader, CardTitle } from "./ui/card";
import { LucideIcon } from "lucide-react";
import { motion } from "motion/react";

interface SensorCardProps {
  title: string;
  value: string | number;
  unit?: string;
  icon: LucideIcon;
  status?: "online" | "offline" | "warning";
  trend?: "up" | "down" | "stable";
  isBoolean?: boolean;
}

export function SensorCard({
  title,
  value,
  unit,
  icon: Icon,
  status = "online",
  trend,
  isBoolean = false,
}: SensorCardProps) {
  const statusColors = {
    online: "text-green-500",
    offline: "text-gray-400",
    warning: "text-yellow-500",
  };

  const trendColors = {
    up: "text-red-500",
    down: "text-blue-500",
    stable: "text-gray-500",
  };

  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.3 }}
    >
      <Card>
        <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
          <CardTitle className="text-sm font-medium">{title}</CardTitle>
          <Icon className={`h-4 w-4 ${statusColors[status]}`} />
        </CardHeader>
        <CardContent>
          <div className="flex items-baseline gap-2">
            <div className="text-2xl font-bold">
              {isBoolean ? (value ? "Yes" : "No") : value}
            </div>
            {unit && !isBoolean && (
              <span className="text-sm text-muted-foreground">{unit}</span>
            )}
          </div>
          {trend && (
            <p className={`text-xs ${trendColors[trend]} mt-1`}>
              {trend === "up" && "↑ Increasing"}
              {trend === "down" && "↓ Decreasing"}
              {trend === "stable" && "→ Stable"}
            </p>
          )}
        </CardContent>
      </Card>
    </motion.div>
  );
}
