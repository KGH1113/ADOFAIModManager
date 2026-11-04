import { useReducedMotion } from "motion/react";
import { useCallback, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import type { Platform } from "@/models/onboarding/element-guide";

const copy = "onboarding.elementStart.settings";
const cards = [
  { title: "Overlay", body: `${copy}.overlay`, focus: true },
  { title: "Tweaks", body: `${copy}.tweaks` },
  { title: "Effects", body: `${copy}.effects` },
  { title: `${copy}.general`, body: `${copy}.generalDescription`, translated: true },
] as const;

/**
 * Left Ctrl + F9 and the settings home it opens. The keys press once by themselves;
 * pressing the real shortcut (or the keycaps) toggles the drawing, like in the game.
 */
export function SettingsVisual({ platform, shortcut }: { platform: Platform; shortcut: string }) {
  const { t } = useTranslation();
  const reduced = useReducedMotion();
  const [open, setOpen] = useState(!!reduced);
  const [pressed, setPressed] = useState(false);
  const toggle = useCallback(() => {
    setPressed(true);
    setOpen((value) => !value);
  }, []);
  useEffect(() => {
    if (reduced) return;
    const timer = window.setTimeout(toggle, 700);
    return () => window.clearTimeout(timer);
  }, [reduced, toggle]);
  useEffect(() => {
    if (!pressed) return;
    const timer = window.setTimeout(() => setPressed(false), 260);
    return () => window.clearTimeout(timer);
  }, [pressed]);
  useEffect(() => {
    // Element only answers the left Control, so track that key instead of event.ctrlKey.
    let leftControl = false;
    const down = (event: globalThis.KeyboardEvent) => {
      if (event.code === "ControlLeft") leftControl = true;
      if (event.code !== "F9" || event.repeat || !leftControl) return;
      event.preventDefault();
      toggle();
    };
    const up = (event: globalThis.KeyboardEvent) => {
      if (event.code === "ControlLeft") leftControl = false;
    };
    const reset = () => {
      leftControl = false;
    };
    window.addEventListener("keydown", down);
    window.addEventListener("keyup", up);
    window.addEventListener("blur", reset);
    return () => {
      window.removeEventListener("keydown", down);
      window.removeEventListener("keyup", up);
      window.removeEventListener("blur", reset);
    };
  }, [toggle]);
  const control = platform === "macos" ? "control" : "Ctrl";

  return (
    <div className="settings-visual">
      <button
        type="button"
        className="settings-keys"
        aria-label={shortcut}
        aria-pressed={open}
        title={t(`${copy}.try`)}
        onClick={toggle}
      >
        <kbd className="settings-key" data-pressed={pressed}>
          {t(`${copy}.left`, { key: control })}
        </kbd>
        <span className="settings-plus" aria-hidden="true">
          +
        </span>
        <kbd className="settings-key" data-pressed={pressed}>
          F9
        </kbd>
      </button>
      {platform === "macos" && <p className="settings-fn">{t(`${copy}.fn`)}</p>}
      <div className="settings-window" data-open={open} aria-hidden="true">
        <header>{t(`${copy}.window`)}</header>
        <div className="settings-cards">
          {cards.map((card) => (
            <div key={card.title} className="settings-card" data-focus={"focus" in card}>
              <strong>{"translated" in card ? t(card.title) : card.title}</strong>
              <span className="settings-card-text">{t(card.body)}</span>
            </div>
          ))}
        </div>
      </div>
      <div className="settings-path">
        <span>{t(`${copy}.keyViewer`)}</span>
        <ol className="element-path">
          <li>Overlay</li>
          <li>{t(`${copy}.addElement`)}</li>
          <li>{t(`${copy}.keyViewerItem`)}</li>
        </ol>
      </div>
    </div>
  );
}
