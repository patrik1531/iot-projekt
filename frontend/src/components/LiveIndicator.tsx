import { motion } from "motion/react";

export function LiveIndicator() {
  return (
    <div className="flex items-center gap-2">
      <motion.div
        className="w-2 h-2 bg-green-500 rounded-full"
        animate={{
          opacity: [1, 0.3, 1],
        }}
        transition={{
          duration: 2,
          repeat: Infinity,
          ease: "easeInOut",
        }}
      />
      <span className="text-sm text-muted-foreground">Live</span>
    </div>
  );
}
