import { useState, useRef, useEffect } from "react";
import { Card, CardContent, CardHeader, CardTitle } from "./ui/card";
import { Button } from "./ui/button";
import { Input } from "./ui/input";
import { Badge } from "./ui/badge";
import { ScrollArea } from "./ui/scroll-area";
import { Send, Bot, User, Zap, Power, Thermometer, Wind } from "lucide-react";
import { motion, AnimatePresence } from "motion/react";

interface Message {
  id: string;
  type: "user" | "agent";
  content: string;
  timestamp: Date;
  action?: {
    type: string;
    device: string;
    value?: any;
  };
}

interface DeviceAction {
  device: string;
  action: string;
  status: "success" | "pending" | "error";
}

export function AIAgent() {
  const [messages, setMessages] = useState<Message[]>([
    {
      id: "1",
      type: "agent",
      content:
        "Hello! I'm your IoT AI Assistant. I can help you control your Raspberry Pico devices. Try commands like 'turn on LED', 'set temperature to 22', or 'open window'.",
      timestamp: new Date(),
    },
  ]);
  const [input, setInput] = useState("");
  const [isProcessing, setIsProcessing] = useState(false);
  const [recentActions, setRecentActions] = useState<DeviceAction[]>([]);
  const scrollRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (scrollRef.current) {
      scrollRef.current.scrollTop = scrollRef.current.scrollHeight;
    }
  }, [messages]);

  const addRecentAction = (
    device: string,
    action: string,
    status: "success" | "pending" | "error"
  ) => {
    setRecentActions((prev) => [{ device, action, status }, ...prev.slice(0, 4)]);
  };

  // Helper function to parse actions from command/response for UI feedback
  const parseActionFromCommand = (command: string): Message["action"] | undefined => {
    const lowerCommand = command.toLowerCase();

    // Detect LED actions
    if (lowerCommand.includes("led") || lowerCommand.includes("light")) {
      if (lowerCommand.includes("turn on") || lowerCommand.includes("zapni")) {
        addRecentAction("LED Light", "Turned ON", "success");
        return { type: "power", device: "LED", value: true };
      } else if (lowerCommand.includes("turn off") || lowerCommand.includes("vypni")) {
        addRecentAction("LED Light", "Turned OFF", "success");
        return { type: "power", device: "LED", value: false };
      }
    }

    // Detect Fan actions
    if (lowerCommand.includes("fan") || lowerCommand.includes("ventilator")) {
      if (lowerCommand.includes("turn on") || lowerCommand.includes("zapni")) {
        addRecentAction("Fan", "Turned ON", "success");
        return { type: "power", device: "Fan", value: true };
      } else if (lowerCommand.includes("turn off") || lowerCommand.includes("vypni")) {
        addRecentAction("Fan", "Turned OFF", "success");
        return { type: "power", device: "Fan", value: false };
      }
    }

    // Detect Temperature actions
    if (lowerCommand.includes("temperature") || lowerCommand.includes("teplota")) {
      const tempMatch = lowerCommand.match(/(\d+)/);
      if (tempMatch) {
        const temp = tempMatch[1];
        addRecentAction("Thermostat", `Set to ${temp}°C`, "success");
        return { type: "temperature", device: "Thermostat", value: parseInt(temp) };
      }
    }

    // Detect Window actions
    if (lowerCommand.includes("window") || lowerCommand.includes("okno")) {
      if (lowerCommand.includes("open") || lowerCommand.includes("otvor")) {
        addRecentAction("Window", "Opened", "success");
        return { type: "window", device: "Window", value: true };
      } else if (lowerCommand.includes("close") || lowerCommand.includes("zatvor")) {
        addRecentAction("Window", "Closed", "success");
        return { type: "window", device: "Window", value: false };
      }
    }

    return undefined;
  };

  // Process command via API call to /api/reports/ask
  const processCommand = async (command: string): Promise<Message> => {
    try {
      const response = await fetch("/api/reports/ask", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({ userText: command }),
      });

      if (!response.ok) {
        throw new Error(`API error: ${response.status}`);
      }

      const data = await response.json();
      const responseText = data.text || "Command processed successfully.";

      // Parse the command to detect actions for the UI
      const action = parseActionFromCommand(command);

      return {
        id: Date.now().toString(),
        type: "agent",
        content: responseText,
        timestamp: new Date(),
        action,
      };
    } catch (error) {
      console.error("Error calling AI API:", error);
      
      // Update recent action to show error if we detected an action attempt
      const lowerCommand = command.toLowerCase();
      if (lowerCommand.includes("led") || lowerCommand.includes("light")) {
        addRecentAction("LED Light", "Command failed", "error");
      } else if (lowerCommand.includes("fan") || lowerCommand.includes("ventilator")) {
        addRecentAction("Fan", "Command failed", "error");
      } else if (lowerCommand.includes("temperature") || lowerCommand.includes("teplota")) {
        addRecentAction("Thermostat", "Command failed", "error");
      } else if (lowerCommand.includes("window") || lowerCommand.includes("okno")) {
        addRecentAction("Window", "Command failed", "error");
      }

      return {
        id: Date.now().toString(),
        type: "agent",
        content: "Sorry, I encountered an error processing your request. Please check your connection and try again.",
        timestamp: new Date(),
      };
    }
  };

  const handleSend = async () => {
    if (!input.trim() || isProcessing) return;

    const userMessage: Message = {
      id: Date.now().toString(),
      type: "user",
      content: input,
      timestamp: new Date(),
    };

    setMessages((prev) => [...prev, userMessage]);
    setInput("");
    setIsProcessing(true);

    const agentResponse = await processCommand(input);
    setMessages((prev) => [...prev, agentResponse]);
    setIsProcessing(false);
  };

  const quickCommands = [
    { label: "Turn ON LED", command: "turn on led" },
    { label: "Turn OFF LED", command: "turn off led" },
    { label: "Set Temp 22°C", command: "set temperature to 22" },
    { label: "Check Status", command: "status" },
  ];

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold">AI Control Center</h1>
        <p className="text-muted-foreground">
          Control your Raspberry Pico devices with natural language
        </p>
      </div>

      <div className="grid gap-6 lg:grid-cols-3">
        <div className="lg:col-span-2">
          <Card className="h-[600px] flex flex-col">
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Bot className="h-5 w-5" />
                AI Assistant Chat
              </CardTitle>
            </CardHeader>
            <CardContent className="flex-1 flex flex-col gap-4">
              <ScrollArea className="flex-1 pr-4" ref={scrollRef}>
                <div className="space-y-4">
                  <AnimatePresence>
                    {messages.map((message) => (
                      <motion.div
                        key={message.id}
                        initial={{ opacity: 0, y: 10 }}
                        animate={{ opacity: 1, y: 0 }}
                        exit={{ opacity: 0 }}
                        className={`flex gap-3 ${
                          message.type === "user" ? "justify-end" : "justify-start"
                        }`}
                      >
                        {message.type === "agent" && (
                          <div className="w-8 h-8 rounded-full bg-primary flex items-center justify-center flex-shrink-0">
                            <Bot className="h-4 w-4 text-primary-foreground" />
                          </div>
                        )}
                        <div
                          className={`max-w-[80%] rounded-lg p-3 ${
                            message.type === "user"
                              ? "bg-primary text-primary-foreground"
                              : "bg-muted"
                          }`}
                        >
                          <p className="text-sm">{message.content}</p>
                          <p className="text-xs opacity-70 mt-1">
                            {message.timestamp.toLocaleTimeString()}
                          </p>
                        </div>
                        {message.type === "user" && (
                          <div className="w-8 h-8 rounded-full bg-muted flex items-center justify-center flex-shrink-0">
                            <User className="h-4 w-4" />
                          </div>
                        )}
                      </motion.div>
                    ))}
                  </AnimatePresence>
                  {isProcessing && (
                    <motion.div
                      initial={{ opacity: 0 }}
                      animate={{ opacity: 1 }}
                      className="flex gap-3"
                    >
                      <div className="w-8 h-8 rounded-full bg-primary flex items-center justify-center">
                        <Bot className="h-4 w-4 text-primary-foreground" />
                      </div>
                      <div className="bg-muted rounded-lg p-3">
                        <div className="flex gap-1">
                          <div className="w-2 h-2 bg-foreground/50 rounded-full animate-bounce" />
                          <div
                            className="w-2 h-2 bg-foreground/50 rounded-full animate-bounce"
                            style={{ animationDelay: "0.1s" }}
                          />
                          <div
                            className="w-2 h-2 bg-foreground/50 rounded-full animate-bounce"
                            style={{ animationDelay: "0.2s" }}
                          />
                        </div>
                      </div>
                    </motion.div>
                  )}
                </div>
              </ScrollArea>

              <div className="space-y-3">
                <div className="flex flex-wrap gap-2">
                  {quickCommands.map((cmd) => (
                    <Button
                      key={cmd.command}
                      variant="outline"
                      size="sm"
                      onClick={() => {
                        setInput(cmd.command);
                      }}
                    >
                      <Zap className="h-3 w-3 mr-1" />
                      {cmd.label}
                    </Button>
                  ))}
                </div>

                <div className="flex gap-2">
                  <Input
                    placeholder="Type a command... (e.g., 'turn on LED')"
                    value={input}
                    onChange={(e) => setInput(e.target.value)}
                    onKeyDown={(e) => e.key === "Enter" && handleSend()}
                    disabled={isProcessing}
                  />
                  <Button onClick={handleSend} disabled={isProcessing || !input.trim()}>
                    <Send className="h-4 w-4" />
                  </Button>
                </div>
              </div>
            </CardContent>
          </Card>
        </div>

        <div className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle className="text-sm">Recent Actions</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="space-y-3">
                {recentActions.length === 0 ? (
                  <p className="text-sm text-muted-foreground">No recent actions</p>
                ) : (
                  recentActions.map((action, index) => (
                    <motion.div
                      key={index}
                      initial={{ opacity: 0, x: -10 }}
                      animate={{ opacity: 1, x: 0 }}
                      className="flex items-center justify-between p-2 bg-muted rounded-lg"
                    >
                      <div>
                        <p className="text-sm font-medium">{action.device}</p>
                        <p className="text-xs text-muted-foreground">{action.action}</p>
                      </div>
                      <Badge
                        variant={
                          action.status === "success"
                            ? "default"
                            : action.status === "error"
                            ? "destructive"
                            : "secondary"
                        }
                      >
                        {action.status}
                      </Badge>
                    </motion.div>
                  ))
                )}
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="text-sm">Quick Controls</CardTitle>
            </CardHeader>
            <CardContent className="space-y-2">
              <Button
                variant="outline"
                className="w-full justify-start"
                onClick={() => setInput("turn on led")}
              >
                <Power className="h-4 w-4 mr-2" />
                Toggle LED
              </Button>
              <Button
                variant="outline"
                className="w-full justify-start"
                onClick={() => setInput("set temperature to 22")}
              >
                <Thermometer className="h-4 w-4 mr-2" />
                Adjust Temperature
              </Button>
              <Button
                variant="outline"
                className="w-full justify-start"
                onClick={() => setInput("turn on fan")}
              >
                <Wind className="h-4 w-4 mr-2" />
                Toggle Fan
              </Button>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="text-sm">Connection Status</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="space-y-2">
                <div className="flex items-center justify-between">
                  <span className="text-sm text-muted-foreground">Raspberry Pico</span>
                  <Badge variant="default">Connected</Badge>
                </div>
                <div className="flex items-center justify-between">
                  <span className="text-sm text-muted-foreground">AI Agent</span>
                  <Badge variant="default">Online</Badge>
                </div>
                <div className="flex items-center justify-between">
                  <span className="text-sm text-muted-foreground">API Status</span>
                  <Badge variant="default">Active</Badge>
                </div>
              </div>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}