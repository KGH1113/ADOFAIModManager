import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useIsPresent } from "motion/react";
import { Fragment, useCallback, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { useApi } from "@/api/app-api-provider";
import { useManager } from "@/app/manager-provider";
import alphaImage from "@/assets/onboarding/steam-alpha.png";
import propertiesImage from "@/assets/onboarding/steam-properties.png";
import { PreviewVideo } from "@/components/onboarding/preview-video";
import {
  captureUmmShortcut,
  defaultUmmShortcut,
  shortcutLabels,
} from "@/models/onboarding/umm-shortcut";
import { Button } from "@/shared/ui/button";
import { Icon } from "@/shared/ui/icon";
import { runOperation } from "@/state/operations/operations-store";

export function AlphaGuide({ next }: { next(): void }) {
  const { t } = useTranslation();
  const { game, locked } = useManager();
  return (
    <div className="alpha-guide">
      <div className="guide-heading">
        <span className="guide-eyebrow">{t("onboarding.alpha.eyebrow")}</span>
        <h1 id="onboarding-title" tabIndex={-1}>
          {t("onboarding.alpha.title")}
        </h1>
        <p>{t("onboarding.alpha.description")}</p>
      </div>
      <div className="alpha-guide-steps">
        <figure className="alpha-properties-step">
          <figcaption>
            <span className="guide-number">01</span>
            <div>
              <h2>{t("onboarding.alpha.properties.title")}</h2>
              <p>{t("onboarding.alpha.properties.description")}</p>
            </div>
          </figcaption>
          <img
            src={propertiesImage}
            alt={t("onboarding.alpha.properties.alt")}
            width={388}
            height={206}
          />
          <p className="alpha-access-hint">
            {t("onboarding.alpha.access")} <code>thisisnotrhythmdoctor</code>
          </p>
        </figure>
        <figure className="alpha-version-step">
          <figcaption>
            <span className="guide-number">02</span>
            <div>
              <h2>{t("onboarding.alpha.betas.title")}</h2>
              <p>{t("onboarding.alpha.betas.description")}</p>
            </div>
          </figcaption>
          <img src={alphaImage} alt={t("onboarding.alpha.betas.alt")} width={854} height={608} />
        </figure>
      </div>
      {!game.data?.detected && (
        <div className="alpha-game-location">
          <p>{t("onboarding.game.chooseHint")}</p>
          <Button size="small" disabled={locked} onClick={() => void game.chooseGame()}>
            <Icon name="folder" size={16} />
            {t("onboarding.game.choose")}
          </Button>
          <Button
            variant="ghost"
            size="icon"
            disabled={locked || game.isFetching}
            aria-label={t("common.refresh")}
            onClick={() => void game.refresh()}
          >
            <Icon name="refresh" className={game.isFetching ? "animate-spin" : ""} />
          </Button>
        </div>
      )}
      <div className="alpha-guide-footer">
        <Button variant="ghost" disabled={locked} onClick={next}>
          {t("onboarding.skip")}
        </Button>
        <Button
          variant="primary"
          disabled={locked || !game.data?.detected || !game.data.alphaInstalled || game.isError}
          onClick={next}
        >
          {t(game.data?.alphaInstalled ? "onboarding.alpha.continue" : "onboarding.alpha.waiting")}
          <Icon name="right" />
        </Button>
      </div>
    </div>
  );
}

export function ModSettingsGuide({ next }: { next(): void }) {
  const { t } = useTranslation();
  const { game, locked } = useManager();
  const api = useApi();
  const cache = useQueryClient();
  const present = useIsPresent();
  const path = game.path;
  const query = useQuery({
    queryKey: ["ummShortcut", path],
    queryFn: () => api.mods.loadUmmShortcut(path as string),
    enabled: !!path,
    retry: false,
  });
  const [listening, setListening] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const disabled = locked || !!game.data?.gameRunning || !query.data;
  const labels = shortcutLabels(query.data ?? defaultUmmShortcut);
  useEffect(() => {
    if (!listening || disabled || !present || !path) return;
    const cancel = () => setListening(false);
    const capture = (event: KeyboardEvent) => {
      event.preventDefault();
      event.stopPropagation();
      if (event.repeat) return;
      if (event.code === "Escape") {
        cancel();
        return;
      }
      if (/^(Control|Shift|Alt|Meta)(Left|Right)$/.test(event.code)) return;
      const shortcut = captureUmmShortcut(event);
      if (!shortcut) {
        setMessage("onboarding.shortcut.unsupported");
        return;
      }
      cancel();
      setMessage(null);
      void runOperation("onboarding.shortcut.saving", async () => {
        try {
          const saved = await api.mods.saveUmmShortcut(path, shortcut);
          cache.setQueryData(["ummShortcut", path], saved);
          setMessage("onboarding.shortcut.saved");
        } catch {
          setMessage("onboarding.shortcut.failed");
          throw new Error("onboarding.shortcut.failed");
        }
      });
    };
    window.addEventListener("keydown", capture, true);
    window.addEventListener("blur", cancel);
    return () => {
      window.removeEventListener("keydown", capture, true);
      window.removeEventListener("blur", cancel);
    };
  }, [listening, disabled, present, path, api, cache]);
  const [pressed, setPressed] = useState(0);
  const syncShortcut = useCallback((time: number) => {
    // Source video is 60fps. The preview starts at 144F and UMM opens at 177F:
    // lead with Ctrl at 160F, press F10 with the menu, then hold for 24 frames.
    setPressed(time < 160 / 60 || time >= 201 / 60 ? 0 : time < 177 / 60 ? 1 : 2);
  }, []);
  return (
    <div className="mod-settings-guide">
      <div className="mod-settings-demonstration">
        <div className="recommendation-media">
          <PreviewVideo
            kind="modSettings"
            name={t("onboarding.modSettings.video")}
            startTime={144 / 60}
            onMediaTime={syncShortcut}
            fallback={
              <div className="mod-menu-shortcut">
                <p>{t("onboarding.modSettings.open")}</p>
              </div>
            }
          />
        </div>
        <div className="shortcut-editor">
          <button
            type="button"
            className="shortcut-preview"
            data-listening={listening && !disabled}
            aria-label={t("onboarding.shortcut.change", { shortcut: labels.join(" + ") })}
            aria-describedby="shortcut-help"
            aria-pressed={listening && !disabled}
            disabled={disabled}
            onBlur={() => setListening(false)}
            onClick={() => {
              setListening((value) => !value);
              setMessage(null);
            }}
          >
            {labels.map((label, index) => (
              <Fragment key={label}>
                {index > 0 && <span aria-hidden="true">+</span>}
                <kbd
                  data-pressed={
                    !listening && (index < labels.length - 1 ? pressed >= 1 : pressed === 2)
                  }
                >
                  {label}
                </kbd>
              </Fragment>
            ))}
          </button>
          <p id="shortcut-help" className="shortcut-help" role="status">
            {t(
              game.data?.gameRunning
                ? "onboarding.umm.quitGame"
                : listening && !disabled
                  ? (message ?? "onboarding.shortcut.listening")
                  : query.isError
                    ? "onboarding.shortcut.loadFailed"
                    : query.isPending
                      ? "onboarding.shortcut.loading"
                      : (message ?? "onboarding.shortcut.hint"),
            )}
          </p>
          {query.isError && (
            <Button variant="ghost" size="small" onClick={() => void query.refetch()}>
              {t("onboarding.keys.retry")}
            </Button>
          )}
        </div>
      </div>
      <div className="mod-settings-copy">
        <div className="guide-heading">
          <span className="guide-eyebrow">{t("onboarding.modSettings.eyebrow")}</span>
          <h1 id="onboarding-title" tabIndex={-1}>
            {t("onboarding.modSettings.title")}
          </h1>
          <p>{t("onboarding.modSettings.description", { shortcut: labels.join(" + ") })}</p>
        </div>
        <p className="mod-settings-hint">{t("onboarding.modSettings.configure")}</p>
        <Button variant="primary" disabled={locked} onClick={next}>
          {t("onboarding.modSettings.understand")}
          <Icon name="right" />
        </Button>
      </div>
    </div>
  );
}
