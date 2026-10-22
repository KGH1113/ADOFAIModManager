import { describe, expect, it } from "vitest";
import {
  copyKeyLayouts,
  defaultKey10,
  defaultKeyLayouts,
  hasDuplicateBinding,
  keyLabel,
  keyLayoutComplete,
  keySlotGuide,
  keyViewerStyles,
  toUnityKey,
} from "./key-bindings";

describe("Unity keyboard bindings", () => {
  it("maps physical letters independently from IME text and distinguishes the number pad", () => {
    expect(toUnityKey("KeyQ")).toBe(113);
    expect(toUnityKey("Digit1")).toBe(49);
    expect(toUnityKey("Numpad1")).toBe(257);
    expect(toUnityKey("NumpadEnter")).toBe(271);
    expect(toUnityKey("Enter")).toBe(13);
    expect(keyLabel(257)).toBe("Num 1");
  });
  it("distinguishes left/right modifiers and platform meta keys", () => {
    expect(toUnityKey("ShiftLeft")).toBe(304);
    expect(toUnityKey("ShiftRight")).toBe(303);
    expect(toUnityKey("MetaLeft", "macos")).toBe(310);
    expect(toUnityKey("MetaLeft", "windows")).toBe(311);
    expect(toUnityKey("F15")).toBe(296);
    expect(toUnityKey("F16")).toBeNull();
    expect(toUnityKey("Unidentified")).toBeNull();
  });
  it("preserves labels for default keys and unknown existing native bindings", () => {
    expect(defaultKey10.map(keyLabel)).toEqual([
      "Tab",
      "1",
      "2",
      "E",
      "P",
      "=",
      "Backspace",
      "\\",
      "Space",
      ",",
    ]);
    expect(keyLabel(4101)).toBe("Key 4101");
  });
  it("maps each native style to its fingers, thumb pairs, and edges", () => {
    expect([0, 1, 2, 3].map((index) => keySlotGuide(4, index).finger)).toEqual([2, 3, 3, 2]);
    expect(keySlotGuide(10, 9)).toMatchObject({ hand: "right", finger: 4, highlight: 4 });
    for (const style of [12, 16] as const) {
      for (const index of [8, 9, 10, 11])
        expect(keySlotGuide(style, index)).toMatchObject({
          hand: index < 10 ? "left" : "right",
          finger: 4,
          highlight: 4,
        });
    }
    for (const style of [16] as const) {
      for (const index of [12, 13, 14, 15])
        expect(keySlotGuide(style, index)).toMatchObject({
          hand: index < 14 ? "left" : "right",
          finger: null,
          highlight: 5,
        });
    }
    expect(() => keySlotGuide(12, 12)).toThrow();
  });
  it("accepts native defaults while rejecting duplicate bindings", () => {
    for (const style of keyViewerStyles) {
      expect(keyLayoutComplete(style, defaultKeyLayouts[style])).toBe(true);
      expect(keyLayoutComplete(style, defaultKeyLayouts[style].slice(1))).toBe(false);
      expect(
        hasDuplicateBinding(style, defaultKeyLayouts[style], 0, defaultKeyLayouts[style][1]),
      ).toBe(true);
      expect(
        hasDuplicateBinding(style, defaultKeyLayouts[style], 0, defaultKeyLayouts[style][0]),
      ).toBe(false);
    }
    expect(keyLayoutComplete(4, [50, 50, 112, 61])).toBe(false);
    expect(keyLayoutComplete(4, [50, 101, 112, 0])).toBe(false);
  });
  it("copies all five styles without sharing editable arrays", () => {
    const copy = copyKeyLayouts(defaultKeyLayouts);
    copy[12][0] = 113;
    expect(defaultKeyLayouts[12][0]).toBe(9);
    expect(copy[16][0]).toBe(9);
  });
});
