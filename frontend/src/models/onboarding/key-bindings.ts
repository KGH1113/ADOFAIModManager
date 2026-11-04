export const keyViewerStyles = [4, 8, 10, 12, 16] as const;
export type KeyViewerStyle = (typeof keyViewerStyles)[number];
export type KeyLayouts = Record<KeyViewerStyle, number[]>;

export interface JrpKeyBindings {
  style: KeyViewerStyle;
  layouts: KeyLayouts;
}

const defaultKey10 = [9, 49, 50, 101, 112, 61, 8, 92, 32, 44];
const defaultKey12 = [...defaultKey10.slice(0, 8), 32, 99, 44, 46];
const defaultKey16 = [...defaultKey12, 301, 304, 13, 104];
export const defaultKeyLayouts: KeyLayouts = {
  4: [50, 101, 112, 61],
  8: defaultKey10.slice(0, 8),
  10: defaultKey10,
  12: defaultKey12,
  16: defaultKey16,
};

export function copyKeyLayouts(layouts: KeyLayouts): KeyLayouts {
  return {
    4: [...layouts[4]],
    8: [...layouts[8]],
    10: [...layouts[10]],
    12: [...layouts[12]],
    16: [...layouts[16]],
  };
}
