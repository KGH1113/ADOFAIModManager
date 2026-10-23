import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";
import {
  CopyGamePath,
  GameLocation,
  RevealGameFolder,
} from "@/components/installation/game-location";
import type { Installation } from "@/models/installation/installation-model";
import { Button } from "@/shared/ui/button";
import { Icon } from "@/shared/ui/icon";
import { usePreferences } from "@/state/preferences/preferences-store";

export function GameSetupCard({
  installation,
  busy,
  loading,
  choose,
  refresh,
  compact = false,
  loaderActions,
}: {
  installation?: Installation;
  busy: boolean;
  loading: boolean;
  choose(): void;
  refresh(): void;
  compact?: boolean;
  loaderActions?: ReactNode;
}) {
  const { t } = useTranslation();
  const registeredPath = usePreferences((state) => state.gamePath);
  const gamePath = installation?.gamePath ?? registeredPath;
  return (
    <section
      className={`setup-card ${compact ? "setup-card-compact" : ""}`}
      aria-label={t("install.status")}
    >
      <div className="setup-surface">
        <header className="setup-status-heading">
          <span>
            <Icon name={installation?.installed ? "ready" : "download"} size={18} />
            {t("install.status")}
          </span>
          <Button
            variant="ghost"
            size="icon"
            onClick={refresh}
            disabled={busy || loading}
            aria-label={t("common.refresh")}
          >
            <Icon name="refresh" className={loading ? "animate-spin" : ""} />
          </Button>
        </header>
        <div className="setup-overview">
          <h2>
            {t(
              loading && !installation
                ? "common.loading"
                : installation?.installed
                  ? "manager.connected.title"
                  : "onboarding.install.title",
            )}
          </h2>
          <p>
            {t(
              installation?.installed ? "setup.loaderInstalledHint" : "onboarding.install.subtitle",
            )}
          </p>
        </div>
        {installation?.gameRunning && (
          <p className="inline-warning">
            <Icon name="alert" size={16} />
            {t("onboarding.umm.quitGame")}
          </p>
        )}
        {installation?.warning && <p className="inline-warning">{installation.warning}</p>}
        <div className="setup-destination">
          <Icon name="folder" size={22} />
          <div className="setup-destination-content">
            <h3 className="setup-product-name">{t("setup.destination")}</h3>
            {gamePath ? (
              <GameLocation key={gamePath} path={gamePath} />
            ) : (
              <p className="setup-empty-path">
                {t(loading ? "common.loading" : "onboarding.game.chooseHint")}
              </p>
            )}
          </div>
          <div className="setup-location-actions">
            {gamePath && <CopyGamePath key={gamePath} path={gamePath} />}
            {gamePath && <RevealGameFolder path={gamePath} />}
            <Button size="small" onClick={choose} disabled={busy}>
              {t(gamePath ? "setup.changeFolder" : "onboarding.game.choose")}
            </Button>
          </div>
        </div>
        <div className="setup-loader">
          <Icon name="puzzle" size={22} />
          <div>
            <h3 className="setup-product-name">Unity Mod Manager</h3>
            <p className="setup-status">
              {t(installation?.installed ? "onboarding.mods.installed" : "install.notInstalled")}
              {installation?.installed && installation.version ? ` · ${installation.version}` : ""}
            </p>
          </div>
        </div>
      </div>
      {loaderActions && <footer className="setup-footer">{loaderActions}</footer>}
    </section>
  );
}
