// Values for the illustrated Element HUD in the onboarding demo. A short practice run
// loops forever; every number is derived from the beat so the scene stays deterministic.
export const demoBpm = 150;
export const demoLength = 96;
export const judgmentKinds = [
  "tooEarly",
  "early",
  "ePerfect",
  "perfect",
  "lPerfect",
  "late",
  "tooLate",
] as const;
export type JudgmentKind = (typeof judgmentKinds)[number];

export function judgmentAt(beat: number): JudgmentKind {
  if (beat % 23 === 17) return "early";
  if (beat % 9 === 4) return "ePerfect";
  if (beat % 13 === 7) return "lPerfect";
  return "perfect";
}

const clock = (seconds: number) =>
  `${Math.floor(seconds / 60)}:${String(Math.floor(seconds % 60)).padStart(2, "0")}`;

export function demoHud(beat: number) {
  const run = Math.floor(beat / demoLength);
  const step = beat % demoLength;
  const hits = step + 1;
  const counts = Object.fromEntries(judgmentKinds.map((kind) => [kind, 0])) as Record<
    JudgmentKind,
    number
  >;
  for (let index = 0; index < hits; index++) counts[judgmentAt(run * demoLength + index)]++;
  const weighted =
    counts.perfect +
    0.75 * (counts.ePerfect + counts.lPerfect) +
    0.4 * (counts.early + counts.late);
  const secondsPerBeat = 60 / demoBpm;
  return {
    progress: `${((hits / demoLength) * 100).toFixed(1)}%`,
    accuracy: `${((weighted / hits) * 100).toFixed(2)}%`,
    musicTime: `${clock(hits * secondsPerBeat)}-${clock(demoLength * secondsPerBeat)}`,
    // An Early ends the Perfect streak, like the combo counter in the game.
    combo: step - lastBreak(run, step),
    counts,
    attempt: run + 1,
    fullAttempt: run + 4,
    fill: hits / demoLength,
  };
}

/** Index of the latest Early in this run up to `step`, or -1 when there was none. */
function lastBreak(run: number, step: number) {
  for (let index = step; index >= 0; index--)
    if (judgmentAt(run * demoLength + index) === "early") return index;
  return -1;
}

// Keys from ElementNote's default 16K tab that the demo key viewer presses, in order.
export const demoKeyPattern = [3, 4, 2, 5, 11, 12, 1, 6, 3, 4, 10, 13] as const;
