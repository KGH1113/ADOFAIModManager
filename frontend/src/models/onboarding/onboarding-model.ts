export const onboardingSteps = [
  "welcome",
  "experience",
  "alpha",
  "installation",
  "modSettings",
  "element",
  "elementStart",
  "tufIntro",
  "tuf",
  "more",
  "ready",
] as const;
export type OnboardingStep = (typeof onboardingSteps)[number];
export interface OnboardingState {
  step: OnboardingStep;
  readyBack: OnboardingStep;
}
export type OnboardingEvent = "continue" | "returning" | "new" | "next" | "defer" | "back";
// The first-launch guide only applies once Element is actually in the Mods folder.
export const setupSteps = (alphaInstalled: boolean, elementInstalled = true) =>
  onboardingSteps.filter(
    (step) =>
      (!alphaInstalled || step !== "alpha") && (elementInstalled || step !== "elementStart"),
  );
export function transition(
  state: OnboardingState,
  event: OnboardingEvent,
  alphaInstalled = false,
  elementInstalled = true,
): OnboardingState {
  const sequence = setupSteps(alphaInstalled, elementInstalled);
  switch (event) {
    case "continue":
      return state.step === "welcome" ? { ...state, step: "experience" } : state;
    case "returning":
      return { step: "ready", readyBack: "experience" };
    case "new":
      return { step: alphaInstalled ? "installation" : "alpha", readyBack: "experience" };
    case "defer":
      return { step: "ready", readyBack: state.step };
    case "next":
      return {
        step:
          state.step === "alpha"
            ? "installation"
            : sequence[Math.min(sequence.indexOf(state.step) + 1, sequence.length - 1)],
        readyBack: state.step,
      };
    case "back":
      return {
        ...state,
        step:
          state.step === "ready"
            ? state.readyBack
            : sequence[Math.max(1, sequence.indexOf(state.step) - 1)],
      };
  }
}
export interface RecommendedMod {
  installedId: string;
  name: string;
  slug: string;
  dependencies?: RecommendationKey[];
}
export const recommendations = {
  jalib: { installedId: "JALib", name: "JALib", slug: "jalib" },
  element: { installedId: "Element", name: "Element", slug: "element" },
  tufLite: {
    installedId: "TUFHelperLite",
    name: "TUFHelperLite",
    slug: "tufhelperlite",
  },
  tuf: { installedId: "TUFHelper", name: "TUFHelper", slug: "tufhelper" },
  countdown: {
    installedId: "EnhancedCountdown",
    name: "Enhanced Countdown",
    slug: "enhanced-countdown",
  },
  calibration: {
    installedId: "BetterCalibration",
    name: "Better Calibration",
    slug: "bettercalibration",
    dependencies: ["jalib"],
  },
  youtube: { installedId: "YouTubeStream", name: "YouTubeStream", slug: "youtubestream" },
  effects: {
    installedId: "EnhancedEffectRemover",
    name: "Enhanced Effect Remover",
    slug: "enhancedeffectremover",
  },
  ipc: { installedId: "AdofaiIpc", name: "ADOFAI IPC", slug: "adofaiipc" },
} satisfies Record<string, RecommendedMod>;
export type RecommendationKey =
  | "jalib"
  | "element"
  | "tufLite"
  | "tuf"
  | "countdown"
  | "calibration"
  | "youtube"
  | "effects"
  | "ipc";
export const moreMods: RecommendationKey[] = ["countdown", "calibration", "youtube", "effects"];
// Element's Effects tab already removes level effects, so a second remover would only overlap.
export const moreModsFor = (elementInstalled: boolean) =>
  elementInstalled ? moreMods.filter((key) => key !== "effects") : moreMods;
export type OnboardingSetup =
  | "jrp-defaults"
  | "chatter-defaults"
  | "jrp-key-sync"
  | "tuf-overlay"
  | "countdown-defaults";
