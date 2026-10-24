import { useTranslation } from "react-i18next";
import { useManager } from "@/app/manager-provider";
import { UpdateStatus } from "@/components/updates/update-provider";
import { Button } from "@/shared/ui/button";
import { Dialog } from "@/shared/ui/dialog";
import { Icon } from "@/shared/ui/icon";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/shared/ui/select";
import { type Language, usePreferences } from "@/state/preferences/preferences-store";
export function SettingsDialog() {
  const { t } = useTranslation();
  const { locked, game } = useManager();
  const open = usePreferences((state) => state.settingsOpen);
  const setOpen = usePreferences((state) => state.setSettingsOpen);
  const language = usePreferences((state) => state.language);
  const setLanguage = usePreferences((state) => state.setLanguage);
  const replay = usePreferences((state) => state.replay);
  return (
    <Dialog
      open={open}
      onOpenChange={setOpen}
      title={t("settings.title")}
      className="settings-dialog"
      autoFocusTitle
    >
      <div className="settings-sections">
        <section className="settings-row">
          <label htmlFor="settings-language">{t("settings.language")}</label>
          <Select value={language} onValueChange={(value) => setLanguage(value as Language)}>
            <SelectTrigger id="settings-language" className="settings-select">
              <SelectValue />
            </SelectTrigger>
            <SelectContent className="settings-select-content">
              <SelectItem value="system">{t("settings.system")}</SelectItem>
              <SelectItem value="ko">한국어</SelectItem>
              <SelectItem value="en">English</SelectItem>
              <SelectItem value="zh">简体中文</SelectItem>
            </SelectContent>
          </Select>
        </section>
        <section className="settings-row">
          <h3>{t("onboarding.settings.welcome")}</h3>
          <Button size="small" disabled={locked} onClick={replay}>
            {t("manager.returnToWelcome")}
            <Icon name="right" size={18} />
          </Button>
        </section>
        <section className="settings-about">
          <div className="settings-about-heading">
            <h3>ADOFAI Mod Manager</h3>
            <small>{game.runtime?.version ?? "—"}</small>
          </div>
          <p>{t("settings.aboutHint")}</p>
          <UpdateStatus settings />
        </section>
      </div>
    </Dialog>
  );
}
