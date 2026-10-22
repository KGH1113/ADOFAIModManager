import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useIsPresent } from "motion/react";
import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { useApi } from "@/api/app-api-provider";
import { useManager } from "@/app/manager-provider";
import {
  copyKeyLayouts,
  hasDuplicateBinding,
  type JrpKeyBindings,
  type KeyViewerStyle,
  keyLabel,
  keyLayoutComplete,
  keySlotGuide,
  keyViewerStyles,
  toUnityKey,
} from "@/models/onboarding/key-bindings";
import { type KeyPlacement, keyViewerLayouts } from "@/models/onboarding/key-viewer-layout";
import { Button } from "@/shared/ui/button";
import { Icon } from "@/shared/ui/icon";
import { runOperation } from "@/state/operations/operations-store";
import { FingerHands } from "./finger-hands";

export function JrpKeysStep({ next }: { next(): void }) {
  const { t } = useTranslation();
  const api = useApi();
  const { game, locked } = useManager();
  const path = game.path;
  const query = useQuery({
    queryKey: ["jrpKeys", path],
    queryFn: () => api.mods.loadJrpKeys(path as string),
    enabled: !!path,
    retry: false,
  });
  return (
    <section className="jrp-keys-step" aria-labelledby="onboarding-title">
      <div className="keys-heading">
        <span className="guide-eyebrow">JipperResourcePack</span>
        <h1 id="onboarding-title" tabIndex={-1}>
          {t("onboarding.keys.title")}
        </h1>
        <p>{t("onboarding.keys.description")}</p>
      </div>
      {query.data && path ? (
        <KeyViewerEditor
          key={`${path}:${query.data.style}:${JSON.stringify(query.data.layouts)}`}
          initial={query.data}
          path={path}
          next={next}
        />
      ) : (
        <div className="keys-unavailable" role="status">
          <Icon name={query.isFetching ? "loading" : "keyboard"} size={24} />
          <p>{t(query.isError ? "onboarding.keys.loadFailed" : "onboarding.keys.loading")}</p>
          {query.isError && (
            <Button disabled={locked} onClick={() => void query.refetch()}>
              {t("onboarding.keys.retry")}
            </Button>
          )}
          <Button variant="ghost" disabled={locked} onClick={next}>
            {t("onboarding.skip")}
          </Button>
        </div>
      )}
    </section>
  );
}

function KeyViewerEditor({
  initial,
  path,
  next,
}: {
  initial: JrpKeyBindings;
  path: string;
  next(): void;
}) {
  const { t } = useTranslation();
  const api = useApi();
  const cache = useQueryClient();
  const { game, locked } = useManager();
  const present = useIsPresent();
  const root = useRef<HTMLDivElement>(null);
  const [layouts, setLayouts] = useState(() => copyKeyLayouts(initial.layouts));
  const [style, setStyle] = useState(initial.style);
  const keys = layouts[style];
  const layout = keyViewerLayouts[style];
  const [active, setActive] = useState<number | null>(null);
  const [hovered, setHovered] = useState<number | null>(null);
  const [message, setMessage] = useState<{ key: string; name?: string } | null>(null);
  const [saveFailed, setSaveFailed] = useState(false);
  const disabled = locked || !!game.data?.gameRunning;
  const activeSlot = present && !disabled ? active : null;
  const hoveredSlot = present && !disabled ? hovered : null;
  const platform = game.runtime?.platform ?? "macos";

  useEffect(() => {
    if (activeSlot === null) return;
    const cancel = () => {
      setActive(null);
      setMessage(null);
    };
    const outside = (event: PointerEvent) => {
      if (
        !root.current?.contains(event.target as Node) ||
        !(event.target as Element).closest("[data-key-slot]")
      )
        cancel();
    };
    const capture = (event: KeyboardEvent) => {
      event.preventDefault();
      event.stopPropagation();
      if (event.repeat) return;
      if (event.code === "Escape") {
        cancel();
        setMessage(null);
        return;
      }
      const key = toUnityKey(event.code, platform);
      if (key === null) {
        setMessage({ key: "onboarding.keys.unsupported" });
        return;
      }
      if (hasDuplicateBinding(style, keys, activeSlot, key)) {
        setMessage({ key: "onboarding.keys.duplicate", name: keyLabel(key) });
        return;
      }
      setLayouts((values) => ({
        ...values,
        [style]: values[style].map((value, index) => (index === activeSlot ? key : value)),
      }));
      setActive(null);
      setMessage({ key: "onboarding.keys.assigned", name: keyLabel(key) });
      setSaveFailed(false);
    };
    window.addEventListener("keydown", capture, true);
    window.addEventListener("blur", cancel);
    document.addEventListener("pointerdown", outside);
    return () => {
      window.removeEventListener("keydown", capture, true);
      window.removeEventListener("blur", cancel);
      document.removeEventListener("pointerdown", outside);
    };
  }, [activeSlot, keys, platform, style]);

  // A mutation/OS focus change ends capture instead of leaving a finger depressed.
  useEffect(() => {
    if (disabled || !present) {
      setActive(null);
      setHovered(null);
    }
  }, [disabled, present]);

  const partName = (index: number) =>
    t(`onboarding.keys.finger.${keySlotGuide(style, index).label}`, { number: index - 15 });
  const finger = activeSlot === null ? null : partName(activeSlot);
  const complete = keyLayoutComplete(style, keys);
  const chooseStyle = (value: KeyViewerStyle) => {
    setStyle(value);
    setActive(null);
    setHovered(null);
    setMessage(null);
    setSaveFailed(false);
  };
  const save = () => {
    setActive(null);
    setSaveFailed(false);
    void runOperation("onboarding.keys.saving", async () => {
      try {
        const result = await api.mods.saveJrpKeys(path, keys, style);
        cache.setQueryData(["jrpKeys", path], result);
        next();
      } catch {
        setSaveFailed(true);
        throw new Error("onboarding.keys.saveFailed");
      }
    });
  };

  const keyBox = ({ slot: index, x, y, width }: KeyPlacement) => {
    const guide = keySlotGuide(style, index);
    const name = keyLabel(keys[index]);
    return (
      <button
        key={index}
        type="button"
        className={`key-viewer-key${activeSlot === index ? " is-listening" : ""}`}
        style={{
          left: `${(x / layout.width) * 100}%`,
          top: `${(y / layout.height) * 100}%`,
          width: `${(width / layout.width) * 100}%`,
          height: `${(50 / layout.height) * 100}%`,
        }}
        data-key-slot={index}
        data-key-kind={guide.finger !== null ? "finger" : guide.hand !== null ? "edge" : "free"}
        aria-pressed={activeSlot === index}
        aria-label={t("onboarding.keys.keyLabel", {
          finger: partName(index),
          name,
        })}
        disabled={disabled}
        onMouseEnter={() => setHovered(index)}
        onMouseLeave={() => setHovered(null)}
        onClick={() => {
          setActive(index);
          setMessage(null);
        }}
      >
        <span className={`key-viewer-key-name${name.length > 7 ? " is-long" : ""}`}>{name}</span>
        <span className="key-viewer-key-state" aria-hidden="true" />
      </button>
    );
  };

  return (
    <div ref={root} className="key-viewer-editor">
      <fieldset className="key-viewer-styles">
        <legend>{t("onboarding.keys.style")}</legend>
        <div>
          {keyViewerStyles.map((value) => (
            <label key={value}>
              <input
                type="radio"
                name="jrp-key-viewer-style"
                value={value}
                checked={style === value}
                disabled={disabled}
                onChange={() => chooseStyle(value)}
              />
              <span>Key{value}</span>
            </label>
          ))}
        </div>
      </fieldset>
      <div className="key-viewer-preview">
        <fieldset
          className="key-viewer-layout"
          aria-label={t("onboarding.keys.layout", { count: style })}
          style={{
            aspectRatio: `${layout.width} / ${layout.height}`,
            width: `min(100%, 720px, ${(layout.width / layout.height) * 100}cqh)`,
          }}
        >
          {layout.keys.map(keyBox)}
        </fieldset>
      </div>
      <FingerHands style={style} activeSlot={activeSlot} hoveredSlot={hoveredSlot} />
      <footer className="key-viewer-actions">
        <div className="key-viewer-editor-options">
          <div className="key-capture-status" role="status" aria-live="polite">
            {(message || activeSlot !== null) && (
              <p>
                {message
                  ? t(message.key, { name: message.name })
                  : t("onboarding.keys.listening", { finger })}
              </p>
            )}
            {activeSlot !== null && (
              <Button
                variant="ghost"
                size="small"
                onClick={() => {
                  setActive(null);
                  setMessage(null);
                }}
              >
                {t("common.cancel")}
                <kbd>Esc</kbd>
              </Button>
            )}
          </div>
          {game.data?.gameRunning && (
            <p className="inline-warning">{t("onboarding.umm.quitGame")}</p>
          )}
          {saveFailed && (
            <p className="inline-warning" role="alert">
              {t("onboarding.keys.saveFailed")}
            </p>
          )}
        </div>
        <div className="key-viewer-action-buttons">
          <Button
            variant="ghost"
            disabled={locked}
            onClick={() => {
              setActive(null);
              next();
            }}
          >
            {t("onboarding.skip")}
          </Button>
          <Button
            variant="primary"
            pending={locked}
            disabled={disabled || !complete}
            onClick={save}
          >
            {t("onboarding.keys.save")}
            <Icon name="right" />
          </Button>
        </div>
      </footer>
    </div>
  );
}
