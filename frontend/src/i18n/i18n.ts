import i18n from "i18next";
import { initReactI18next } from "react-i18next";
import en from "./locales/en/common.json";
import ko from "./locales/ko/common.json";
import zh from "./locales/zh/common.json";
export function resolveLanguage(language: string) {
  const candidate = language === "system" ? navigator.language : language;
  return candidate.startsWith("ko") ? "ko" : candidate.startsWith("zh") ? "zh" : "en";
}
void i18n.use(initReactI18next).init({
  showSupportNotice: false,
  resources: { ko: { translation: ko }, en: { translation: en }, zh: { translation: zh } },
  lng: resolveLanguage("system"),
  fallbackLng: "en",
  keySeparator: false,
  interpolation: { escapeValue: false },
});
export default i18n;
