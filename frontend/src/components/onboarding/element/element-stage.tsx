import { useReducedMotion } from "motion/react";
import { type CSSProperties, useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { usePageActive } from "@/hooks/shared/use-page-active";
import {
  demoBpm,
  demoHud,
  demoKeyPattern,
  judgmentAt,
  judgmentKinds,
} from "@/models/onboarding/element-demo";
import type { ElementChapter } from "@/models/onboarding/element-guide";
import { useBeatClock } from "./use-beat-clock";
import "./element.css";

// Stage units are cqw (1% of the stage width) so the scene scales with the window.
const tileGap = 8.6;
const visibleTiles = { behind: 7, ahead: 8 };
const viewerLabels = [
  "Tab",
  "1",
  "2",
  "E",
  "P",
  "=",
  "Back",
  "\\",
  "Caps",
  "LCtrl",
  "C",
  "Space",
  "RAlt",
  ".",
  "RCtrl",
  "Enter",
];
const effectsTiming = { busy: 2400, clean: 3200 };
const decorTops = [12, 58, 24, 66, 6, 46];

/** An illustrated ADOFAI screen with Element's default HUD. Decorative except the switch. */
export function ElementStage({
  chapter,
  onInteract,
}: {
  chapter: ElementChapter;
  /** Called when the player plays with the scene, so a guided tour can stop moving on. */
  onInteract?: () => void;
}) {
  const { t } = useTranslation();
  const reduced = !!useReducedMotion();
  const active = usePageActive(false);
  const fire = useRef<HTMLSpanElement>(null);
  const ice = useRef<HTMLSpanElement>(null);
  const track = useRef<HTMLDivElement>(null);
  const place = useCallback(
    (beats: number) => {
      const pivot = Math.floor(beats);
      const phase = reduced ? 0.55 : beats - pivot;
      // The free planet swings clockwise over the top from the previous tile to the next.
      const angle = Math.PI * (1 - phase);
      const [still, moving] =
        pivot % 2 === 0 ? [fire.current, ice.current] : [ice.current, fire.current];
      if (!still || !moving || !track.current) return;
      still.style.transform = `translate3d(${pivot * tileGap}cqw, 0, 0)`;
      moving.style.transform = `translate3d(${(pivot + Math.cos(angle)) * tileGap}cqw, ${-Math.sin(angle) * tileGap}cqw, 0)`;
      track.current.style.transform = `translate3d(${-(pivot + phase) * tileGap}cqw, 0, 0)`;
    },
    [reduced],
  );
  const beat = useBeatClock(active && !reduced, demoBpm, place);
  const hud = useMemo(() => demoHud(beat), [beat]);
  const pressed = demoKeyPattern[beat % demoKeyPattern.length];

  const [effects, setEffects] = useState({ chapter, removed: false, auto: true });
  if (effects.chapter !== chapter) setEffects({ chapter, removed: false, auto: true });
  const showEffects = chapter === "effects";
  useEffect(() => {
    if (!showEffects || !effects.auto || reduced || !active) return;
    const timer = window.setTimeout(
      () => setEffects((state) => ({ ...state, removed: !state.removed })),
      effects.removed ? effectsTiming.clean : effectsTiming.busy,
    );
    return () => window.clearTimeout(timer);
  }, [showEffects, effects.auto, effects.removed, reduced, active]);
  const busy = showEffects && !effects.removed;

  const [replay, setReplay] = useState(0);
  const tiles = [];
  for (let index = beat - visibleTiles.behind; index <= beat + visibleTiles.ahead; index++)
    tiles.push(
      <span
        key={index}
        className="element-tile"
        style={{ "--tile": index } as CSSProperties}
        data-passed={index <= beat}
      />,
    );
  // Highlights the HUD parts a beginner needs first. Only one part per name carries a label.
  const spot = (name: "progress" | "accuracy" | "judgment", labelled = true) => ({
    "data-spot": chapter === "play" ? name : undefined,
    "data-tag": chapter === "play" && labelled ? t(`onboarding.element.tag.${name}`) : undefined,
  });

  return (
    <div className="element-stage" data-chapter={chapter} data-busy={busy}>
      <div className="element-world" aria-hidden="true">
        <div className="element-backdrop" />
        {busy && (
          <div className="element-decor">
            {decorTops.map((top, index) => (
              <span
                key={top}
                style={
                  { "--i": index, "--top": `${top}%`, "--hue": 300 + index * 30 } as CSSProperties
                }
              />
            ))}
          </div>
        )}
        <div className="element-track-anchor">
          <div className="element-track" ref={track}>
            {tiles}
            <span
              className="element-orbit"
              style={{ "--tile": beat } as CSSProperties}
              aria-hidden="true"
            />
            <span className="element-planet element-planet-fire" ref={fire} />
            <span className="element-planet element-planet-ice" ref={ice} />
          </div>
        </div>
        {busy && <div className="element-flash" />}
      </div>

      <div className="element-hud" aria-hidden="true">
        <div className="element-progress-bar" {...spot("progress", false)}>
          <span style={{ transform: `scaleX(${hud.fill})` }} />
        </div>
        <dl className="element-hud-column element-hud-left">
          <div {...spot("progress")}>
            <dt>Progress</dt>
            <dd>0%-{hud.progress}</dd>
          </div>
          <div {...spot("accuracy")}>
            <dt>XAccuracy</dt>
            <dd>{hud.accuracy}</dd>
          </div>
          <div>
            <dt>Music Time</dt>
            <dd>{hud.musicTime}</dd>
          </div>
          <div>
            <dt>FPS</dt>
            <dd>144</dd>
          </div>
        </dl>
        <dl className="element-hud-column element-hud-right">
          <div>
            <dt>TBPM</dt>
            <dd>{demoBpm}</dd>
          </div>
          <div>
            <dt>CBPM</dt>
            <dd>{demoBpm}</dd>
          </div>
          <div>
            <dt>KPS</dt>
            <dd>{(demoBpm / 60).toFixed(1)}</dd>
          </div>
        </dl>
        <div className="element-hud-center">
          <span className="element-hud-title">Orbit Practice</span>
          <div className="element-hud-hit" {...spot("judgment")}>
            <strong>{judgmentAt(beat) === "early" ? "Early" : "Perfect"}</strong>
            <b key={beat}>{hud.combo}</b>
          </div>
        </div>
        <div className="element-hud-bottom" {...spot("judgment", false)}>
          <span>Timing Scale - 100%</span>
          <ol>
            {judgmentKinds.map((kind) => (
              <li key={kind} data-kind={kind}>
                {hud.counts[kind]}
              </li>
            ))}
          </ol>
        </div>
        <div className="element-hud-attempt">
          <span>Attempt {hud.attempt}</span>
          <span>Full Attempt {hud.fullAttempt}</span>
        </div>
      </div>

      <div
        className="element-viewer"
        aria-hidden="true"
        data-tag={t("onboarding.element.tag.keys")}
        data-visible={chapter === "keys"}
      >
        <div className="element-viewer-keys">
          {viewerLabels.map((label, index) => (
            <span
              key={index === pressed ? `${label}-${beat}` : label}
              data-down={chapter === "keys" && index === pressed}
            >
              {label}
            </span>
          ))}
        </div>
        <div className="element-viewer-stats">
          <span>KPS {(demoBpm / 60).toFixed(1)}</span>
          <span>Total {beat + 1}</span>
        </div>
      </div>

      <div className="element-chatter" data-visible={chapter === "chatter"}>
        <div className="element-chatter-panel" key={replay}>
          <button
            type="button"
            className="element-chatter-key"
            tabIndex={chapter === "chatter" ? 0 : -1}
            aria-label={t("onboarding.element.chatter.replay")}
            onClick={() => {
              onInteract?.();
              setReplay((value) => value + 1);
            }}
          >
            E
          </button>
          <div className="element-chatter-scope" aria-hidden="true">
            <div className="element-chatter-lane">
              <span>{t("onboarding.element.chatter.keyboard")}</span>
              <div>
                <i data-pulse="press" />
                <i data-pulse="bounce" />
              </div>
            </div>
            <div className="element-chatter-lane">
              <span>{t("onboarding.element.chatter.game")}</span>
              <div>
                <i data-pulse="press" />
                <i data-pulse="blocked">
                  <em>{t("onboarding.element.chatter.ignored")}</em>
                </i>
              </div>
            </div>
            <div className="element-chatter-window">
              <span>{t("onboarding.element.chatter.window")}</span>
            </div>
          </div>
        </div>
      </div>

      <div className="element-effects" data-visible={showEffects}>
        <button
          type="button"
          role="switch"
          aria-checked={effects.removed}
          tabIndex={showEffects ? 0 : -1}
          className="element-effects-switch"
          onClick={() => {
            onInteract?.();
            setEffects((state) => ({ ...state, removed: !state.removed, auto: false }));
          }}
        >
          <span className="element-effects-copy">
            <strong>{t("onboarding.element.effects.switch")}</strong>
            <small>
              {t(
                effects.removed
                  ? "onboarding.element.effects.removed"
                  : "onboarding.element.effects.kept",
              )}
            </small>
          </span>
          <span className="element-effects-track" aria-hidden="true">
            <span />
          </span>
        </button>
      </div>
    </div>
  );
}
