import { AnimatePresence, motion, useReducedMotion } from "motion/react";
import { type KeyboardEvent, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { useManager } from "@/app/manager-provider";
import { KeyTester } from "@/components/onboarding/element/key-tester";
import { PermissionVisual } from "@/components/onboarding/element/permission-visual";
import { SettingsVisual } from "@/components/onboarding/element/settings-visual";
import { type ElementMoment, elementMoments, toPlatform } from "@/models/onboarding/element-guide";
import { Button } from "@/shared/ui/button";
import { Icon } from "@/shared/ui/icon";
import "@/components/onboarding/element/element.css";

const copy = "onboarding.elementStart";

/** What happens the first time the game starts with Element, one moment at a time. */
export function ElementStartStep({ next }: { next(): void }) {
  const { t } = useTranslation();
  const { game, locked } = useManager();
  const reduced = useReducedMotion();
  const platform = toPlatform(game.runtime?.platform);
  const [moment, setMoment] = useState<ElementMoment>("permissions");
  const index = elementMoments.indexOf(moment);
  const last = index === elementMoments.length - 1;
  const tabs = useRef<(HTMLButtonElement | null)[]>([]);
  const shortcut = `${t(`${copy}.settings.left`, {
    key: platform === "macos" ? "control" : "Ctrl",
  })} + F9`;
  const onTabKey = (event: KeyboardEvent<HTMLButtonElement>, position: number) => {
    const step = { ArrowDown: 1, ArrowRight: 1, ArrowUp: -1, ArrowLeft: -1 }[event.key];
    if (!step) return;
    event.preventDefault();
    const target = (position + step + elementMoments.length) % elementMoments.length;
    setMoment(elementMoments[target]);
    tabs.current[target]?.focus();
  };

  return (
    <div className="element-start-step">
      <div className="element-sidebar">
        <header className="element-heading">
          <span className="guide-eyebrow">{t(`${copy}.eyebrow`)}</span>
          <h1 id="onboarding-title" tabIndex={-1}>
            {t(`${copy}.title`)}
          </h1>
          <p>{t(`${copy}.description`)}</p>
        </header>
        <div
          className="element-chapters"
          role="tablist"
          aria-orientation="vertical"
          aria-label={t(`${copy}.moments`)}
        >
          {elementMoments.map((name, position) => {
            const selected = moment === name;
            return (
              <button
                key={name}
                ref={(node) => {
                  tabs.current[position] = node;
                }}
                type="button"
                role="tab"
                id={`element-moment-${name}`}
                aria-selected={selected}
                aria-controls="element-moment"
                tabIndex={selected ? 0 : -1}
                className="element-chapter"
                onClick={() => setMoment(name)}
                onKeyDown={(event) => onTabKey(event, position)}
              >
                <span className="element-chapter-icon element-chapter-number">{position + 1}</span>
                <span className="element-chapter-copy">
                  <strong>{t(`${copy}.${name}.title`)}</strong>
                  <span className="element-chapter-detail">
                    <span className="element-chapter-text">
                      {name === "permissions"
                        ? t(`${copy}.permissions.${platform === "windows" ? "windows" : "macos"}`)
                        : t(`${copy}.${name}.description`, { shortcut })}
                    </span>
                  </span>
                </span>
              </button>
            );
          })}
        </div>
        <footer className="recommendation-actions">
          <div>
            <Button
              variant="primary"
              disabled={locked}
              onClick={() => (last ? next() : setMoment(elementMoments[index + 1]))}
            >
              {t(last ? "onboarding.continue" : `${copy}.next`)}
              <Icon name="right" />
            </Button>
          </div>
        </footer>
      </div>
      <section
        id="element-moment"
        className="element-media element-moment"
        role="tabpanel"
        aria-labelledby={`element-moment-${moment}`}
      >
        <AnimatePresence mode="wait" initial={false}>
          <motion.div
            key={moment}
            className="element-moment-surface"
            initial={{ opacity: 0, y: reduced ? 0 : 8 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: reduced ? 0 : -6 }}
            transition={{ duration: reduced ? 0.1 : 0.2, ease: "easeOut" }}
          >
            {moment === "permissions" && <PermissionVisual platform={platform} />}
            {moment === "keys" && <KeyTester platform={platform} />}
            {moment === "settings" && <SettingsVisual platform={platform} shortcut={shortcut} />}
          </motion.div>
        </AnimatePresence>
      </section>
    </div>
  );
}
