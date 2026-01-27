import { useState, useEffect, useRef } from "react";
import { Card, CardContent, CardHeader, CardTitle } from "./ui/card";
import { Button } from "./ui/button";
import { Badge } from "./ui/badge";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "./ui/tabs";
import { Bell, AlertTriangle, Info, CheckCircle, X, Wifi, WifiOff } from "lucide-react";
import { motion, AnimatePresence } from "motion/react";

interface Alert {
  id: string;
  type: "info" | "warning" | "error" | "success";
  title: string;
  message: string;
  device?: string;
  timestamp: Date;
  read: boolean;
}

interface VentilationEventFromServer {
  id: number;
  dt: string;
  deviceId: string;
  co2Ppm: number;
  windowOpen: boolean;
  level: number;
  type: "info" | "warning" | "error" | "success";
  action: number;
  message: string;
  title: string;
}

const SSE_ENDPOINT = "/api/sensor-events/stream";

export function Alerts() {
  const [alerts, setAlerts] = useState<Alert[]>([]);
  const [connectionStatus, setConnectionStatus] = useState<"connecting" | "connected" | "error">("connecting");
  const eventSourceRef = useRef<EventSource | null>(null);

  // Connect to SSE stream
  useEffect(() => {
    const eventSource = new EventSource(SSE_ENDPOINT);
    eventSourceRef.current = eventSource;

    eventSource.onopen = () => {
      console.log("Alerts SSE connected");
      setConnectionStatus("connected");
    };

    eventSource.onerror = (e) => {
      console.error("Alerts SSE error:", e);
      setConnectionStatus("error");
    };

    // Handle VentilationEvent-update (alerts from backend)
    eventSource.addEventListener("VentilationEvent-update", (event) => {
      const data = JSON.parse(event.data);
      const ventEvent: VentilationEventFromServer = data.value;
      console.log("New ventilation alert:", ventEvent);
      
      const newAlert: Alert = {
        id: `${ventEvent.id}-${Date.now()}`, // Unique ID
        type: ventEvent.type,
        title: ventEvent.title,
        message: ventEvent.message,
        device: `CO₂ Sensor (${ventEvent.co2Ppm} ppm)`,
        timestamp: new Date(ventEvent.dt),
        read: false,
      };
      
      // Add new alert at the beginning
      setAlerts((prev) => [newAlert, ...prev]);
    });

    // Cleanup on unmount
    return () => {
      eventSource.close();
    };
  }, []);

  const markAsRead = (id: string) => {
    setAlerts((prev) =>
      prev.map((alert) => (alert.id === id ? { ...alert, read: true } : alert))
    );
  };

  const markAllAsRead = () => {
    setAlerts((prev) => prev.map((alert) => ({ ...alert, read: true })));
  };

  const deleteAlert = (id: string) => {
    setAlerts((prev) => prev.filter((alert) => alert.id !== id));
  };

  const getAlertIcon = (type: string) => {
    switch (type) {
      case "warning":
        return <AlertTriangle className="h-5 w-5 text-yellow-500" />;
      case "error":
        return <AlertTriangle className="h-5 w-5 text-red-500" />;
      case "success":
        return <CheckCircle className="h-5 w-5 text-green-500" />;
      default:
        return <Info className="h-5 w-5 text-blue-500" />;
    }
  };

  const getAlertBadgeVariant = (type: string): "default" | "destructive" | "secondary" => {
    switch (type) {
      case "error":
        return "destructive";
      case "warning":
        return "secondary";
      default:
        return "default";
    }
  };

  const unreadCount = alerts.filter((a) => !a.read).length;
  const unreadAlerts = alerts.filter((a) => !a.read);

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-3xl font-bold">Alerts</h1>
            {unreadCount > 0 && (
              <Badge variant="destructive">{unreadCount} new</Badge>
            )}
            {/* Connection status indicator */}
            <div className="ml-2" title={`Status: ${connectionStatus}`}>
              {connectionStatus === "connected" ? (
                <Wifi className="h-4 w-4 text-green-500" />
              ) : connectionStatus === "error" ? (
                <WifiOff className="h-4 w-4 text-red-500" />
              ) : (
                <Wifi className="h-4 w-4 text-yellow-500 animate-pulse" />
              )}
            </div>
          </div>
          <p className="text-muted-foreground">
            System notifications and alerts
            {connectionStatus === "error" && (
              <span className="text-red-500 ml-2">(Disconnected)</span>
            )}
          </p>
        </div>
        {unreadCount > 0 && (
          <Button variant="outline" onClick={markAllAsRead}>
            <CheckCircle className="h-4 w-4 mr-2" />
            Mark All as Read
          </Button>
        )}
      </div>

      <div className="grid gap-4 md:grid-cols-4">
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-sm text-muted-foreground">Total</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{alerts.length}</div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-sm text-muted-foreground">Unread</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{unreadCount}</div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-sm text-muted-foreground">Warnings</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">
              {alerts.filter((a) => a.type === "warning").length}
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-sm text-muted-foreground">Errors</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">
              {alerts.filter((a) => a.type === "error").length}
            </div>
          </CardContent>
        </Card>
      </div>

      <Tabs defaultValue="unread">
        <TabsList>
          <TabsTrigger value="unread">
            Unread {unreadCount > 0 && `(${unreadCount})`}
          </TabsTrigger>
          <TabsTrigger value="all">All</TabsTrigger>
          <TabsTrigger value="warnings">Warnings</TabsTrigger>
          <TabsTrigger value="errors">Errors</TabsTrigger>
        </TabsList>

        <TabsContent value="unread" className="space-y-3">
          {unreadAlerts.length === 0 ? (
            <Card>
              <CardContent className="flex flex-col items-center justify-center py-10">
                <Bell className="h-12 w-12 text-muted-foreground mb-2" />
                <p className="text-muted-foreground">No unread alerts</p>
              </CardContent>
            </Card>
          ) : (
            <AnimatePresence>
              {unreadAlerts.map((alert, index) => (
                <motion.div
                  key={alert.id}
                  initial={{ opacity: 0, x: -20 }}
                  animate={{ opacity: 1, x: 0 }}
                  exit={{ opacity: 0, x: 20 }}
                  transition={{ delay: index * 0.05 }}
                >
                  <Card className="border-l-4 border-l-primary">
                    <CardContent className="pt-6">
                      <div className="flex items-start justify-between gap-4">
                        <div className="flex items-start gap-3 flex-1">
                          {getAlertIcon(alert.type)}
                          <div className="flex-1">
                            <div className="flex items-center gap-2 mb-1">
                              <h3 className="font-semibold">{alert.title}</h3>
                              <Badge variant={getAlertBadgeVariant(alert.type)}>
                                {alert.type}
                              </Badge>
                            </div>
                            <p className="text-sm text-muted-foreground mb-2">
                              {alert.message}
                            </p>
                            <div className="flex items-center gap-4 text-xs text-muted-foreground">
                              {alert.device && <span>Device: {alert.device}</span>}
                              <span>{alert.timestamp.toLocaleString()}</span>
                            </div>
                          </div>
                        </div>
                        <div className="flex gap-2">
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => markAsRead(alert.id)}
                          >
                            Mark Read
                          </Button>
                          <Button
                            variant="ghost"
                            size="icon"
                            onClick={() => deleteAlert(alert.id)}
                          >
                            <X className="h-4 w-4" />
                          </Button>
                        </div>
                      </div>
                    </CardContent>
                  </Card>
                </motion.div>
              ))}
            </AnimatePresence>
          )}
        </TabsContent>

        <TabsContent value="all" className="space-y-3">
          {alerts.length === 0 ? (
            <Card>
              <CardContent className="flex flex-col items-center justify-center py-10">
                <Bell className="h-12 w-12 text-muted-foreground mb-2" />
                <p className="text-muted-foreground">No alerts yet</p>
              </CardContent>
            </Card>
          ) : (
            alerts.map((alert, index) => (
              <motion.div
                key={alert.id}
                initial={{ opacity: 0, x: -20 }}
                animate={{ opacity: 1, x: 0 }}
                transition={{ delay: index * 0.05 }}
              >
                <Card className={!alert.read ? "border-l-4 border-l-primary" : ""}>
                  <CardContent className="pt-6">
                    <div className="flex items-start justify-between gap-4">
                      <div className="flex items-start gap-3 flex-1">
                        {getAlertIcon(alert.type)}
                        <div className="flex-1">
                          <div className="flex items-center gap-2 mb-1">
                            <h3 className="font-semibold">{alert.title}</h3>
                            <Badge variant={getAlertBadgeVariant(alert.type)}>
                              {alert.type}
                            </Badge>
                          </div>
                          <p className="text-sm text-muted-foreground mb-2">
                            {alert.message}
                          </p>
                          <div className="flex items-center gap-4 text-xs text-muted-foreground">
                            {alert.device && <span>Device: {alert.device}</span>}
                            <span>{alert.timestamp.toLocaleString()}</span>
                          </div>
                        </div>
                      </div>
                      <Button
                        variant="ghost"
                        size="icon"
                        onClick={() => deleteAlert(alert.id)}
                      >
                        <X className="h-4 w-4" />
                      </Button>
                    </div>
                  </CardContent>
                </Card>
              </motion.div>
            ))
          )}
        </TabsContent>

        <TabsContent value="warnings" className="space-y-3">
          {alerts.filter((a) => a.type === "warning").length === 0 ? (
            <Card>
              <CardContent className="flex flex-col items-center justify-center py-10">
                <AlertTriangle className="h-12 w-12 text-muted-foreground mb-2" />
                <p className="text-muted-foreground">No warnings</p>
              </CardContent>
            </Card>
          ) : (
            alerts
              .filter((a) => a.type === "warning")
              .map((alert, index) => (
                <motion.div
                  key={alert.id}
                  initial={{ opacity: 0, x: -20 }}
                  animate={{ opacity: 1, x: 0 }}
                  transition={{ delay: index * 0.05 }}
                >
                  <Card className={!alert.read ? "border-l-4 border-l-primary" : ""}>
                    <CardContent className="pt-6">
                      <div className="flex items-start justify-between gap-4">
                        <div className="flex items-start gap-3 flex-1">
                          {getAlertIcon(alert.type)}
                          <div className="flex-1">
                            <div className="flex items-center gap-2 mb-1">
                              <h3 className="font-semibold">{alert.title}</h3>
                              <Badge variant={getAlertBadgeVariant(alert.type)}>
                                {alert.type}
                              </Badge>
                            </div>
                            <p className="text-sm text-muted-foreground mb-2">
                              {alert.message}
                            </p>
                            <div className="flex items-center gap-4 text-xs text-muted-foreground">
                              {alert.device && <span>Device: {alert.device}</span>}
                              <span>{alert.timestamp.toLocaleString()}</span>
                            </div>
                          </div>
                        </div>
                        <Button
                          variant="ghost"
                          size="icon"
                          onClick={() => deleteAlert(alert.id)}
                        >
                          <X className="h-4 w-4" />
                        </Button>
                      </div>
                    </CardContent>
                  </Card>
                </motion.div>
              ))
          )}
        </TabsContent>

        <TabsContent value="errors" className="space-y-3">
          {alerts.filter((a) => a.type === "error").length === 0 ? (
            <Card>
              <CardContent className="flex flex-col items-center justify-center py-10">
                <AlertTriangle className="h-12 w-12 text-muted-foreground mb-2" />
                <p className="text-muted-foreground">No errors</p>
              </CardContent>
            </Card>
          ) : (
            alerts
              .filter((a) => a.type === "error")
              .map((alert, index) => (
                <motion.div
                  key={alert.id}
                  initial={{ opacity: 0, x: -20 }}
                  animate={{ opacity: 1, x: 0 }}
                  transition={{ delay: index * 0.05 }}
                >
                  <Card className={!alert.read ? "border-l-4 border-l-primary" : ""}>
                    <CardContent className="pt-6">
                      <div className="flex items-start justify-between gap-4">
                        <div className="flex items-start gap-3 flex-1">
                          {getAlertIcon(alert.type)}
                          <div className="flex-1">
                            <div className="flex items-center gap-2 mb-1">
                              <h3 className="font-semibold">{alert.title}</h3>
                              <Badge variant={getAlertBadgeVariant(alert.type)}>
                                {alert.type}
                              </Badge>
                            </div>
                            <p className="text-sm text-muted-foreground mb-2">
                              {alert.message}
                            </p>
                            <div className="flex items-center gap-4 text-xs text-muted-foreground">
                              {alert.device && <span>Device: {alert.device}</span>}
                              <span>{alert.timestamp.toLocaleString()}</span>
                            </div>
                          </div>
                        </div>
                        <Button
                          variant="ghost"
                          size="icon"
                          onClick={() => deleteAlert(alert.id)}
                        >
                          <X className="h-4 w-4" />
                        </Button>
                      </div>
                    </CardContent>
                  </Card>
                </motion.div>
              ))
          )}
        </TabsContent>
      </Tabs>
    </div>
  );
}