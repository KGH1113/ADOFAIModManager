import { describe, expect, it } from "vitest";
import {
  acceptsKey,
  elementNoteKeys,
  elementNoteTabs,
  elementSupported,
  keyboardRows,
  keyName,
  toPlatform,
} from "./element-guide";

describe("Element first-launch facts", () => {
  it("mirrors ElementNote's default tabs, with 16K selected", () => {
    for (const tab of elementNoteTabs) expect(elementNoteKeys[tab]).toHaveLength(tab);
    expect(elementNoteKeys[4]).toEqual(["Digit2", "KeyE", "KeyP", "Equal"]);
    expect(elementNoteKeys[10]).toEqual([...elementNoteKeys[8], "Space", "AltRight"]);
  });
  it("accepts only the selected tab's keys, counting Korean IME keys as right Alt and Ctrl", () => {
    expect(acceptsKey(16, "Space")).toBe(true);
    expect(acceptsKey(16, "KeyF")).toBe(false);
    expect(acceptsKey(16, "Lang1")).toBe(true);
    expect(acceptsKey(16, "Lang2")).toBe(true);
    expect(acceptsKey(16, "NumpadEnter")).toBe(true);
    expect(acceptsKey(4, "Space")).toBe(false);
    expect(acceptsKey(4, "KeyE")).toBe(true);
  });
  it("draws every play key on a 15-unit keyboard for each platform", () => {
    for (const platform of ["macos", "windows"] as const) {
      const rows = keyboardRows(platform);
      for (const row of rows)
        expect(row.reduce((sum, key) => sum + (key.width ?? 1), 0)).toBeCloseTo(15);
      const codes = new Set(rows.flat().map((key) => key.code));
      for (const code of elementNoteKeys[16]) expect(codes.has(code)).toBe(true);
    }
  });
  it("names keys the way the player's keyboard labels them", () => {
    expect(keyName("KeyF", "windows")).toBe("F");
    expect(keyName("Digit2", "windows")).toBe("2");
    expect(keyName("Lang1", "windows")).toBe("R Alt");
    expect(keyName("AltRight", "macos")).toBe("R option");
    expect(keyName("Backspace", "macos")).toBe("delete");
    expect(keyName("ArrowLeft", "windows")).toBe("Left");
  });
  it("offers Element on macOS and Windows only", () => {
    expect(elementSupported(toPlatform("macos"))).toBe(true);
    expect(elementSupported(toPlatform("windows"))).toBe(true);
    expect(elementSupported(toPlatform("linux"))).toBe(false);
    expect(toPlatform(undefined)).toBe("macos");
  });
});
