import { useTranslation } from "react-i18next";
import type { Platform } from "@/models/onboarding/element-guide";
import { Icon } from "@/shared/ui/icon";

const copy = "onboarding.elementStart.permission";

function AppRow() {
  return (
    <span className="permission-app">
      <span className="permission-app-icon">
        <Icon name="keyboard" size={18} />
      </span>
      ElementNote
    </span>
  );
}

/** A simplified drawing of the prompts ElementNote triggers on its first launch. */
export function PermissionVisual({ platform }: { platform: Platform }) {
  const { t } = useTranslation();
  if (platform === "windows")
    return (
      <div className="permission-visual permission-windows" aria-hidden="true">
        <div className="permission-dialog permission-uac">
          <header>{t(`${copy}.uac`)}</header>
          <div className="permission-dialog-body">
            <strong>{t(`${copy}.uacQuestion`)}</strong>
            <AppRow />
          </div>
          <footer>
            <span className="permission-button" data-recommended="true">
              {t(`${copy}.yes`)}
              <em>{t(`${copy}.choose`)}</em>
            </span>
            <span className="permission-button">{t(`${copy}.no`)}</span>
          </footer>
        </div>
        <div className="permission-dialog permission-firewall">
          <header>{t(`${copy}.firewall`)}</header>
          <footer>
            <span className="permission-button">{t(`${copy}.allow`)}</span>
            <span className="permission-button" data-fine="true">
              {t(`${copy}.cancel`)}
              <em>{t(`${copy}.cancelFine`)}</em>
            </span>
          </footer>
        </div>
      </div>
    );
  return (
    <div className="permission-visual permission-macos" aria-hidden="true">
      <div className="permission-window">
        <header>
          <span className="permission-lights">
            <i />
            <i />
            <i />
          </span>
          {t(`${copy}.privacy`)}
        </header>
        {(["accessibility", "inputMonitoring"] as const).map((section, index) => (
          <section key={section}>
            <h3>{t(`${copy}.${section}`)}</h3>
            <div className="permission-row">
              <AppRow />
              <span
                className="permission-switch"
                style={{ animationDelay: `${0.7 + index * 0.6}s` }}
              >
                <span
                  className="permission-knob"
                  style={{ animationDelay: `${0.7 + index * 0.6}s` }}
                />
              </span>
            </div>
          </section>
        ))}
      </div>
    </div>
  );
}
