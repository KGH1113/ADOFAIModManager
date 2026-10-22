import { useTranslation } from "react-i18next";
import { useManager } from "@/app/manager-provider";
import { Button } from "@/shared/ui/button";
import { Dialog } from "@/shared/ui/dialog";
import { Icon } from "@/shared/ui/icon";
import { type Language, usePreferences } from "@/state/preferences/preferences-store";
export function SettingsDialog() {
  const { t } = useTranslation();
  const { locked } = useManager();
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
    >
      <div className="settings-sections">
        <section className="settings-row">
          <label htmlFor="settings-language">{t("settings.language")}</label>
          <div className="settings-select">
            <select
              id="settings-language"
              value={language}
              onChange={(event) => setLanguage(event.target.value as Language)}
            >
              <option value="system">{t("settings.system")}</option>
              <option value="ko">한국어</option>
              <option value="en">English</option>
              <option value="zh">简体中文</option>
            </select>
            <Icon name="down" size={18} />
          </div>
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
            <small>0.2.0</small>
          </div>
          <p>{t("settings.aboutHint")}</p>
        </section>
      </div>
    </Dialog>
  );
}
