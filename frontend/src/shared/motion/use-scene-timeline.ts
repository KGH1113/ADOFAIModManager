import { useGSAP } from "@gsap/react";
import gsap from "gsap";
import { useReducedMotion } from "motion/react";
import { type RefObject, useEffect, useRef } from "react";
import { usePageActive } from "@/hooks/shared/use-page-active";

gsap.registerPlugin(useGSAP);

/** Scoped, cancellable cinematic motion. Normal input stays available throughout. */
export function useSceneTimeline(
  scope: RefObject<HTMLElement | null>,
  build: (timeline: gsap.core.Timeline, reduced: boolean) => void,
  dependencies: unknown[] = [],
  preservePresentation = false,
) {
  const reduced = !!useReducedMotion();
  const active = usePageActive();
  const timeline = useRef<gsap.core.Timeline | null>(null);
  useGSAP(
    () => {
      timeline.current?.kill();
      const sequence = gsap.timeline();
      timeline.current = sequence;
      build(sequence, reduced);
      if (!active) sequence.pause();
      return () => {
        timeline.current = null;
      };
    },
    { scope, dependencies: [...dependencies, reduced], revertOnUpdate: !preservePresentation },
  );
  useEffect(() => {
    if (active) timeline.current?.resume();
    else timeline.current?.pause();
  }, [active]);
}
