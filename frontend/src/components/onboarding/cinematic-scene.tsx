import { motion, useIsPresent, usePresenceData, useReducedMotion } from "motion/react";
import { type ReactNode, useEffect, useRef } from "react";

export function CinematicScene({ step, children }: { step: string; children: ReactNode }) {
  const root = useRef<HTMLElement>(null);
  const present = useIsPresent();
  const reduced = useReducedMotion();
  const direction = (usePresenceData() as number | undefined) ?? 1;
  useEffect(() => {
    if (present && step !== "welcome")
      root.current?.querySelector<HTMLElement>("#onboarding-title")?.focus({ preventScroll: true });
  }, [present, step]);
  return (
    <motion.main
      ref={root}
      className={`onboarding-content cinematic-scene step-${step}`}
      inert={!present}
      aria-hidden={!present}
      initial={{ opacity: 0, x: reduced ? 0 : 12 * direction }}
      animate={{ opacity: 1, x: 0 }}
      exit={{ opacity: 0, x: reduced ? 0 : -8 * direction }}
      transition={{ duration: reduced ? 0.1 : 0.22, ease: "easeOut" }}
    >
      <div className="scene-surface">{children}</div>
    </motion.main>
  );
}
