import { useTranslation } from "react-i18next";
import type { Installation } from "@/models/installation/installation-model";
import { Button } from "@/shared/ui/button";
import { Icon } from "@/shared/ui/icon";
export function GameSetupCard({
  installation,
  busy,
  loading,
  choose,
  refresh,
  compact = false,
}: {
  installation?: Installation;
  busy: boolean;
  loading: boolean;
  choose(): void;
  refresh(): void;
  compact?: boolean;
}) {
  const { t } = useTranslation();
  return (
    <section
      className={`setup-card ${compact ? "setup-card-compact" : ""}`}
      aria-label={t("install.status")}
    >
      <div className="setup-card-heading">
        <span>
          <Icon name="ready" size={16} />
          {t("install.status")}
        </span>
        <Button
          variant="ghost"
          size="icon"
          onClick={refresh}
          disabled={busy || loading}
          aria-label={t("common.refresh")}
        >
          <Icon name="refresh" size={16} className={loading ? "animate-spin" : ""} />
        </Button>
      </div>
      {!compact && (
        <div className="setup-overview">
          <div>
            <h2>
              {t(installation?.installed ? "manager.connected.title" : "manager.connect.title")}
            </h2>
            <p>
              {t(
                installation?.installed ? "manager.connected.subtitle" : "manager.connect.subtitle",
              )}
            </p>
          </div>
        </div>
      )}
      <div className="setup-detail">
        <div className="setup-row">
          <span className="setup-row-icon">
            <Icon name="folder" size={25} />
          </span>
          <div className="min-w-0 flex-1">
            <strong>{t("얼불춤")}</strong>
            <p className="truncate" title={installation?.gamePath ?? undefined}>
              {loading
                ? t("common.loading")
                : installation?.detected
                  ? installation.gamePath
                  : t("onboarding.game.missing")}
            </p>
          </div>
          <Button size="small" onClick={choose} disabled={busy}>
            <Icon name="folder" size={16} />
            {t(installation?.detected ? "setup.changeFolder" : "onboarding.game.choose")}
          </Button>
        </div>
        <div className="setup-row">
          <span className={`setup-row-icon ${installation?.installed ? "text-success" : ""}`}>
            <Icon name="puzzle" size={25} />
          </span>
          <div>
            <strong>Unity Mod Manager</strong>
            <p>
              {installation?.installed
                ? `${t("onboarding.mods.installed")} · ${installation.version ?? ""}`
                : t("install.notInstalled")}
            </p>
          </div>
        </div>
      </div>
      {installation?.gameRunning && (
        <p className="inline-warning">
          <Icon name="alert" size={16} />
          {t("onboarding.umm.quitGame")}
        </p>
      )}
      {installation?.warning && <p className="inline-warning">{installation.warning}</p>}
    </section>
  );
}
