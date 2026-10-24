export interface UmmShortcut {
  keyCode: string;
  modifiers: number;
}
export const defaultUmmShortcut: UmmShortcut = { keyCode: "F10", modifiers: 1 };
const keys: Record<string, string> = {
  Enter: "Return",
  Space: "Space",
  Tab: "Tab",
  Backspace: "Backspace",
  Delete: "Delete",
  Insert: "Insert",
  Home: "Home",
  End: "End",
  PageUp: "PageUp",
  PageDown: "PageDown",
  ArrowUp: "UpArrow",
  ArrowDown: "DownArrow",
  ArrowLeft: "LeftArrow",
  ArrowRight: "RightArrow",
  Backquote: "BackQuote",
  Minus: "Minus",
  Equal: "Equals",
  BracketLeft: "LeftBracket",
  BracketRight: "RightBracket",
  Semicolon: "Semicolon",
  Quote: "Quote",
  Backslash: "Backslash",
  Comma: "Comma",
  Period: "Period",
  Slash: "Slash",
};
export function captureUmmShortcut(event: KeyboardEvent): UmmShortcut | null {
  if (event.metaKey) return null;
  const keyCode = /^Key[A-Z]$/.test(event.code)
    ? event.code.slice(3)
    : /^Digit[0-9]$/.test(event.code)
      ? `Alpha${event.code.slice(5)}`
      : /^F([1-9]|1[0-2])$/.test(event.code)
        ? event.code
        : keys[event.code];
  return keyCode
    ? {
        keyCode,
        modifiers:
          Number(event.ctrlKey) | (Number(event.shiftKey) << 1) | (Number(event.altKey) << 2),
      }
    : null;
}
export function shortcutLabels(shortcut: UmmShortcut): string[] {
  const labels: Record<string, string> = {
    Return: "Enter",
    UpArrow: "↑",
    DownArrow: "↓",
    LeftArrow: "←",
    RightArrow: "→",
    BackQuote: "`",
    Minus: "−",
    Equals: "=",
    LeftBracket: "[",
    RightBracket: "]",
    Semicolon: ";",
    Quote: "'",
    Backslash: "\\",
    Comma: ",",
    Period: ".",
    Slash: "/",
  };
  return [
    ...(shortcut.modifiers & 1 ? ["Ctrl"] : []),
    ...(shortcut.modifiers & 2 ? ["Shift"] : []),
    ...(shortcut.modifiers & 4 ? ["Alt"] : []),
    labels[shortcut.keyCode] ?? shortcut.keyCode.replace(/^Alpha/, ""),
  ];
}
