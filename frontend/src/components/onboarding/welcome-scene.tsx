import { useTranslation } from "react-i18next";
import { Button } from "@/shared/ui/button";
import { Icon } from "@/shared/ui/icon";

export function WelcomeScene({ locked, onContinue }: { locked: boolean; onContinue(): void }) {
  const { t } = useTranslation();
  return (
    <div className="welcome-scene">
      <h1 id="onboarding-title">ADOFAI Mod Manager</h1>
      <p className="scene-description">{t("onboarding.welcome.subtitle")}</p>
      <Button variant="primary" className="welcome-continue" disabled={locked} onClick={onContinue}>
        {t("onboarding.continue")}
        <Icon name="right" />
      </Button>
    </div>
  );
}
