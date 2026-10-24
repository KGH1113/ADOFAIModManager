import { describe, expect, it } from "vitest";
import { captureUmmShortcut, shortcutLabels } from "./umm-shortcut";

describe("UMM shortcuts", () => {
  it("captures physical keys with UMM modifier bits", () => {
    const shortcut = captureUmmShortcut(
      new KeyboardEvent("keydown", { code: "KeyK", key: "ㅏ", ctrlKey: true, altKey: true }),
    );
    expect(shortcut).toEqual({ keyCode: "K", modifiers: 5 });
    expect(shortcutLabels(shortcut!)).toEqual(["Ctrl", "Alt", "K"]);
  });
  it("supports single keys without adding a modifier", () => {
    expect(captureUmmShortcut(new KeyboardEvent("keydown", { code: "F8" }))).toEqual({
      keyCode: "F8",
      modifiers: 0,
    });
    expect(shortcutLabels({ keyCode: "Alpha3", modifiers: 0 })).toEqual(["3"]);
  });
  it("rejects escape, modifier-only and unsupported command combinations", () => {
    for (const code of ["Escape", "ControlLeft", "F15", "Unidentified"])
      expect(captureUmmShortcut(new KeyboardEvent("keydown", { code }))).toBeNull();
    expect(
      captureUmmShortcut(new KeyboardEvent("keydown", { code: "KeyK", metaKey: true })),
    ).toBeNull();
  });
});
