import { cleanup } from "@testing-library/react";
import { afterEach, vi } from "vitest";

afterEach(cleanup);
// Radix Select uses browser pointer capture and scrolling APIs absent in jsdom.
for (const [name, implementation] of Object.entries({
  hasPointerCapture: (): boolean => false,
  setPointerCapture: (): void => {},
  releasePointerCapture: (): void => {},
  scrollIntoView: (): void => {},
})) {
  if (!(name in HTMLElement.prototype)) {
    Object.defineProperty(HTMLElement.prototype, name, {
      configurable: true,
      value: implementation,
    });
  }
}
vi.spyOn(document, "hasFocus").mockReturnValue(true);
// Node 26 exposes a disabled native localStorage; install a per-test DOM store.
const values = new Map<string, string>();
const storage: Storage = {
  get length() {
    return values.size;
  },
  clear: () => values.clear(),
  getItem: (key) => values.get(key) ?? null,
  setItem: (key, value) => {
    values.set(key, String(value));
  },
  removeItem: (key) => {
    values.delete(key);
  },
  key: (index) => [...values.keys()][index] ?? null,
};
Object.defineProperty(window, "localStorage", { configurable: true, value: storage });
Object.defineProperty(globalThis, "localStorage", { configurable: true, value: storage });
Object.defineProperty(window, "matchMedia", {
  writable: true,
  value: () => ({
    matches: false,
    addEventListener() {},
    removeEventListener() {},
    addListener() {},
    removeListener() {},
  }),
});
