import type { KeyViewerStyle } from "./key-bindings";

export interface KeyPlacement {
  slot: number;
  x: number;
  y: number;
  width: number;
}

interface KeyViewerLayout {
  width: number;
  height: number;
  keys: KeyPlacement[];
}

// JRP's Initialize0/1/3KeyViewer and compact layout coordinates, with Y
// measured from the top and KPS/Total omitted. All keys are 50 units tall.
const topRow = Array.from({ length: 8 }, (_, slot) => ({ slot, x: 54 * slot, y: 0, width: 50 }));
const pairedRow = [12, 13, 9, 8, 10, 11, 14, 15].map((slot, column) => ({
  slot,
  x: 54 * column,
  y: 54,
  width: 50,
}));

export const keyViewerLayouts: Record<KeyViewerStyle, KeyViewerLayout> = {
  4: {
    width: 428,
    height: 50,
    keys: Array.from({ length: 4 }, (_, slot) => ({ slot, x: 108 + 54 * slot, y: 0, width: 50 })),
  },
  8: { width: 428, height: 50, keys: [...topRow] },
  10: {
    width: 428,
    height: 104,
    keys: [
      ...topRow,
      { slot: 8, x: 81, y: 54, width: 131 },
      { slot: 9, x: 216, y: 54, width: 131 },
    ],
  },
  12: {
    width: 428,
    height: 104,
    keys: [
      ...topRow,
      { slot: 9, x: 81, y: 54, width: 50 },
      { slot: 8, x: 135, y: 54, width: 77 },
      { slot: 10, x: 216, y: 54, width: 77 },
      { slot: 11, x: 297, y: 54, width: 50 },
    ],
  },
  16: { width: 428, height: 104, keys: [...topRow, ...pairedRow] },
};
