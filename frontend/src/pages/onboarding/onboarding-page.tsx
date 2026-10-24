import { AnimatePresence } from "motion/react";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { useManager } from "@/app/manager-provider";
import { GameSetupCard } from "@/components/installation/game-setup-card";
import { CinematicScene } from "@/components/onboarding/cinematic-scene";
import { ExperienceScene } from "@/components/onboarding/experience-scene";
import { JrpKeysStep } from "@/components/onboarding/jrp-keys-step";
import { AlphaGuide, ModSettingsGuide } from "@/components/onboarding/setup-guides";
import { StagedScene } from "@/components/onboarding/staged-scene";
import { TufIntro } from "@/components/onboarding/tuf-intro";
import { WelcomeScene } from "@/components/onboarding/welcome-scene";
import { LanguageMenu } from "@/components/settings/language-menu";
import { useRecommendations } from "@/hooks/mods/use-recommendations";
import { type OnboardingEvent, setupSteps } from "@/models/onboarding/onboarding-model";
import {
  MoreModsStep,
  RecommendationStep,
  TufStep,
} from "@/sections/onboarding/recommendation-step";
import { Button } from "@/shared/ui/button";
import { Icon } from "@/shared/ui/icon";
import { usePreferences } from "@/state/preferences/preferences-store";

export function OnboardingPage() {
  const { t } = useTranslation();
  const { game, busy, locked } = useManager();
  const { step } = usePreferences((state) => state.onboarding);
  const alphaInstalled = !!game.data?.alphaInstalled;
  const jrpInstalled = !!game.data?.mods.some(
    (mod) => mod.installed && mod.id.toLowerCase() === "jipperresourcepack",
  );
  const sequence = setupSteps(alphaInstalled && step !== "alpha", jrpInstalled);
  const recommendations = useRecommendations();
  const [navigation, setNavigation] = useState({ step, direction: 1 });
  if (navigation.step !== step)
    setNavigation({
      step,
      direction: sequence.indexOf(step) > sequence.indexOf(navigation.step) ? 1 : -1,
    });
  const dispatch = usePreferences((state) => state.send);
  const send = (event: OnboardingEvent) => dispatch(event, alphaInstalled, jrpInstalled);
  const complete = usePreferences((state) => state.complete);
  const progress = sequence.indexOf(step);
  return (
    <div className="onboarding">
      <header className="onboarding-header">
        <LanguageMenu />
      </header>
      <div className="onboarding-stage">
        <AnimatePresence initial={false} custom={navigation.direction}>
          <CinematicScene key={step} step={step}>
            {step === "welcome" && (
              <WelcomeScene locked={locked} onContinue={() => send("continue")} />
            )}
            {step === "experience" && (
              <ExperienceScene select={(returning) => send(returning ? "returning" : "new")} />
            )}
            {step === "alpha" && <AlphaGuide next={() => send("next")} />}
            {step === "installation" && (
              <StagedScene kind="installation">
                <div className="onboarding-install">
                  <h1 id="onboarding-title" tabIndex={-1}>
                    {t("onboarding.install.title")}
                  </h1>
                  <p className="scene-description">{t("onboarding.install.subtitle")}</p>
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
                  <div className="onboarding-install-actions">
                    {game.data?.installed ? (
                      <>
                        <span className="installed-label">
                          <Icon name="check" />
                          {t("onboarding.mods.installed")}
                        </span>
                        <Button variant="primary" disabled={locked} onClick={() => send("next")}>
                          {t("onboarding.continue")}
                          <Icon name="right" />
                        </Button>
                      </>
                    ) : (
                      <Button
                        variant="primary"
                        pending={busy}
                        disabled={locked || !game.data?.detected || game.data.gameRunning}
                        onClick={() => {
                          void game.execute("install");
                        }}
                      >
                        <Icon name="download" />
                        {t("onboarding.umm.install")}
                      </Button>
                    )}
                  </div>
                </div>
              </StagedScene>
            )}
            {step === "modSettings" && <ModSettingsGuide next={() => send("next")} />}
            {step === "keyBindings" && <JrpKeysStep next={() => send("next")} />}
            {(step === "jrp" || step === "chatter") && (
              <RecommendationStep
                key={step}
                kind={step}
                flow={recommendations}
                next={() => send("next")}
              />
            )}
            {step === "tufIntro" && <TufIntro next={() => send("next")} disabled={locked} />}
            {step === "tuf" && <TufStep flow={recommendations} next={() => send("next")} />}
            {step === "more" && <MoreModsStep flow={recommendations} next={() => send("next")} />}
            {step === "ready" && (
              <StagedScene kind="ready">
                <div className="ready-scene">
                  <div className="ready-mark">
                    <Icon name="ready" size={32} />
                  </div>
                  <h1 id="onboarding-title" tabIndex={-1}>
                    {t("onboarding.ready.title")}
                  </h1>
                  <p className="scene-description">{t("onboarding.ready.returning")}</p>
                  <Button
                    variant="primary"
                    disabled={locked}
                    onClick={() => complete(!!game.data?.installed)}
                    data-testid="get-started"
                  >
                    {t("onboarding.getStarted")}
                    <Icon name="right" />
                  </Button>
                </div>
              </StagedScene>
            )}
          </CinematicScene>
        </AnimatePresence>
      </div>
      <footer className="onboarding-footer">
        <div>
          {step !== "welcome" && step !== "experience" && (
            <Button variant="ghost" size="small" disabled={locked} onClick={() => send("back")}>
              <Icon name="left" size={15} />
              {t("onboarding.back")}
            </Button>
          )}
          {step === "tufIntro" && (
            <Button variant="ghost" size="small" disabled={locked} onClick={() => send("next")}>
              {t("onboarding.skip")}
            </Button>
          )}
          {step === "installation" && (
            <Button variant="ghost" size="small" disabled={locked} onClick={() => send("defer")}>
              {t("onboarding.later")}
            </Button>
          )}
        </div>
        <div
          className="step-indicators"
          role="progressbar"
          aria-label={t("onboarding.step.setup")}
          aria-valuemin={0}
          aria-valuemax={sequence.length - 1}
          aria-valuenow={progress}
        >
          {sequence.map((name, index) => (
            <span key={name} className={progress === index ? "active" : ""} />
          ))}
        </div>
      </footer>
    </div>
  );
}
