import { useReducedMotion } from "motion/react";
import { type KeyboardEvent, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { useManager } from "@/app/manager-provider";
import { ElementStage } from "@/components/onboarding/element/element-stage";
import { hasInstalledMod } from "@/models/installation/installation-model";
import {
  type ElementChapter,
  elementChapters,
  elementSupported,
  toPlatform,
} from "@/models/onboarding/element-guide";
import { recommendations } from "@/models/onboarding/onboarding-model";
import { type Flow, InstallButton, Progress } from "@/sections/onboarding/recommendation-step";
import { Button } from "@/shared/ui/button";
import { Icon, type IconName } from "@/shared/ui/icon";

const chapterIcons: Record<ElementChapter, IconName> = {
  play: "chart",
  keys: "keyboard",
  effects: "sparkles",
  chatter: "shield",
};
// How long each chapter stays before the tour moves on. Roughly two loops of its scene.
const chapterDurations: Record<ElementChapter, number> = {
  play: 6500,
  keys: 6000,
  effects: 6200,
  chatter: 5600,
};

export function ElementStep({ flow, next }: { flow: Flow; next(): void }) {
  const { t } = useTranslation();
  const { game, locked } = useManager();
  const reduced = useReducedMotion();
  const supported = elementSupported(toPlatform(game.runtime?.platform));
  const installed = hasInstalledMod(game.data, recommendations.element.installedId);
  // The tour plays once by itself, then waits. Choosing a chapter hands control to the player.
  const [tour, setTour] = useState<{ chapter: ElementChapter; auto: boolean }>({
    chapter: "play",
    auto: true,
  });
  // Reduced motion keeps the chapters still until the player picks one.
  const touring = tour.auto && !reduced;
  const tabs = useRef<(HTMLButtonElement | null)[]>([]);
  const choose = (chapter: ElementChapter) => setTour({ chapter, auto: false });
  const advance = () =>
    setTour(({ chapter }) => {
      const index = elementChapters.indexOf(chapter);
      const last = index === elementChapters.length - 1;
      return { chapter: elementChapters[last ? 0 : index + 1], auto: !last };
    });
  const onTabKey = (event: KeyboardEvent<HTMLButtonElement>, index: number) => {
    const step = { ArrowDown: 1, ArrowRight: 1, ArrowUp: -1, ArrowLeft: -1 }[event.key];
    if (!step) return;
    event.preventDefault();
    const target = (index + step + elementChapters.length) % elementChapters.length;
    choose(elementChapters[target]);
    tabs.current[target]?.focus();
  };

  return (
    <div className="element-step">
      <div className="element-sidebar">
        <header className="element-heading">
          <span className="guide-eyebrow">{t("onboarding.element.eyebrow")}</span>
          <h1 id="onboarding-title" tabIndex={-1}>
            {recommendations.element.name}
          </h1>
          <p>{t("onboarding.element.description")}</p>
        </header>
        <div
          className="element-chapters"
          role="tablist"
          aria-orientation="vertical"
          aria-label={t("onboarding.element.chapters")}
        >
          {elementChapters.map((chapter, index) => {
            const selected = tour.chapter === chapter;
            return (
              <button
                key={chapter}
                ref={(node) => {
                  tabs.current[index] = node;
                }}
                type="button"
                role="tab"
                id={`element-chapter-${chapter}`}
                aria-selected={selected}
                aria-controls="element-stage"
                tabIndex={selected ? 0 : -1}
                className="element-chapter"
                onClick={() => choose(chapter)}
                onKeyDown={(event) => onTabKey(event, index)}
              >
                <span className="element-chapter-icon">
                  <Icon name={chapterIcons[chapter]} size={18} />
                </span>
                <span className="element-chapter-copy">
                  <strong>{t(`onboarding.element.${chapter}.title`)}</strong>
                  <span className="element-chapter-detail">
                    <span className="element-chapter-text">
                      {t(`onboarding.element.${chapter}.description`)}
                    </span>
                  </span>
                </span>
                {selected && touring && (
                  <span
                    key={chapter}
                    className="element-chapter-timer"
                    style={{ animationDuration: `${chapterDurations[chapter]}ms` }}
                    onAnimationEnd={advance}
                    aria-hidden="true"
                  />
                )}
              </button>
            );
          })}
        </div>
        <Progress flow={flow} />
        <footer className="recommendation-actions">
          <div>
            {supported && <InstallButton modKey="element" flow={flow} />}
            <Button variant={installed ? "primary" : "ghost"} disabled={locked} onClick={next}>
              {t(installed ? "onboarding.continue" : "onboarding.skip")}
              <Icon name="right" />
            </Button>
          </div>
        </footer>
        <p className={supported ? "element-install-note" : "inline-warning"}>
          {t(supported ? "onboarding.element.note" : "onboarding.element.unsupported")}
        </p>
      </div>
      <section
        id="element-stage"
        className="element-media"
        role="tabpanel"
        aria-labelledby={`element-chapter-${tour.chapter}`}
      >
        <ElementStage
          chapter={tour.chapter}
          onInteract={() => setTour((state) => ({ ...state, auto: false }))}
        />
      </section>
    </div>
  );
}
