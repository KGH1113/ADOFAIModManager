import { describe, expect, it } from "vitest";
import { keyViewerStyles } from "./key-bindings";
import { keyViewerLayouts } from "./key-viewer-layout";

describe("JRP 1.5.2.0 key viewer geometry", () => {
  it("matches the native key order and wide-key placement in Key12", () => {
    expect(keyViewerLayouts[12].keys.slice(8)).toEqual([
      { slot: 9, x: 81, y: 54, width: 50 },
      { slot: 8, x: 135, y: 54, width: 77 },
      { slot: 10, x: 216, y: 54, width: 77 },
      { slot: 11, x: 297, y: 54, width: 50 },
    ]);
  });
  it("places all Key16 secondary keys in one row below their paired main keys", () => {
    const second = keyViewerLayouts[16].keys.slice(8);
    expect(second.map((key) => key.slot)).toEqual([12, 13, 9, 8, 10, 11, 14, 15]);
    expect(second.map((key) => key.x)).toEqual([0, 54, 108, 162, 216, 270, 324, 378]);
    expect(second.every((key) => key.y === 54 && key.width === 50)).toBe(true);
  });
  it("places Key4 and Key8 in a single row", () => {
    expect(keyViewerLayouts[4].width).toBe(keyViewerLayouts[8].width);
    expect(keyViewerLayouts[4].keys).toEqual(
      keyViewerLayouts[8].keys.slice(2, 6).map((key, slot) => ({ ...key, slot })),
    );
    for (const style of [4, 8] as const) {
      expect(keyViewerLayouts[style].height).toBe(50);
      expect(keyViewerLayouts[style].keys.every((key) => key.y === 0 && key.width === 50)).toBe(
        true,
      );
    }
  });
  it.each(keyViewerStyles)(
    "contains each Key%i slot once with no overlaps or statistics boxes",
    (style) => {
      const layout = keyViewerLayouts[style];
      expect(layout.keys.map((key) => key.slot).sort((a, b) => a - b)).toEqual(
        Array.from({ length: style }, (_, i) => i),
      );
      for (const key of layout.keys) {
        expect(key.x).toBeGreaterThanOrEqual(0);
        expect(key.x + key.width).toBeLessThanOrEqual(layout.width);
        expect(key.y + 50).toBeLessThanOrEqual(layout.height);
        for (const other of layout.keys.filter((other) => other !== key)) {
          expect(
            key.x + key.width <= other.x ||
              other.x + other.width <= key.x ||
              key.y + 50 <= other.y ||
              other.y + 50 <= key.y,
          ).toBe(true);
        }
      }
    },
  );
});
