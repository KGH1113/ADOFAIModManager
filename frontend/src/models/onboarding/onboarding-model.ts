export const onboardingSteps = [
  "welcome",
  "experience",
  "alpha",
  "installation",
  "modSettings",
  "jrp",
  "keyBindings",
  "chatter",
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
export const setupSteps = (alphaInstalled: boolean, jrpInstalled = true) =>
  onboardingSteps.filter(
    (step) => (!alphaInstalled || step !== "alpha") && (jrpInstalled || step !== "keyBindings"),
  );
export function transition(
  state: OnboardingState,
  event: OnboardingEvent,
  alphaInstalled = false,
  jrpInstalled = true,
): OnboardingState {
  const sequence = setupSteps(alphaInstalled, jrpInstalled);
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
  jrp: {
    installedId: "JipperResourcePack",
    name: "JipperResourcePack",
    slug: "jrp",
    dependencies: ["jalib"],
  },
  chatter: {
    installedId: "KeyboardChatterBlocker",
    name: "Keyboard Chatter Blocker",
    slug: "keyboardchatterblocker",
  },
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
  | "jrp"
  | "chatter"
  | "tufLite"
  | "tuf"
  | "countdown"
  | "calibration"
  | "youtube"
  | "effects"
  | "ipc";
export const moreMods: RecommendationKey[] = ["countdown", "calibration", "youtube", "effects"];
export type OnboardingSetup =
  | "jrp-defaults"
  | "chatter-defaults"
  | "jrp-key-sync"
  | "tuf-overlay"
  | "countdown-defaults";
