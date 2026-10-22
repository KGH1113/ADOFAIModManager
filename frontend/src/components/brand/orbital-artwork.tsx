import {
  motion,
  useAnimationFrame,
  useMotionValue,
  useReducedMotion,
  useSpring,
} from "motion/react";
import { useEffect, useId } from "react";
import { usePageActive } from "@/hooks/shared/use-page-active";
import { cn } from "@/shared/lib/cn";
import { motionTokens } from "@/shared/motion/transitions";

const trail = Array.from({ length: 18 }, (_, index) => index + 1);
const ticks = Array.from({ length: 12 }, (_, index) => index);

// Match the native canvas geometry, angular velocity, and trail falloff.
// Transforming the outer layer never resets the continuous orbital phase.
export function OrbitalArtwork({
  variant = "pair",
  energized = false,
  className,
}: {
  variant?: "pair" | "ice" | "fire";
  energized?: boolean;
  className?: string;
}) {
  const id = useId().replaceAll(":", "");
  const reduced = useReducedMotion();
  const active = usePageActive();
  const phase = useMotionValue(0);
  const speed = useSpring(34.3774677, { stiffness: 70, damping: 18 });
  useEffect(() => {
    speed.set(energized ? 60 : 34.3774677);
  }, [energized, speed]);
  useAnimationFrame((_time, delta) => {
    if (!reduced && active) phase.set(phase.get() + (Math.min(delta, 40) * speed.get()) / 1000);
  });
  const colors =
    variant === "ice"
      ? ["#81bcff"]
      : variant === "fire"
        ? ["#ff87a2", "#ff87a2"]
        : ["#81bcff", "#ff87a2"];
  return (
    <motion.div
      className={cn("orbital-artwork", `orbital-${variant}`, className)}
      aria-hidden="true"
      initial={false}
      animate={{ rotate: !reduced && energized ? 18 : 0, scale: !reduced && energized ? 1.08 : 1 }}
      transition={motionTokens.hover}
    >
      <svg viewBox="0 0 200 200" fill="none" focusable="false" aria-hidden="true">
        <defs>
          {colors.map((color, index) => (
            // The two fixed orbital slots never reorder.
            // biome-ignore lint/suspicious/noArrayIndexKey: fixed orbital slots
            <g key={`${id}-${index}`}>
              <radialGradient id={`${id}-body-${index}`} cx="35%" cy="30%" r="85%">
                <stop stopColor="white" />
                <stop offset="1" stopColor={color} />
              </radialGradient>
              <radialGradient id={`${id}-halo-${index}`}>
                <stop stopColor={color} stopOpacity="0.34" />
                <stop offset="1" stopColor={color} stopOpacity="0" />
              </radialGradient>
            </g>
          ))}
        </defs>
        {[49, 66, 83].map((radius, index) => (
          <circle
            className="orbit-ring"
            key={radius}
            cx="100"
            cy="100"
            r={radius}
            stroke="currentColor"
            strokeOpacity={index === 1 ? 0.3 : 0.14}
            strokeWidth="1"
            strokeDasharray={index === 2 ? "2 6" : undefined}
          />
        ))}
        {ticks.map((index) => (
          <path
            key={index}
            d="M191 100h4"
            stroke="currentColor"
            strokeOpacity="0.4"
            strokeWidth="1"
            transform={`rotate(${index * 30} 100 100)`}
          />
        ))}
        <motion.g
          className="planet-system"
          style={{ rotate: phase, transformOrigin: "100px 100px" }}
        >
          {colors.map((color, index) => {
            const angle = -Math.PI / 4 + index * Math.PI;
            const x = 100 + Math.cos(angle) * 49;
            const y = 100 + Math.sin(angle) * 49;
            return (
              // biome-ignore lint/suspicious/noArrayIndexKey: fixed orbital slots
              <g key={`${id}-planet-${index}`}>
                {trail.map((step) => (
                  <circle
                    key={step}
                    cx={100 + Math.cos(angle - step * 0.045) * 49}
                    cy={100 + Math.sin(angle - step * 0.045) * 49}
                    r={1.6 * (1 - step / 22)}
                    fill={color}
                    fillOpacity={0.5 * (1 - step / 20)}
                  />
                ))}
                <circle cx={x} cy={y} r="34.4" fill={`url(#${id}-halo-${index})`} />
                <circle cx={x} cy={y} r="8.6" fill={`url(#${id}-body-${index})`} />
              </g>
            );
          })}
        </motion.g>
      </svg>
    </motion.div>
  );
}
