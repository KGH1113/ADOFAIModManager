import { createContext, type ReactNode, useContext, useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { useStore } from "zustand";
import { useApi } from "@/api/app-api-provider";
import { useManager } from "@/app/manager-provider";
import { Button } from "@/shared/ui/button";
import { useOperations } from "@/state/operations/operations-store";
import {
  createUpdateController,
  UPDATE_INTERVAL,
  type UpdateController,
} from "@/state/updates/update-controller";

const UpdateContext = createContext<UpdateController | null>(null);

export function UpdateProvider({ children }: { children: ReactNode }) {
  const api = useApi();
  const { imports } = useManager();
  const importsRef = useRef(imports);
  importsRef.current = imports;
  const [controller] = useState(() =>
    createUpdateController(api.updates, {
      acquire: () => {
        const current = importsRef.current;
        if (useOperations.getState().label || current.pending || current.queued || current.download)
          return false;
        useOperations.setState({ label: "updates.installing" });
        return true;
      },
      release: () => useOperations.setState({ label: null }),
    }),
  );
  const state = useStore(controller.store);
  const { t } = useTranslation();
  useEffect(() => {
    controller.start();
    const check = () => {
      void controller.check(true);
    };
    const visible = () => {
      if (document.visibilityState === "visible") check();
    };
    const timer = window.setInterval(check, UPDATE_INTERVAL);
    window.addEventListener("focus", check);
    window.addEventListener("online", check);
    document.addEventListener("visibilitychange", visible);
    return () => {
      window.clearInterval(timer);
      window.removeEventListener("focus", check);
      window.removeEventListener("online", check);
      document.removeEventListener("visibilitychange", visible);
    };
  }, [controller]);
  const blocking = state.startup || state.phase === "installing";
  return (
    <UpdateContext value={controller}>
      {blocking ? (
        <div className="update-startup">
          <div data-tauri-drag-region className="window-drag-region" />
          <img src="/assets/app-icon.png" alt="" width={64} height={64} />
          <h1>ADOFAI Mod Manager</h1>
          <p role="status">
            {t(`updates.${state.phase === "idle" ? "checking" : state.phase}`, {
              version: state.update?.version,
            })}
          </p>
          {state.phase !== "installing" && (
            <Button onClick={() => controller.defer()}>{t("updates.later")}</Button>
          )}
        </div>
      ) : (
        children
      )}
    </UpdateContext>
  );
}

export function UpdateStatus({ settings = false }: { settings?: boolean }) {
  const controller = useContext(UpdateContext);
  return controller ? <UpdateControls controller={controller} settings={settings} /> : null;
}

function UpdateControls({
  controller,
  settings,
}: {
  controller: UpdateController;
  settings: boolean;
}) {
  const { t } = useTranslation();
  const state = useStore(controller.store);
  const { locked, imports } = useManager();
  const pending =
    state.phase === "checking" || state.phase === "downloading" || state.phase === "installing";
  // Background check failures belong in settings; they should never interrupt work.
  if (!settings && !state.update) return null;
  const label = state.error
    ? "error"
    : state.phase === "idle"
      ? state.checked
        ? "current"
        : "hint"
      : state.phase;
  return (
    <section
      className={settings ? "update-settings" : "update-banner"}
      aria-label={t("updates.title")}
    >
      <p role="status">{t(`updates.${label}`, { version: state.update?.version })}</p>
      {settings && state.update?.notes && (
        <details className="update-notes">
          <summary>{t("updates.notes")}</summary>
          <p>{state.update.notes}</p>
        </details>
      )}
      {state.phase === "ready" ? (
        <Button
          size="small"
          variant="primary"
          disabled={locked || !!imports.queued || !!imports.download}
          onClick={() => {
            void controller.restart();
          }}
        >
          {t("updates.restart")}
        </Button>
      ) : (
        <Button
          size="small"
          pending={pending}
          onClick={() => {
            void controller.check();
          }}
        >
          {t(state.phase === "error" ? "updates.retry" : "updates.check")}
        </Button>
      )}
    </section>
  );
}
