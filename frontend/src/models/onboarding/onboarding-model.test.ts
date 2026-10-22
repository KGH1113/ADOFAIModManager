import { describe, expect, it } from "vitest";
import { type OnboardingState, recommendations, transition } from "./onboarding-model";

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
    expect(state.step).toBe("jrp");
    state = transition(state, "next");
    expect(state.step).toBe("keyBindings");
    state = transition(state, "next");
    expect(state.step).toBe("chatter");
    state = transition(state, "next");
    expect(state.step).toBe("tuf");
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
    expect(recommendations.chatter.slug).toBe("keyboardchatterblocker");
    expect(recommendations.jrp.installedId).toBe("JipperResourcePack");
  });
  it("skips key binding setup when JRP was skipped and restores it when installed", () => {
    const jrp: OnboardingState = { step: "jrp", readyBack: "modSettings" };
    expect(transition(jrp, "next", false, false).step).toBe("chatter");
    expect(transition(jrp, "next", false, true).step).toBe("keyBindings");
    expect(transition({ step: "chatter", readyBack: "jrp" }, "back", false, false).step).toBe(
      "jrp",
    );
  });
});
