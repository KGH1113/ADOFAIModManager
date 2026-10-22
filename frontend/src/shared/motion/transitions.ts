import type { Transition } from "motion/react";

// Match SwiftUI's response and damping ratio, using a unit-mass spring.
function spring(response: number, dampingRatio: number): Transition {
  const frequency = (2 * Math.PI) / response;
  return {
    type: "spring",
    mass: 1,
    stiffness: frequency ** 2,
    damping: 2 * dampingRatio * frequency,
  };
}

export const motionTokens = {
  hover: spring(0.44, 0.86),
  pointer: spring(0.45, 1),
  page: spring(0.62, 0.9),
  welcome: spring(1.1, 0.85),
  opacity: { type: "tween", ease: "easeOut", duration: 0.22 } satisfies Transition,
};
