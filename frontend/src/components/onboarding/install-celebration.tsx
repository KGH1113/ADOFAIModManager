import { useTranslation } from "react-i18next";
import { Icon } from "@/shared/ui/icon";

export function InstallCelebration({ name }: { name: string }) {
  const { t } = useTranslation();
  return (
    <div
      className="install-celebration"
      role="status"
      aria-label={`${name}: ${t("onboarding.mods.installed")}`}
      data-testid="install-celebration"
    >
      <Icon name="ready" size={18} />
      <span>
        {name} · {t("onboarding.mods.installed")}
      </span>
    </div>
  );
}
