import { useTranslation } from "react-i18next";
import type { InstalledMod } from "@/models/installation/installation-model";
import type { ModUpdate } from "@/models/mods/mod-updates";
import { Button } from "@/shared/ui/button";
import { Icon } from "@/shared/ui/icon";
export function ModRow({
  mod,
  disabled,
  toggle,
  remove,
  update,
  downloadUpdate,
}: {
  mod: InstalledMod;
  disabled: boolean;
  toggle(): void;
  remove(): void;
  update?: ModUpdate;
  downloadUpdate?(): void;
}) {
  const { t } = useTranslation();
  return (
    <li className={`mod-row ${mod.enabled ? "" : "mod-disabled"}`}>
      <input
        type="checkbox"
        checked={mod.enabled}
        onChange={toggle}
        disabled={disabled}
        aria-label={t("mods.enable", { name: mod.name })}
      />
      <span className="mod-initial" aria-hidden="true">
        {mod.name.slice(0, 1).toUpperCase()}
      </span>
      <div className="mod-row-text">
        <strong>{mod.name}</strong>
        <span>
          {mod.id} · {mod.version}
        </span>
        {update && (
          <small className={`mod-update-status mod-update-${update.state}`}>
            {t(`mods.update.${update.state}`, { version: update.latest?.version })}
          </small>
        )}
        {mod.requirements.some((item) => item.state !== "installed" && item.state !== "ok") && (
          <small>
            {t("mods.requirements")}: {mod.requirements.map((item) => item.id).join(", ")}
          </small>
        )}
      </div>
      {update?.state === "available" && (
        <Button
          size="small"
          disabled={disabled}
          onClick={downloadUpdate}
          aria-label={t("mods.update.downloadNamed", { name: mod.name })}
        >
          <Icon name="download" size={16} />
          {t("mods.update.download")}
        </Button>
      )}
      <Button
        variant="ghost"
        size="icon"
        className="mod-delete"
        onClick={remove}
        disabled={disabled}
        aria-label={`${t("common.remove")} ${mod.name}`}
      >
        <Icon name="trash" size={17} />
      </Button>
    </li>
  );
}
