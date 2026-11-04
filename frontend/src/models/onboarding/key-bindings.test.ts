import { describe, expect, it } from "vitest";
import { copyKeyLayouts, defaultKeyLayouts } from "./key-bindings";

describe("JRP key layouts", () => {
  it("copies all five styles without sharing editable arrays", () => {
    const copy = copyKeyLayouts(defaultKeyLayouts);
    copy[12][0] = 113;
    expect(defaultKeyLayouts[12][0]).toBe(9);
    expect(copy[16][0]).toBe(9);
  });
});
