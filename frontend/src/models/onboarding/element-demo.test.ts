import { describe, expect, it } from "vitest";
import { demoHud, demoLength, judgmentAt, judgmentKinds } from "./element-demo";

describe("illustrated Element HUD", () => {
  it("starts a clean run at the first hit", () => {
    const hud = demoHud(0);
    expect(hud.progress).toBe(`${(100 / demoLength).toFixed(1)}%`);
    expect(hud.accuracy).toBe("100.00%");
    expect(hud.combo).toBe(1);
    expect(hud.attempt).toBe(1);
  });
  it("counts every hit once and breaks the combo on an Early", () => {
    const early = Array.from({ length: demoLength }, (_, beat) => beat).find(
      (beat) => judgmentAt(beat) === "early",
    );
    if (early === undefined) throw new Error("The demo needs one Early");
    expect(demoHud(early).combo).toBe(0);
    expect(demoHud(early + 1).combo).toBe(1);
    const hud = demoHud(demoLength - 1);
    expect(judgmentKinds.reduce((sum, kind) => sum + hud.counts[kind], 0)).toBe(demoLength);
    expect(Number.parseFloat(hud.accuracy)).toBeLessThan(100);
  });
  it("loops the run as a new attempt", () => {
    const next = demoHud(demoLength);
    expect(next.attempt).toBe(2);
    expect(next.progress).toBe(demoHud(0).progress);
    expect(next.fill).toBeCloseTo(1 / demoLength);
  });
});
