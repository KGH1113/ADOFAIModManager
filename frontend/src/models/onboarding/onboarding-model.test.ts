import { describe, expect, it } from "vitest";
import { moreModsFor, type OnboardingState, recommendations, transition } from "./onboarding-model";

describe("onboarding navigation", () => {
  it("supports the experienced and beginner paths with reversible skips", () => {
    const start: OnboardingState = { step: "welcome", readyBack: "experience" };
    expect(transition(transition(start, "continue"), "returning").step).toBe("ready");
    let state = transition(start, "new");
    expect(state.step).toBe("alpha");
    state = transition(state, "next");
    expect(state.step).toBe("installation");
    state = transition(state, "next");
    expect(state.step).toBe("modSettings");
    state = transition(state, "next");
    expect(state.step).toBe("element");
    state = transition(state, "next");
    expect(state.step).toBe("elementStart");
    state = transition(state, "next");
    expect(state.step).toBe("tufIntro");
    expect(transition(state, "back").step).toBe("elementStart");
    state = transition(state, "next");
    expect(state.step).toBe("tuf");
    expect(transition(state, "back").step).toBe("tufIntro");
    state = transition(state, "next");
    expect(state.step).toBe("more");
    state = transition(state, "next");
    expect(state.step).toBe("ready");
    expect(transition(state, "back").step).toBe("more");
    expect(
      transition(transition({ step: "installation", readyBack: "experience" }, "defer"), "back")
        .step,
    ).toBe("installation");
  });
  it("skips an installed alpha in both directions and after a refresh", () => {
    const experience: OnboardingState = { step: "experience", readyBack: "experience" };
    expect(transition(experience, "new", true).step).toBe("installation");
    expect(transition({ step: "installation", readyBack: "experience" }, "back", true).step).toBe(
      "experience",
    );
    expect(transition({ step: "installation", readyBack: "experience" }, "back", false).step).toBe(
      "alpha",
    );
    expect(transition({ step: "alpha", readyBack: "experience" }, "next", true).step).toBe(
      "installation",
    );
  });
  it("identifies recommendations by TUF slug instead of a display name", () => {
    expect(recommendations.element.slug).toBe("element");
    expect(recommendations.element.installedId).toBe("Element");
    expect(recommendations.calibration.slug).toBe("bettercalibration");
  });
  it("shows the first-launch guide only after Element is installed", () => {
    const element: OnboardingState = { step: "element", readyBack: "modSettings" };
    expect(transition(element, "next", false, false).step).toBe("tufIntro");
    expect(transition(element, "next", false, true).step).toBe("elementStart");
    expect(transition({ step: "tufIntro", readyBack: "element" }, "back", false, false).step).toBe(
      "element",
    );
    expect(transition({ step: "tufIntro", readyBack: "element" }, "back", false, true).step).toBe(
      "elementStart",
    );
  });
  it("drops the duplicate effect remover from More mods once Element is installed", () => {
    expect(moreModsFor(false)).toContain("effects");
    expect(moreModsFor(true)).toEqual(["countdown", "calibration", "youtube"]);
  });
});
