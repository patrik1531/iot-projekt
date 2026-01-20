import { useState } from "react";
import { Card, CardContent, CardHeader, CardTitle } from "./ui/card";
import { Button } from "./ui/button";
import { Badge } from "./ui/badge";
import { Switch } from "./ui/switch";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "./ui/select";
import { Plus, Trash2, Clock, Zap } from "lucide-react";
import { motion, AnimatePresence } from "motion/react";

interface AutomationRule {
  id: string;
  name: string;
  enabled: boolean;
  trigger: {
    type: string;
    condition: string;
    value: string;
  };
  action: {
    device: string;
    operation: string;
    value?: string;
  };
  lastTriggered?: Date;
}

export function Automation() {
  const [rules, setRules] = useState<AutomationRule[]>([
    {
      id: "1",
      name: "Morning Light",
      enabled: true,
      trigger: {
        type: "time",
        condition: "at",
        value: "07:00",
      },
      action: {
        device: "LED Strip",
        operation: "turn on",
        value: "75%",
      },
      lastTriggered: new Date(Date.now() - 3600000),
    },
    {
      id: "2",
      name: "Temperature Control",
      enabled: true,
      trigger: {
        type: "temperature",
        condition: "above",
        value: "25°C",
      },
      action: {
        device: "Ceiling Fan",
        operation: "turn on",
      },
      lastTriggered: new Date(Date.now() - 7200000),
    },
    {
      id: "3",
      name: "Window Safety",
      enabled: true,
      trigger: {
        type: "air_quality",
        condition: "below",
        value: "70%",
      },
      action: {
        device: "Window Actuator",
        operation: "open",
      },
    },
    {
      id: "4",
      name: "Night Mode",
      enabled: false,
      trigger: {
        type: "time",
        condition: "at",
        value: "22:00",
      },
      action: {
        device: "LED Strip",
        operation: "turn off",
      },
    },
  ]);

  const [showNewRule, setShowNewRule] = useState(false);

  const toggleRule = (id: string) => {
    setRules((prev) =>
      prev.map((rule) =>
        rule.id === id ? { ...rule, enabled: !rule.enabled } : rule
      )
    );
  };

  const deleteRule = (id: string) => {
    setRules((prev) => prev.filter((rule) => rule.id !== id));
  };

  const getTriggerIcon = (type: string) => {
    switch (type) {
      case "time":
        return <Clock className="h-4 w-4" />;
      default:
        return <Zap className="h-4 w-4" />;
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold">Automation</h1>
          <p className="text-muted-foreground">Create smart automation rules</p>
        </div>
        <Button onClick={() => setShowNewRule(!showNewRule)}>
          <Plus className="h-4 w-4 mr-2" />
          New Rule
        </Button>
      </div>

      <AnimatePresence>
        {showNewRule && (
          <motion.div
            initial={{ opacity: 0, height: 0 }}
            animate={{ opacity: 1, height: "auto" }}
            exit={{ opacity: 0, height: 0 }}
          >
            <Card>
              <CardHeader>
                <CardTitle>Create New Automation Rule</CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="grid gap-4 md:grid-cols-2">
                  <div className="space-y-2">
                    <label className="text-sm font-medium">Trigger Type</label>
                    <Select>
                      <SelectTrigger>
                        <SelectValue placeholder="Select trigger" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="time">Time</SelectItem>
                        <SelectItem value="temperature">Temperature</SelectItem>
                        <SelectItem value="air_quality">Air Quality</SelectItem>
                        <SelectItem value="motion">Motion</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="space-y-2">
                    <label className="text-sm font-medium">Condition</label>
                    <Select>
                      <SelectTrigger>
                        <SelectValue placeholder="Select condition" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="at">At</SelectItem>
                        <SelectItem value="above">Above</SelectItem>
                        <SelectItem value="below">Below</SelectItem>
                        <SelectItem value="equals">Equals</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="space-y-2">
                    <label className="text-sm font-medium">Device</label>
                    <Select>
                      <SelectTrigger>
                        <SelectValue placeholder="Select device" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="led">LED Strip</SelectItem>
                        <SelectItem value="fan">Ceiling Fan</SelectItem>
                        <SelectItem value="thermostat">Thermostat</SelectItem>
                        <SelectItem value="window">Window Actuator</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="space-y-2">
                    <label className="text-sm font-medium">Action</label>
                    <Select>
                      <SelectTrigger>
                        <SelectValue placeholder="Select action" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="on">Turn On</SelectItem>
                        <SelectItem value="off">Turn Off</SelectItem>
                        <SelectItem value="set">Set Value</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                </div>
                <div className="flex justify-end gap-2">
                  <Button variant="outline" onClick={() => setShowNewRule(false)}>
                    Cancel
                  </Button>
                  <Button onClick={() => setShowNewRule(false)}>Create Rule</Button>
                </div>
              </CardContent>
            </Card>
          </motion.div>
        )}
      </AnimatePresence>

      <div className="grid gap-4">
        {rules.map((rule, index) => (
          <motion.div
            key={rule.id}
            initial={{ opacity: 0, x: -20 }}
            animate={{ opacity: 1, x: 0 }}
            transition={{ delay: index * 0.05 }}
          >
            <Card>
              <CardHeader>
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-3">
                    <div
                      className={`p-2 rounded-lg ${
                        rule.enabled ? "bg-primary/10" : "bg-muted"
                      }`}
                    >
                      {getTriggerIcon(rule.trigger.type)}
                    </div>
                    <div>
                      <CardTitle className="text-base">{rule.name}</CardTitle>
                      <p className="text-sm text-muted-foreground">
                        When {rule.trigger.type} {rule.trigger.condition}{" "}
                        {rule.trigger.value}
                      </p>
                    </div>
                  </div>
                  <div className="flex items-center gap-2">
                    <Switch
                      checked={rule.enabled}
                      onCheckedChange={() => toggleRule(rule.id)}
                    />
                    <Button
                      variant="ghost"
                      size="icon"
                      onClick={() => deleteRule(rule.id)}
                    >
                      <Trash2 className="h-4 w-4 text-destructive" />
                    </Button>
                  </div>
                </div>
              </CardHeader>
              <CardContent>
                <div className="space-y-3">
                  <div className="flex items-center justify-between p-3 bg-muted rounded-lg">
                    <div>
                      <p className="text-sm font-medium">Action</p>
                      <p className="text-sm text-muted-foreground">
                        {rule.action.operation} {rule.action.device}
                        {rule.action.value && ` to ${rule.action.value}`}
                      </p>
                    </div>
                    <Badge variant={rule.enabled ? "default" : "secondary"}>
                      {rule.enabled ? "Active" : "Inactive"}
                    </Badge>
                  </div>
                  {rule.lastTriggered && (
                    <div className="flex justify-between text-xs text-muted-foreground">
                      <span>Last triggered</span>
                      <span>
                        {rule.lastTriggered.toLocaleDateString()}{" "}
                        {rule.lastTriggered.toLocaleTimeString()}
                      </span>
                    </div>
                  )}
                </div>
              </CardContent>
            </Card>
          </motion.div>
        ))}
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Automation Statistics</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid gap-4 md:grid-cols-3">
            <div>
              <p className="text-sm text-muted-foreground">Total Rules</p>
              <p className="text-2xl font-bold">{rules.length}</p>
            </div>
            <div>
              <p className="text-sm text-muted-foreground">Active Rules</p>
              <p className="text-2xl font-bold">
                {rules.filter((r) => r.enabled).length}
              </p>
            </div>
            <div>
              <p className="text-sm text-muted-foreground">Triggered Today</p>
              <p className="text-2xl font-bold">
                {
                  rules.filter(
                    (r) =>
                      r.lastTriggered &&
                      r.lastTriggered.toDateString() === new Date().toDateString()
                  ).length
                }
              </p>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
