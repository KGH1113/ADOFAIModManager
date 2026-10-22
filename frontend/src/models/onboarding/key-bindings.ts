export const keyViewerStyles = [4, 8, 10, 12, 16] as const;
export type KeyViewerStyle = (typeof keyViewerStyles)[number];
export type KeyLayouts = Record<KeyViewerStyle, number[]>;

export interface JrpKeyBindings {
  style: KeyViewerStyle;
  layouts: KeyLayouts;
}

export const defaultKey10 = [9, 49, 50, 101, 112, 61, 8, 92, 32, 44];
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

const fingers = [
  "leftLittle",
  "leftRing",
  "leftMiddle",
  "leftIndex",
  "rightIndex",
  "rightMiddle",
  "rightRing",
  "rightLittle",
  "leftThumb",
  "rightThumb",
] as const;

export interface KeySlotGuide {
  hand: "left" | "right" | null;
  finger: number | null;
  highlight: number | null;
  label: (typeof fingers)[number] | "leftEdge" | "rightEdge" | "free";
}

export function keySlotGuide(style: KeyViewerStyle, index: number): KeySlotGuide {
  if (index < 0 || index >= style) throw new Error("Key slot is outside the selected layout.");
  if (style === 4) {
    const nativeIndex = index + 2;
    const finger = index < 2 ? nativeIndex : 7 - nativeIndex;
    return {
      hand: index < 2 ? "left" : "right",
      finger,
      highlight: finger,
      label: fingers[nativeIndex],
    };
  }
  if (index < 8) {
    const finger = index < 4 ? index : 7 - index;
    return { hand: index < 4 ? "left" : "right", finger, highlight: finger, label: fingers[index] };
  }
  if (index < (style === 10 ? 10 : 12)) {
    const left = index < (style === 10 ? 9 : 10);
    return {
      hand: left ? "left" : "right",
      finger: 4,
      highlight: 4,
      label: left ? "leftThumb" : "rightThumb",
    };
  }
  if (index < 16) {
    const left = index < 14;
    return {
      hand: left ? "left" : "right",
      finger: null,
      highlight: 5,
      label: left ? "leftEdge" : "rightEdge",
    };
  }
  return { hand: null, finger: null, highlight: null, label: "free" };
}

export function hasDuplicateBinding(
  style: KeyViewerStyle,
  keys: readonly number[],
  slot: number,
  key: number,
) {
  return keys.slice(0, style).some((value, index) => value === key && index !== slot);
}

export function keyLayoutComplete(style: KeyViewerStyle, keys: readonly number[]) {
  const guidedKeys = keys.slice(0, style);
  return (
    keys.length === style &&
    keys.every((key) => key > 0) &&
    new Set(guidedKeys).size === guidedKeys.length
  );
}

const namedKeys: Record<string, [number, string]> = {
  Backspace: [8, "Backspace"],
  Tab: [9, "Tab"],
  Enter: [13, "Enter"],
  Escape: [27, "Esc"],
  Space: [32, "Space"],
  Quote: [39, "'"],
  Comma: [44, ","],
  Minus: [45, "−"],
  Period: [46, "."],
  Slash: [47, "/"],
  Semicolon: [59, ";"],
  Equal: [61, "="],
  BracketLeft: [91, "["],
  Backslash: [92, "\\"],
  BracketRight: [93, "]"],
  Backquote: [96, "`"],
  NumpadDecimal: [266, "Num ."],
  NumpadDivide: [267, "Num /"],
  NumpadMultiply: [268, "Num ×"],
  NumpadSubtract: [269, "Num −"],
  NumpadAdd: [270, "Num +"],
  NumpadEnter: [271, "Num Enter"],
  NumpadEqual: [272, "Num ="],
  ArrowUp: [273, "↑"],
  ArrowDown: [274, "↓"],
  ArrowRight: [275, "→"],
  ArrowLeft: [276, "←"],
  Insert: [277, "Insert"],
  Delete: [127, "Delete"],
  Home: [278, "Home"],
  End: [279, "End"],
  PageUp: [280, "Page Up"],
  PageDown: [281, "Page Down"],
  NumLock: [300, "Num Lock"],
  CapsLock: [301, "Caps Lock"],
  ScrollLock: [302, "Scroll Lock"],
  ShiftRight: [303, "R Shift"],
  ShiftLeft: [304, "L Shift"],
  ControlRight: [305, "R Ctrl"],
  ControlLeft: [306, "L Ctrl"],
  AltRight: [307, "R Alt"],
  AltLeft: [308, "L Alt"],
  PrintScreen: [316, "Print Screen"],
  Pause: [19, "Pause"],
  ContextMenu: [319, "Menu"],
};

// Physical codes keep letter bindings stable with Korean IME and non-English labels.
export function toUnityKey(code: string, platform = "macos"): number | null {
  if (/^Key[A-Z]$/.test(code)) return code.charCodeAt(3) + 32;
  if (/^Digit[0-9]$/.test(code)) return code.charCodeAt(5);
  if (/^Numpad[0-9]$/.test(code)) return 256 + Number(code.at(-1));
  const functionKey = /^F([1-9]|1[0-5])$/.exec(code);
  if (functionKey) return 281 + Number(functionKey[1]);
  if (code === "MetaLeft") return platform === "macos" ? 310 : 311;
  if (code === "MetaRight") return platform === "macos" ? 309 : 312;
  return namedKeys[code]?.[0] ?? null;
}

export function keyLabel(key: number): string {
  if (key === 0) return "—";
  if (key >= 97 && key <= 122) return String.fromCharCode(key).toUpperCase();
  if (key >= 48 && key <= 57) return String.fromCharCode(key);
  if (key >= 256 && key <= 265) return `Num ${key - 256}`;
  if (key >= 282 && key <= 296) return `F${key - 281}`;
  if (key === 310) return "L ⌘";
  if (key === 309) return "R ⌘";
  if (key === 311) return "L Win";
  if (key === 312) return "R Win";
  return Object.values(namedKeys).find(([value]) => value === key)?.[1] ?? `Key ${key}`;
}
