// Facts about Element's first launch that the onboarding shows. They mirror Element's
// defaults, so update them together with Element:
// - ElementNote tabs: apps/element-note/src-tauri/initial_key_viewers.json (16K selected).
// - Key limiter: on by default, so ADOFAI only accepts the selected tab's keys.
// - Settings shortcut: Left Control + F9 on every platform.

export const elementChapters = ["play", "keys", "effects", "chatter"] as const;
export type ElementChapter = (typeof elementChapters)[number];

export const elementMoments = ["permissions", "keys", "settings"] as const;
export type ElementMoment = (typeof elementMoments)[number];

export const elementNoteTabs = [16, 12, 10, 8, 4] as const;
export type ElementNoteTab = (typeof elementNoteTabs)[number];
export const defaultElementNoteTab: ElementNoteTab = 16;

// KeyboardEvent.code for each key, in ElementNote's slot order. ElementNote stores the
// right Alt and right Ctrl as VK 21/25, which Korean keyboards send as 한/영 and 한자.
const sixteen = [
  "Tab",
  "Digit1",
  "Digit2",
  "KeyE",
  "KeyP",
  "Equal",
  "Backspace",
  "Backslash",
  "CapsLock",
  "ControlLeft",
  "KeyC",
  "Space",
  "AltRight",
  "Period",
  "ControlRight",
  "Enter",
] as const;
export type ElementNoteKey = (typeof sixteen)[number];
export const elementNoteKeys: Record<ElementNoteTab, readonly ElementNoteKey[]> = {
  16: sixteen,
  12: [...sixteen.slice(0, 8), "KeyC", "Space", "AltRight", "Period"],
  10: [...sixteen.slice(0, 8), "Space", "AltRight"],
  8: sixteen.slice(0, 8),
  4: ["Digit2", "KeyE", "KeyP", "Equal"],
};

const aliases: Partial<Record<string, ElementNoteKey>> = {
  Lang1: "AltRight",
  HangulMode: "AltRight",
  Lang2: "ControlRight",
  HanjaMode: "ControlRight",
  NumpadEnter: "Enter",
};
/** The physical key the game sees for a browser key code, so 한/영 counts as right Alt. */
export const normalizeKeyCode = (code: string) => aliases[code] ?? code;
export function acceptsKey(tab: ElementNoteTab, code: string) {
  return (elementNoteKeys[tab] as readonly string[]).includes(normalizeKeyCode(code));
}

export type Platform = "macos" | "windows" | "linux";
export const toPlatform = (value?: string | null): Platform =>
  value === "windows" ? "windows" : value === "linux" ? "linux" : "macos";
/** Element ships macOS and Windows packages only. */
export const elementSupported = (platform: Platform) => platform !== "linux";

export interface KeyboardKey {
  code: string;
  label: string;
  width?: number;
}
const letters = (codes: string) =>
  [...codes].map((letter) => ({ code: `Key${letter}`, label: letter }));
const key = (code: string, label: string, width?: number): KeyboardKey => ({
  code,
  label,
  width,
});
/** A 15-unit ANSI main block. Modifier labels follow the player's platform. */
export function keyboardRows(platform: Platform): KeyboardKey[][] {
  const mac = platform === "macos";
  const bottom = mac
    ? [
        key("ControlLeft", "control", 1.5),
        key("AltLeft", "option", 1.25),
        key("MetaLeft", "command", 1.5),
        key("Space", "", 6),
        key("MetaRight", "command", 1.5),
        key("AltRight", "option", 1.25),
        key("ControlRight", "control", 2),
      ]
    : [
        key("ControlLeft", "Ctrl", 1.25),
        key("MetaLeft", "Win", 1.25),
        key("AltLeft", "Alt", 1.25),
        key("Space", "", 6.25),
        key("AltRight", "Alt", 1.25),
        key("MetaRight", "Win", 1.25),
        key("ContextMenu", "Menu", 1.25),
        key("ControlRight", "Ctrl", 1.25),
      ];
  return [
    [
      key("Backquote", "`"),
      ...[1, 2, 3, 4, 5, 6, 7, 8, 9, 0].map((n) => key(`Digit${n}`, String(n))),
      key("Minus", "-"),
      key("Equal", "="),
      key("Backspace", mac ? "delete" : "Backspace", 2),
    ],
    [
      key("Tab", mac ? "tab" : "Tab", 1.5),
      ...letters("QWERTYUIOP"),
      key("BracketLeft", "["),
      key("BracketRight", "]"),
      key("Backslash", "\\", 1.5),
    ],
    [
      key("CapsLock", mac ? "caps lock" : "Caps Lock", 1.75),
      ...letters("ASDFGHJKL"),
      key("Semicolon", ";"),
      key("Quote", "'"),
      key("Enter", mac ? "return" : "Enter", 2.25),
    ],
    [
      key("ShiftLeft", mac ? "shift" : "Shift", 2.25),
      ...letters("ZXCVBNM"),
      key("Comma", ","),
      key("Period", "."),
      key("Slash", "/"),
      key("ShiftRight", mac ? "shift" : "Shift", 2.75),
    ],
    bottom,
  ];
}

/** A short label for status text, e.g. "F", "Space", "Right Alt". */
export function keyName(code: string, platform: Platform) {
  const normalized = normalizeKeyCode(code);
  if (normalized === "Space") return "Space";
  if (normalized.startsWith("Key")) return normalized.slice(3);
  if (normalized.startsWith("Digit")) return normalized.slice(5);
  const found = keyboardRows(platform)
    .flat()
    .find((candidate) => candidate.code === normalized);
  const side = normalized.endsWith("Left") ? "L " : normalized.endsWith("Right") ? "R " : "";
  if (!found?.label) return normalized.replace(/^(Arrow|Numpad)/, "");
  return /^(Shift|Ctrl|Alt|Win|shift|control|option|command)$/.test(found.label)
    ? `${side}${found.label}`
    : found.label;
}
