import { useCallback, useState } from "react";
import { useTranslation } from "react-i18next";
import { useManager } from "@/app/manager-provider";
import alphaImage from "@/assets/onboarding/steam-alpha.png";
import propertiesImage from "@/assets/onboarding/steam-properties.png";
import { GameSetupCard } from "@/components/installation/game-setup-card";
import { PreviewVideo } from "@/components/onboarding/preview-video";
import { Button } from "@/shared/ui/button";
import { Icon } from "@/shared/ui/icon";

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
        <GameSetupCard
          compact
          installation={game.data}
          busy={locked}
          loading={game.isFetching}
          choose={() => {
            void game.chooseGame();
          }}
          refresh={() => {
            void game.refresh();
          }}
        />
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
  const { locked } = useManager();
  const [pressed, setPressed] = useState(0);
  const syncShortcut = useCallback((time: number) => {
    // Source video is 60fps: hold Ctrl at 126F, add F10 at 143F,
    // then release the chord after a brief 24-frame hold.
    setPressed(time < 126 / 60 || time >= 167 / 60 ? 0 : time < 143 / 60 ? 1 : 2);
  }, []);
  return (
    <div className="mod-settings-guide">
      <div className="mod-settings-demonstration">
        <div className="recommendation-media">
          <PreviewVideo
            kind="modSettings"
            name={t("onboarding.modSettings.video")}
            onMediaTime={syncShortcut}
            fallback={
              <div className="mod-menu-shortcut">
                <p>{t("onboarding.modSettings.open")}</p>
              </div>
            }
          />
        </div>
        <div className="shortcut-preview">
          <kbd data-pressed={pressed >= 1}>Ctrl</kbd>
          <span aria-hidden="true">+</span>
          <kbd data-pressed={pressed === 2}>F10</kbd>
        </div>
      </div>
      <div className="mod-settings-copy">
        <div className="guide-heading">
          <span className="guide-eyebrow">{t("onboarding.modSettings.eyebrow")}</span>
          <h1 id="onboarding-title" tabIndex={-1}>
            {t("onboarding.modSettings.title")}
          </h1>
          <p>{t("onboarding.modSettings.description")}</p>
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
