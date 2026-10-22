import { useTranslation } from "react-i18next";
import { Icon } from "@/shared/ui/icon";

export function ExperienceCard({ returning, onSelect }: { returning: boolean; onSelect(): void }) {
  const { t } = useTranslation();
  const key = returning ? "returning" : "new";
  return (
    <button
      type="button"
      className="experience-card"
      onClick={onSelect}
      data-testid={`experience-${key}`}
    >
      <Icon name={returning ? "puzzle" : "package"} size={24} />
      <h2>{t(`onboarding.${key}.title`)}</h2>
      <p>{t(`onboarding.${key}.description`)}</p>
      <div className="experience-action">
        <span>{t(`onboarding.${key}.action`)}</span>
        <Icon name="right" size={18} />
      </div>
    </button>
  );
}
