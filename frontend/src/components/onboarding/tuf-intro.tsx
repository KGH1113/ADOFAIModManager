import { AnimatePresence, motion, useReducedMotion } from "motion/react";
import { type KeyboardEvent, useState } from "react";
import { useTranslation } from "react-i18next";
import { useApi } from "@/api/app-api-provider";
import homeImage from "@/assets/onboarding/tuf-home.webp";
import leaderboardImage from "@/assets/onboarding/tuf-leaderboard.webp";
import levelsImage from "@/assets/onboarding/tuf-levels.webp";
import { TufDifficulty } from "@/components/onboarding/tuf-difficulty";
import { Button } from "@/shared/ui/button";
import { Icon } from "@/shared/ui/icon";
import { runOperation } from "@/state/operations/operations-store";
import "./tuf-intro.css";

const slides = ["home", "levels", "difficulty", "scores"] as const;
const copy = "onboarding.tufIntro";

export function TufIntro({ next, disabled = false }: { next(): void; disabled?: boolean }) {
  const { t } = useTranslation();
  const api = useApi();
  const reduced = useReducedMotion();
  const [{ index, direction }, setSlide] = useState({ index: 0, direction: 1 });
  const slide = slides[index];
  const last = index === slides.length - 1;
  const go = (target: number) => {
    if (disabled || target < 0 || target >= slides.length || target === index) return;
    setSlide({ index: target, direction: target > index ? 1 : -1 });
  };
  const open = (url: string) => {
    void runOperation(`${copy}.openWebsite`, () => api.desktop.openUrl(url));
  };
  const onKeyDown = (event: KeyboardEvent<HTMLElement>) => {
    if (
      event.defaultPrevented ||
      event.altKey ||
      event.ctrlKey ||
      event.metaKey ||
      (event.target instanceof HTMLElement && event.target.closest("input, select, [role=tablist]"))
    )
      return;
    if (event.key === "ArrowLeft" || event.key === "ArrowRight") {
      event.preventDefault();
      const target = index + (event.key === "ArrowRight" ? 1 : -1);
      if (disabled || target < 0 || target >= slides.length) return;
      // Keep keyboard focus on a control that survives the outgoing slide.
      event.currentTarget
        .querySelector<HTMLHeadingElement>("#onboarding-title")
        ?.focus({ preventScroll: true });
      go(target);
    }
  };

  return (
    <section className="tuf-intro" aria-labelledby="onboarding-title" onKeyDown={onKeyDown}>
      <header className="tuf-intro-heading">
        <div>
          <span className="tuf-intro-wordmark">The Universal Forums</span>
          <h1 id="onboarding-title" tabIndex={-1}>
            {t(`${copy}.title`)}
          </h1>
        </div>
        <span className="tuf-slide-count" aria-hidden="true">
          <strong>{String(index + 1).padStart(2, "0")}</strong>
          <span>/ 04</span>
        </span>
      </header>

      <section
        className="tuf-presentation"
        aria-roledescription="carousel"
        aria-label={t(`${copy}.title`)}
      >
        <button
          type="button"
          className="tuf-chevron"
          aria-label={t(`${copy}.previous`)}
          disabled={disabled || index === 0}
          onClick={() => go(index - 1)}
        >
          <Icon name="left" size={28} />
          <span aria-hidden="true">{t(`${copy}.previousAction`)}</span>
        </button>
        <div className="tuf-slide-viewport">
          <AnimatePresence initial={false} mode="wait" custom={direction}>
            <motion.div
              key={slide}
              className={`tuf-slide tuf-slide-${slide}`}
              role="group"
              aria-roledescription="slide"
              aria-label={t(`${copy}.slide`, {
                current: index + 1,
                total: slides.length,
                title: t(`${copy}.labels.${slide}`),
              })}
              custom={direction}
              variants={{
                enter: (way: number) => ({ opacity: 0, x: reduced ? 0 : 24 * way }),
                center: { opacity: 1, x: 0 },
                exit: (way: number) => ({ opacity: 0, x: reduced ? 0 : -16 * way }),
              }}
              initial="enter"
              animate="center"
              exit="exit"
              transition={{ duration: reduced ? 0 : 0.18, ease: "easeOut" }}
            >
              {slide === "home" && (
                <>
                  <figure className="tuf-home-image">
                    <img src={homeImage} alt={t(`${copy}.home.alt`)} draggable={false} />
                  </figure>
                  <div className="tuf-home-copy">
                    <span className="tuf-slide-eyebrow">01 / {t(`${copy}.labels.home`)}</span>
                    <h2>{t(`${copy}.home.title`)}</h2>
                    <p>{t(`${copy}.home.description`)}</p>
                    <Button
                      className="tuf-website-link"
                      disabled={disabled}
                      onClick={() => open("https://tuforums.com")}
                    >
                      {t(`${copy}.openWebsite`)}
                      <Icon name="external" size={18} />
                    </Button>
                  </div>
                </>
              )}
              {slide === "levels" && (
                <>
                  <div className="tuf-levels-heading">
                    <div>
                      <span className="tuf-slide-eyebrow">02 / {t(`${copy}.labels.levels`)}</span>
                      <h2>{t(`${copy}.levels.title`)}</h2>
                    </div>
                    <p>{t(`${copy}.levels.description`)}</p>
                  </div>
                  <figure className="tuf-levels-image">
                    <img src={levelsImage} alt={t(`${copy}.levels.alt`)} draggable={false} />
                  </figure>
                </>
              )}
              {slide === "difficulty" && <TufDifficulty disabled={disabled} />}
              {slide === "scores" && (
                <>
                  <div className="tuf-score-heading">
                    <span className="tuf-slide-eyebrow">04 / {t(`${copy}.labels.scores`)}</span>
                    <h2>{t(`${copy}.scores.title`)}</h2>
                    <p>{t(`${copy}.scores.description`)}</p>
                  </div>
                  <div className="tuf-score-body">
                    <dl className="tuf-score-explainer">
                      {(["clear", "best", "ranked"] as const).map((item) => (
                        <div key={item}>
                          <dt>{t(`${copy}.scores.${item}.title`)}</dt>
                          <dd>{t(`${copy}.scores.${item}.description`)}</dd>
                        </div>
                      ))}
                    </dl>
                    <figure className="tuf-score-image">
                      <img src={leaderboardImage} alt={t(`${copy}.scores.alt`)} draggable={false} />
                      <figcaption>{t(`${copy}.scores.caption`)}</figcaption>
                    </figure>
                  </div>
                </>
              )}
            </motion.div>
          </AnimatePresence>
        </div>
        <button
          type="button"
          className="tuf-chevron tuf-chevron-next"
          aria-label={t(`${copy}.${last ? "finish" : "next"}`)}
          title={last ? t(`${copy}.finish`) : undefined}
          disabled={disabled}
          onClick={() => (last ? next() : go(index + 1))}
        >
          <Icon name="right" size={28} />
          <span aria-hidden="true">{t(`${copy}.${last ? "finishAction" : "nextAction"}`)}</span>
        </button>
      </section>

      <span className="sr-only" aria-live="polite" aria-atomic="true">
        {t(`${copy}.slide`, {
          current: index + 1,
          total: slides.length,
          title: t(`${copy}.labels.${slide}`),
        })}
      </span>
    </section>
  );
}
