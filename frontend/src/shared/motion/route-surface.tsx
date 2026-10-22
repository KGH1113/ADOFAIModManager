import { motion, useIsPresent, useReducedMotion } from "motion/react";
import type { ReactNode } from "react";
import { motionTokens } from "./transitions";

export function RouteSurface({
  children,
  screen = false,
}: {
  children: ReactNode;
  screen?: boolean;
}) {
  const reduced = useReducedMotion();
  const present = useIsPresent();
  return (
    <motion.div
      className={screen ? "app-route" : "workspace-route"}
      inert={!present}
      aria-hidden={!present}
      initial={{ opacity: 0, y: reduced ? 0 : 8 }}
      animate={{ opacity: 1, y: 0 }}
      exit={{ opacity: 0, y: reduced ? 0 : -6 }}
      transition={{ ...motionTokens.page, opacity: { duration: screen ? 0.28 : 0.18 } }}
    >
      {children}
    </motion.div>
  );
}
