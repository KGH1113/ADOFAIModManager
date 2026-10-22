import { AnimatePresence } from "motion/react";
import { lazy, Suspense, useEffect } from "react";
import { destinations, Sidebar } from "@/components/layout/sidebar";
import { InstallationPage } from "@/pages/installation/installation-page";
import { LogsPage } from "@/pages/logs/logs-page";
import { ModsPage } from "@/pages/mods/mods-page";
import { RouteSurface } from "@/shared/motion/route-surface";
import { usePreferences } from "@/state/preferences/preferences-store";

const CatalogPage = lazy(() =>
  import("@/pages/catalog/catalog-page").then((module) => ({ default: module.CatalogPage })),
);
export function ManagerWorkspace() {
  const page = usePreferences((state) => state.page);
  const navigate = usePreferences((state) => state.navigate);
  const settings = usePreferences((state) => state.setSettingsOpen);
  useEffect(() => {
    const handle = (event: KeyboardEvent) => {
      if (!(event.metaKey || event.ctrlKey) || event.altKey || event.shiftKey) return;
      const destination = destinations[Number(event.key) - 1];
      if (destination) {
        event.preventDefault();
        navigate(destination.page);
      }
    };
    window.addEventListener("keydown", handle);
    return () => window.removeEventListener("keydown", handle);
  }, [navigate]);
  return (
    <div className="manager-workspace">
      <Sidebar page={page} navigate={navigate} settings={() => settings(true)} />
      <main className="workspace-main">
        <AnimatePresence initial={false}>
          <RouteSurface key={page}>
            {page === "install" ? (
              <InstallationPage />
            ) : page === "catalog" ? (
              <Suspense fallback={<div className="empty-state" aria-busy="true" />}>
                <CatalogPage />
              </Suspense>
            ) : page === "mods" ? (
              <ModsPage />
            ) : (
              <LogsPage />
            )}
          </RouteSurface>
        </AnimatePresence>
      </main>
    </div>
  );
}
