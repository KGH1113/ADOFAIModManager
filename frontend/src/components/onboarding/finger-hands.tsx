import {
  animate,
  useMotionValue,
  useMotionValueEvent,
  useReducedMotion,
  useSpring,
} from "motion/react";
import { useCallback, useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { createHandPose, handAccentColour } from "@/models/onboarding/hand-rig";
import { type KeyViewerStyle, keySlotGuide } from "@/models/onboarding/key-bindings";
import { createHandRenderer, readHandAccent } from "./hand-renderer";

const fingerMotion = {
  pressDuration: 0.6,
  releaseDuration: 0.8,
  ease: [0.16, 1, 0.3, 1] as [number, number, number, number],
};

interface TransitionProps {
  index: number;
  active: boolean;
  onChange: (index: number, value: number) => void;
}

function FingerTransition({ index, active, onChange }: TransitionProps) {
  const reduced = useReducedMotion();
  const progress = useMotionValue(0);
  useMotionValueEvent(progress, "change", (value) => onChange(index, value));
  useEffect(() => {
    const destination = active ? 1 : 0;
    if (reduced) {
      progress.jump(destination);
      onChange(index, destination);
      return;
    }
    if (progress.get() === destination) return;
    // Retarget from the current pose when capture changes during an animation.
    const animation = animate(progress, destination, {
      type: "tween",
      duration: active ? fingerMotion.pressDuration : fingerMotion.releaseDuration,
      ease: fingerMotion.ease,
    });
    return () => animation.stop();
  }, [active, index, onChange, progress, reduced]);
  return null;
}

function HighlightTransition({ index, active, onChange }: TransitionProps) {
  const reduced = useReducedMotion();
  const target = useMotionValue(0);
  const spring = useSpring(target, { stiffness: 600, damping: 44, mass: 0.8 });
  const progress = reduced ? target : spring;
  useMotionValueEvent(progress, "change", (value) => onChange(index, value));
  useEffect(() => {
    target.set(active ? 1 : 0);
    if (reduced) {
      spring.jump(active ? 1 : 0);
      onChange(index, active ? 1 : 0);
    }
  }, [active, index, onChange, reduced, spring, target]);
  return null;
}

function Hand({
  side,
  active,
  highlighted,
}: {
  side: "left" | "right";
  active: number | null;
  highlighted: number | null;
}) {
  const canvas = useRef<HTMLCanvasElement>(null);
  const renderer = useRef<ReturnType<typeof createHandRenderer>>(null);
  const values = useRef([0, 0, 0, 0, 0]);
  const highlights = useRef([0, 0, 0, 0, 0, 0]);
  const accent = useRef(handAccentColour);
  const frame = useRef<number | null>(null);
  const [ready, setReady] = useState(false);
  const [faces, setFaces] = useState(() => createHandPose(values.current).faces);
  const paint = useCallback(() => {
    frame.current = null;
    if (renderer.current) renderer.current.render(values.current, highlights.current);
    else setFaces(createHandPose(values.current, highlights.current, accent.current).faces);
  }, []);
  const requestPaint = useCallback(() => {
    if (frame.current === null) frame.current = requestAnimationFrame(paint);
  }, [paint]);
  const change = useCallback(
    (index: number, value: number) => {
      values.current[index] = value;
      requestPaint();
    },
    [requestPaint],
  );
  const changeHighlight = useCallback(
    (index: number, value: number) => {
      highlights.current[index] = value;
      requestPaint();
    },
    [requestPaint],
  );

  useEffect(() => {
    const element = canvas.current;
    if (!element) return;
    function setup() {
      renderer.current?.dispose();
      accent.current = readHandAccent(element as HTMLCanvasElement);
      renderer.current = createHandRenderer(element as HTMLCanvasElement, accent.current);
      setReady(Boolean(renderer.current));
      paint();
    }
    function lost(event: Event) {
      event.preventDefault();
      renderer.current?.dispose();
      renderer.current = null;
      setReady(false);
      requestPaint();
    }
    setup();
    const observer =
      typeof ResizeObserver === "undefined" ? null : new ResizeObserver(requestPaint);
    observer?.observe(element);
    element.addEventListener("webglcontextlost", lost);
    element.addEventListener("webglcontextrestored", setup);
    window.addEventListener("resize", requestPaint);
    return () => {
      if (frame.current !== null) cancelAnimationFrame(frame.current);
      frame.current = null;
      observer?.disconnect();
      element.removeEventListener("webglcontextlost", lost);
      element.removeEventListener("webglcontextrestored", setup);
      window.removeEventListener("resize", requestPaint);
      renderer.current?.dispose();
      renderer.current = null;
    };
  }, [paint, requestPaint]);

  return (
    <div
      className={`finger-hand finger-hand-${side}`}
      aria-hidden="true"
      data-hand={side}
      data-active-finger={active ?? "none"}
      data-highlighted-finger={highlighted !== null && highlighted < 5 ? highlighted : "none"}
      data-highlighted-edge={highlighted === 5}
    >
      {[0, 1, 2, 3, 4].map((index) => (
        <FingerTransition key={index} index={index} active={active === index} onChange={change} />
      ))}
      {[0, 1, 2, 3, 4, 5].map((index) => (
        <HighlightTransition
          key={index}
          index={index}
          active={highlighted === index}
          onChange={changeHighlight}
        />
      ))}
      <svg
        viewBox="0 0 250 280"
        className={`hand-art${ready ? " is-hidden" : ""}`}
        aria-hidden="true"
      >
        {faces.map((face) => (
          <path
            key={face.id}
            d={`M${face.points.map(({ x, y }) => `${x} ${y}`).join(" L")} Z`}
            fill={face.fill}
            stroke={face.fill}
            strokeWidth={0.9}
            strokeLinejoin="round"
          />
        ))}
      </svg>
      <canvas ref={canvas} className={`hand-art${ready ? "" : " is-hidden"}`} />
    </div>
  );
}

export function FingerHands({
  style,
  activeSlot,
  hoveredSlot,
}: {
  style: KeyViewerStyle;
  activeSlot: number | null;
  hoveredSlot: number | null;
}) {
  const { t } = useTranslation();
  const activeGuide = activeSlot === null ? null : keySlotGuide(style, activeSlot);
  const hoverGuide = hoveredSlot === null ? null : keySlotGuide(style, hoveredSlot);
  return (
    <div className="finger-hands">
      {(["left", "right"] as const).map((side) => {
        const active = activeGuide?.hand === side ? activeGuide.finger : null;
        const highlighted = hoverGuide?.hand === side ? hoverGuide.highlight : null;
        return (
          <div key={side} className={`hand-guide${active !== null ? " is-active" : ""}`}>
            <Hand side={side} active={active} highlighted={highlighted} />
            <span>{t(`onboarding.keys.${side}Hand`)}</span>
          </div>
        );
      })}
    </div>
  );
}
