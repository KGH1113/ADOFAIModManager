import { motion, useReducedMotion } from "motion/react";
import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { useManager } from "@/app/manager-provider";
import { PreviewVideo } from "@/components/onboarding/preview-video";
import type { useRecommendations } from "@/hooks/mods/use-recommendations";
import {
  moreMods,
  type RecommendationKey,
  recommendations,
} from "@/models/onboarding/onboarding-model";
import { Button } from "@/shared/ui/button";
import { Icon } from "@/shared/ui/icon";
import { Popover, PopoverContent, PopoverTrigger } from "@/shared/ui/popover";

type Flow = ReturnType<typeof useRecommendations>;
function InstallAllButton({
  modKeys,
  flow,
}: {
  modKeys: readonly RecommendationKey[];
  flow: Flow;
}) {
  const { t } = useTranslation();
  const { game, locked } = useManager();
  const complete = modKeys.every(
    (key) =>
      key !== flow.failed &&
      game.data?.mods.some(
        (mod) =>
          mod.installed && mod.id.toLowerCase() === recommendations[key].installedId.toLowerCase(),
      ),
  );
  return (
    <Button
      disabled={locked || !game.data?.installed || game.data.gameRunning || complete}
      pending={flow.batch}
      onClick={() => void flow.installAll(modKeys)}
    >
      {!flow.batch && <Icon name={complete ? "check" : "download"} size={16} />}
      {t(complete ? "onboarding.recommend.allInstalled" : "onboarding.recommend.installAll")}
    </Button>
  );
}
function InstallButton({ modKey, flow }: { modKey: RecommendationKey; flow: Flow }) {
  const { t } = useTranslation();
  const { game, locked } = useManager();
  const mod = recommendations[modKey];
  const installed = game.data?.mods.some(
    (m) => m.installed && m.id.toLowerCase() === mod.installedId.toLowerCase(),
  );
  const retry = flow.failed === modKey;
  return (
    <Button
      variant={installed && !retry ? "secondary" : "primary"}
      disabled={locked || !game.data?.installed || game.data.gameRunning || (installed && !retry)}
      pending={flow.active?.key === modKey}
      onClick={() => {
        void flow.install(modKey);
      }}
      data-testid={`install-${modKey}`}
    >
      {!flow.active && <Icon name={installed && !retry ? "check" : "download"} size={16} />}
      {t(retry ? "onboarding.retry" : installed ? "onboarding.mods.installed" : "common.install")}
    </Button>
  );
}
function Progress({ flow }: { flow: Flow }) {
  const { t } = useTranslation();
  const { game } = useManager();
  return (
    <>
      {flow.active && (
        <div className="recommendation-progress" role="status">
          <span>{t("onboarding.recommend.installingNamed", { name: flow.active.name })}</span>
          <Button variant="ghost" size="small" onClick={flow.cancel}>
            {t("common.cancel")}
          </Button>
        </div>
      )}
      {flow.failed && (
        <p className="inline-warning" role="status">
          {t("onboarding.recommend.retryHint")}
        </p>
      )}
      {game.data?.gameRunning && <p className="inline-warning">{t("onboarding.umm.quitGame")}</p>}
    </>
  );
}
export function RecommendationStep({
  kind,
  flow,
  next,
}: {
  kind: "jrp" | "chatter";
  flow: Flow;
  next(): void;
}) {
  const { t } = useTranslation();
  const { game, locked } = useManager();
  const mod = recommendations[kind];
  const installed = game.data?.mods.some(
    (m) => m.installed && m.id.toLowerCase() === mod.installedId.toLowerCase(),
  );
  return (
    <div className="recommendation-step">
      <div className="recommendation-sidebar">
        <div className="recommendation-heading">
          <div>
            <h1 id="onboarding-title" tabIndex={-1}>
              {mod.name}
            </h1>
            <p>{t(`onboarding.recommend.${kind}.description`)}</p>
          </div>
        </div>
        <p className="recommendation-note">{t(`onboarding.recommend.${kind}.note`)}</p>
        <Progress flow={flow} />
        <footer className="recommendation-actions">
          <div>
            <InstallButton modKey={kind} flow={flow} />
            <Button
              variant="ghost"
              disabled={locked}
              onClick={() => {
                next();
              }}
            >
              {t(installed ? "onboarding.continue" : "onboarding.skip")}
              <Icon name="right" />
            </Button>
          </div>
        </footer>
      </div>
      <div className="recommendation-media">
        <PreviewVideo kind={kind} name={mod.name} />
      </div>
    </div>
  );
}

export function TufStep({ flow, next }: { flow: Flow; next(): void }) {
  const { t } = useTranslation();
  const { locked } = useManager();
  const reduced = useReducedMotion();
  return (
    <div className="tuf-step tuf-step-comparison">
      <div className="experience-heading">
        <h1 id="onboarding-title" tabIndex={-1}>
          {t("onboarding.recommend.tuf.title")}
        </h1>
        <p>{t("onboarding.recommend.tuf.subtitle")}</p>
      </div>
      <div className="tuf-options">
        {(["tuf", "tufLite"] as const).map((key) => (
          <motion.section
            key={key}
            className="tuf-option"
            initial={{ opacity: 0, x: reduced ? 0 : 8 }}
            animate={{ opacity: 1, x: 0 }}
            transition={{ duration: reduced ? 0 : 0.2, ease: "easeOut" }}
            aria-labelledby={`title-${key}`}
          >
            <div className="tuf-option-heading">
              <div className="tuf-option-name">
                <h2 id={`title-${key}`}>{recommendations[key].name}</h2>
                {key === "tuf" && (
                  <Popover>
                    <PopoverTrigger asChild>
                      <Button
                        variant="ghost"
                        size="icon"
                        className="tuf-settings-trigger"
                        aria-label={t("onboarding.recommend.setupDetails", {
                          name: recommendations[key].name,
                        })}
                        title={t("onboarding.recommend.setupDetails", {
                          name: recommendations[key].name,
                        })}
                      >
                        <Icon name="info" size={18} />
                      </Button>
                    </PopoverTrigger>
                    <PopoverContent
                      className="tuf-settings-popover"
                      aria-label={t("onboarding.recommend.setupDetails", {
                        name: recommendations[key].name,
                      })}
                    >
                      <p>{t(`onboarding.recommend.${key}.note`)}</p>
                    </PopoverContent>
                  </Popover>
                )}
              </div>
              <InstallButton modKey={key} flow={flow} />
            </div>
            <div className="tuf-media-stage">
              <div className="tuf-media">
                <PreviewVideo kind={key} name={recommendations[key].name} />
              </div>
            </div>
            <p className="tuf-option-description">{t(`onboarding.recommend.${key}.description`)}</p>
          </motion.section>
        ))}
      </div>
      <Progress flow={flow} />
      <div className="recommendation-continue">
        <InstallAllButton modKeys={["tuf", "tufLite"]} flow={flow} />
        <Button variant="primary" disabled={locked} onClick={next}>
          {t("onboarding.continue")}
          <Icon name="right" />
        </Button>
      </div>
    </div>
  );
}
export function MoreModsStep({ flow, next }: { flow: Flow; next(): void }) {
  const { t } = useTranslation();
  const { locked } = useManager();
  const listRef = useRef<HTMLUListElement>(null);
  const [hasMoreBelow, setHasMoreBelow] = useState(false);
  useEffect(() => {
    const list = listRef.current;
    if (!list) return;
    const update = () =>
      setHasMoreBelow(list.scrollHeight - list.clientHeight - list.scrollTop > 2);
    const observer = typeof ResizeObserver === "undefined" ? null : new ResizeObserver(update);
    observer?.observe(list);
    for (const child of list.children) observer?.observe(child);
    window.addEventListener("resize", update);
    list.addEventListener("scroll", update, { passive: true });
    update();
    return () => {
      observer?.disconnect();
      window.removeEventListener("resize", update);
      list.removeEventListener("scroll", update);
    };
  }, []);
  return (
    <div className="more-mods-step">
      <div className="experience-heading">
        <h1 id="onboarding-title" tabIndex={-1}>
          {t("onboarding.recommend.more.title")}
        </h1>
        <p>{t("onboarding.recommend.more.subtitle")}</p>
      </div>
      <div className="recommended-mod-scroll">
        <ul
          className="recommended-mod-list"
          ref={listRef}
          aria-label={t("onboarding.recommend.more.title")}
        >
          {moreMods.map((key) => (
            <li key={key}>
              <div className="recommended-mod-copy">
                <h2>{recommendations[key].name}</h2>
                <p>{t(`onboarding.recommend.${key}.description`)}</p>
              </div>
              <div className="recommended-mod-note">
                {key === "calibration" && <small>{t("onboarding.recommend.dependency")}</small>}
                {key === "countdown" && <small>{t("onboarding.recommend.countdown.note")}</small>}
              </div>
              <div className="recommended-mod-action">
                <InstallButton modKey={key} flow={flow} />
              </div>
            </li>
          ))}
        </ul>
        {hasMoreBelow && (
          <div className="recommended-mod-scroll-hint" aria-hidden="true">
            <Icon name="down" size={18} />
          </div>
        )}
      </div>
      <Progress flow={flow} />
      <div className="recommendation-continue">
        <InstallAllButton modKeys={moreMods} flow={flow} />
        <Button variant="primary" disabled={locked} onClick={next}>
          {t("onboarding.continue")}
          <Icon name="right" />
        </Button>
      </div>
    </div>
  );
}
