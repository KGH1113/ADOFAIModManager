import { AnimatePresence, MotionConfig } from "motion/react";
import { useEffect } from "react";
import { GlobalFeedback } from "@/components/feedback/global-feedback";
import { SettingsDialog } from "@/components/settings/settings-dialog";
import { usePageActive } from "@/hooks/shared/use-page-active";
import i18n, { resolveLanguage } from "@/i18n/i18n";
import { OnboardingPage } from "@/pages/onboarding/onboarding-page";
import { ManagerWorkspace } from "@/sections/manager/manager-workspace";
import { RouteSurface } from "@/shared/motion/route-surface";
import { usePreferences } from "@/state/preferences/preferences-store";
export function AppRoot({ preview = false }: { preview?: boolean }) {
  const { welcomeOpen, language, hydrated, initialize, setSettingsOpen } = usePreferences();
  const active = usePageActive();
  useEffect(() => {
    if (!hydrated) initialize();
  }, [hydrated, initialize]);
  useEffect(() => {
    const resolved = resolveLanguage(language);
    void i18n.changeLanguage(resolved);
    document.documentElement.lang = resolved;
  }, [language]);
  useEffect(() => {
    const handle = (event: KeyboardEvent) => {
      if ((event.metaKey || event.ctrlKey) && event.key === ",") {
        event.preventDefault();
        setSettingsOpen(true);
      }
    };
    window.addEventListener("keydown", handle);
    return () => window.removeEventListener("keydown", handle);
  }, [setSettingsOpen]);
  if (!hydrated) return null;
  return (
    <MotionConfig reducedMotion="user" transition={{ type: "spring", bounce: 0, duration: 0.35 }}>
      <div className={`app-background ${active ? "" : "app-inactive"}`}>
        <div data-tauri-drag-region className="window-drag-region" />
        {preview && !welcomeOpen && <div className="preview-badge">{i18n.t("preview.label")}</div>}
        <AnimatePresence initial={false}>
          <RouteSurface screen key={welcomeOpen ? "onboarding" : "manager"}>
            {welcomeOpen ? <OnboardingPage /> : <ManagerWorkspace />}
          </RouteSurface>
        </AnimatePresence>
        <SettingsDialog />
        <GlobalFeedback />
      </div>
    </MotionConfig>
  );
}
