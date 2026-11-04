import { useEffect, useRef, useState } from "react";

/**
 * A display-synced beat clock. React re-renders once per beat; `onFrame` receives the
 * fractional beat every frame so callers can move elements through refs without
 * re-rendering. Pausing keeps the position, so the scene resumes where it stopped.
 */
export function useBeatClock(running: boolean, bpm: number, onFrame: (beats: number) => void) {
  const [beat, setBeat] = useState(0);
  const position = useRef(0);
  const frame = useRef(onFrame);
  useEffect(() => {
    frame.current = onFrame;
  }, [onFrame]);
  useEffect(() => {
    frame.current(position.current);
    if (!running) return;
    const period = 60_000 / bpm;
    let handle = 0;
    let origin: number | null = null;
    let last = Math.floor(position.current);
    const tick = (now: number) => {
      origin ??= now - position.current * period;
      const beats = (now - origin) / period;
      position.current = beats;
      const whole = Math.floor(beats);
      if (whole !== last) {
        last = whole;
        setBeat(whole);
      }
      frame.current(beats);
      handle = requestAnimationFrame(tick);
    };
    handle = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(handle);
  }, [running, bpm]);
  return beat;
}
