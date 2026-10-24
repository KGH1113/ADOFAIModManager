import { motion, useReducedMotion } from "motion/react";
import { type KeyboardEvent, useId, useState } from "react";
import { useTranslation } from "react-i18next";

const icons = import.meta.glob<string>("../../assets/onboarding/tuf-difficulties/*.png", {
  eager: true,
  query: "?url",
  import: "default",
});
const iconFor = (rating: string) => icons[`../../assets/onboarding/tuf-difficulties/${rating}.png`];
const tiers = ["P", "G", "U"] as const;
const modes = ["main", "quantum", "other"] as const;
const specials = ["P0", "Qq", "Unranked", "Censored", "Impossible"] as const;
type Tier = (typeof tiers)[number];
const copy = "onboarding.tufIntro.difficulty";

export function TufDifficulty({ disabled = false }: { disabled?: boolean }) {
  const { t } = useTranslation();
  const reduced = useReducedMotion();
  const id = useId();
  const [mode, setMode] = useState<(typeof modes)[number]>("main");
  const [tier, setTier] = useState<Tier>("P");
  const [rating, setRating] = useState(10);
  const [quantum, setQuantum] = useState(2);
  const [special, setSpecial] = useState<(typeof specials)[number]>("P0");
  const activeTier = mode === "quantum" && tier === "P" ? "G" : tier;
  const activeRating =
    mode === "other"
      ? special
      : mode === "quantum"
        ? `${activeTier}Q${quantum}`
        : `${tier}${rating}`;
  const availableTiers = mode === "quantum" ? tiers.slice(1) : tiers;
  const estimatedRange = `${activeTier}${quantum * 4 + 1} — ${activeTier}${quantum * 4 + 4}`;
  const switchTab = (event: KeyboardEvent<HTMLButtonElement>, position: number) => {
    if (
      event.key !== "ArrowLeft" &&
      event.key !== "ArrowRight" &&
      event.key !== "Home" &&
      event.key !== "End"
    )
      return;
    event.preventDefault();
    event.stopPropagation();
    const target =
      event.key === "Home"
        ? 0
        : event.key === "End"
          ? modes.length - 1
          : (position + (event.key === "ArrowRight" ? 1 : -1) + modes.length) % modes.length;
    setMode(modes[target]);
    event.currentTarget.parentElement
      ?.querySelectorAll<HTMLButtonElement>("[role=tab]")
      [target]?.focus();
  };

  return (
    <div
      className="tuf-difficulty"
      data-mode={mode}
      data-tier={mode === "other" ? "other" : activeTier}
    >
      <header className="tuf-difficulty-heading">
        <div className="tuf-difficulty-title">
          <h2>{t(`${copy}.title`)}</h2>
          <p>{t(`${copy}.description`)}</p>
        </div>
        <div className="tuf-difficulty-modes" role="tablist" aria-label={t(`${copy}.mode`)}>
          {modes.map((name, position) => (
            <button
              key={name}
              id={`${id}-${name}`}
              type="button"
              role="tab"
              aria-label={t(`${copy}.${name}`)}
              aria-describedby={`${id}-${name}-hint`}
              aria-selected={mode === name}
              aria-controls={`${id}-panel`}
              tabIndex={mode === name ? 0 : -1}
              disabled={disabled}
              onClick={() => setMode(name)}
              onKeyDown={(event) => switchTab(event, position)}
            >
              <span>{t(`${copy}.${name}`)}</span>
              <small id={`${id}-${name}-hint`}>{t(`${copy}.${name}Label`)}</small>
            </button>
          ))}
        </div>
      </header>

      <div
        id={`${id}-panel`}
        role="tabpanel"
        aria-labelledby={`${id}-${mode}`}
        className="tuf-difficulty-panel"
      >
        <div className="tuf-rating-workspace">
          {mode !== "other" && (
            <fieldset className="tuf-tier-options" aria-label={t(`${copy}.tier`)}>
              {availableTiers.map((name) => (
                <button
                  key={name}
                  type="button"
                  aria-pressed={activeTier === name}
                  disabled={disabled}
                  onClick={() => setTier(name)}
                >
                  <span>{name}</span>
                  {t(`${copy}.${name}.name`)}
                </button>
              ))}
            </fieldset>
          )}

          <div className="tuf-difficulty-emblem">
            <motion.img
              key={activeRating}
              src={iconFor(activeRating)}
              alt={activeRating}
              width={800}
              height={800}
              draggable={false}
              initial={reduced ? false : { opacity: 0.5, scale: 0.95 }}
              animate={{ opacity: 1, scale: 1 }}
              transition={{ duration: 0.16 }}
            />
          </div>

          {mode === "other" ? (
            <div className="tuf-special-picker">
              <p className="tuf-picker-hint" id={`${id}-special-hint`}>
                {t(`${copy}.hint`)}
              </p>
              <fieldset
                className="tuf-special-options"
                aria-label={t(`${copy}.special`)}
                aria-describedby={`${id}-special-hint`}
              >
                {specials.map((name) => (
                  <button
                    key={name}
                    type="button"
                    aria-pressed={special === name}
                    disabled={disabled}
                    onClick={() => setSpecial(name)}
                  >
                    <img src={iconFor(name)} width={40} height={40} alt="" />
                    <span>{name}</span>
                  </button>
                ))}
              </fieldset>
            </div>
          ) : (
            <div className="tuf-difficulty-slider">
              <label htmlFor={`${id}-rating`}>
                {t(`${copy}.drag`)}
                <output htmlFor={`${id}-rating`}>{activeRating}</output>
              </label>
              <input
                id={`${id}-rating`}
                type="range"
                min={mode === "quantum" ? 0 : 1}
                max={mode === "quantum" ? 4 : 20}
                step={1}
                value={mode === "quantum" ? quantum : rating}
                aria-label={t(`${copy}.rating`)}
                aria-valuetext={
                  mode === "quantum"
                    ? `${activeRating}: ${activeTier}${quantum * 4 + 1} – ${activeTier}${quantum * 4 + 4}`
                    : activeRating
                }
                disabled={disabled}
                onChange={(event) =>
                  mode === "quantum"
                    ? setQuantum(Number(event.target.value))
                    : setRating(Number(event.target.value))
                }
              />
              <div className="tuf-slider-endpoints" aria-hidden="true">
                <span>{mode === "quantum" ? `${activeTier}Q0` : `${tier}1`}</span>
                <span>{mode === "quantum" ? `${activeTier}Q4` : `${tier}20`}</span>
              </div>
            </div>
          )}
        </div>

        <div className="tuf-rating-context">
          <span className="tuf-difficulty-kicker">
            {t(
              mode === "other"
                ? `${copy}.special`
                : mode === "quantum"
                  ? `${copy}.estimate`
                  : `${copy}.${tier}.name`,
            )}
          </span>
          <div className="tuf-rating-description">
            <h3>
              {mode === "other"
                ? t(`${copy}.${special}.name`)
                : mode === "quantum"
                  ? estimatedRange
                  : `${tier}1 — ${tier}20`}
            </h3>
            <p>
              {t(
                mode === "other"
                  ? `${copy}.${special}.description`
                  : mode === "quantum"
                    ? `${copy}.quantumDescription`
                    : `${copy}.${tier}.description`,
              )}
            </p>
          </div>
          {mode !== "other" && (
            <p className="tuf-difficulty-footnote">
              {t(mode === "quantum" ? `${copy}.quantumHint` : `${copy}.scale`)}
            </p>
          )}
        </div>
      </div>
    </div>
  );
}
