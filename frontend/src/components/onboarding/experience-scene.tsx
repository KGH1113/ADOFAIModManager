import { useTranslation } from "react-i18next";
import { ExperienceCard } from "./experience-card";

export function ExperienceScene({ select }: { select(returning: boolean): void }) {
  const { t } = useTranslation();
  return (
    <div className="experience-scene">
      <div className="experience-heading">
        <h1 id="onboarding-title" tabIndex={-1}>
          {t("onboarding.experience.title")}
        </h1>
        <p>{t("onboarding.experience.subtitle")}</p>
      </div>
      <div className="experience-grid">
        <ExperienceCard returning onSelect={() => select(true)} />
        <ExperienceCard returning={false} onSelect={() => select(false)} />
      </div>
    </div>
  );
}
