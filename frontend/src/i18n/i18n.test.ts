import { describe, expect, it } from "vitest";
import en from "./locales/en/common.json";
import ko from "./locales/ko/common.json";
import zh from "./locales/zh/common.json";

describe("localization", () => {
  it("keeps all supported languages in sync", () => {
    expect(Object.keys(en).sort()).toEqual(Object.keys(ko).sort());
    expect(Object.keys(zh).sort()).toEqual(Object.keys(ko).sort());
  });
  it("uses factual setup copy instead of the removed slogans", () => {
    const text = Object.values(en).join(" ");
    expect(text).not.toMatch(
      /make it your rhythm|whole new rhythm|journey that feels like you|next chapter/i,
    );
  });
});
