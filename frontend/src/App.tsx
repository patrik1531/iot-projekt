import { useState } from "react";
import { Dashboard } from "./components/Dashboard";
import { Statistics } from "./components/Statistics";
import { AIAgent } from "./components/AIAgent";
import { Devices } from "./components/Devices";
import { Automation } from "./components/Automation";
import { Alerts } from "./components/Alerts";
import { Button } from "./components/ui/button";
import { Badge } from "./components/ui/badge";
import { LayoutDashboard, BarChart3, Bot, Cpu, Zap, Bell } from "lucide-react";

type ViewType = "dashboard" | "statistics" | "ai" | "devices" | "automation" | "alerts";

export default function App() {
  const [currentView, setCurrentView] = useState<ViewType>("dashboard");
  const unreadAlerts = 2; // This would come from your alerts state in a real app

  const renderView = () => {
    switch (currentView) {
      case "dashboard":
        return <Dashboard />;
      case "statistics":
        return <Statistics />;
      case "ai":
        return <AIAgent />;
      case "devices":
        return <Devices />;
      case "automation":
        return <Automation />;
      case "alerts":
        return <Alerts />;
      default:
        return <Dashboard />;
    }
  };

  return (
    <div className="min-h-screen bg-background">
      <nav className="border-b sticky top-0 bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/60 z-50">
        <div className="container mx-auto px-4 py-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <LayoutDashboard className="h-6 w-6 text-primary" />
              <span className="font-semibold text-lg hidden lg:inline">IoT Monitoring</span>
            </div>
            <div className="flex gap-2 flex-wrap">
              <Button
                variant={currentView === "dashboard" ? "default" : "ghost"}
                onClick={() => setCurrentView("dashboard")}
                className="gap-2"
                size="sm"
              >
                <LayoutDashboard className="h-4 w-4" />
                <span className="hidden sm:inline">Dashboard</span>
              </Button>
              <Button
                variant={currentView === "statistics" ? "default" : "ghost"}
                onClick={() => setCurrentView("statistics")}
                className="gap-2"
                size="sm"
              >
                <BarChart3 className="h-4 w-4" />
                <span className="hidden sm:inline">Statistics</span>
              </Button>
              <Button
                variant={currentView === "ai" ? "default" : "ghost"}
                onClick={() => setCurrentView("ai")}
                className="gap-2"
                size="sm"
              >
                <Bot className="h-4 w-4" />
                <span className="hidden sm:inline">AI Agent</span>
              </Button>
              <Button
                variant={currentView === "devices" ? "default" : "ghost"}
                onClick={() => setCurrentView("devices")}
                className="gap-2"
                size="sm"
              >
                <Cpu className="h-4 w-4" />
                <span className="hidden sm:inline">Devices</span>
              </Button>
              <Button
                variant={currentView === "automation" ? "default" : "ghost"}
                onClick={() => setCurrentView("automation")}
                className="gap-2"
                size="sm"
              >
                <Zap className="h-4 w-4" />
                <span className="hidden sm:inline">Automation</span>
              </Button>
              <Button
                variant={currentView === "alerts" ? "default" : "ghost"}
                onClick={() => setCurrentView("alerts")}
                className="gap-2 relative"
                size="sm"
              >
                <Bell className="h-4 w-4" />
                <span className="hidden sm:inline">Alerts</span>
            
              </Button>
            </div>
          </div>
        </div>
      </nav>

      <main className="container mx-auto px-4 py-8">
        {renderView()}
      </main>
    </div>
  );
}