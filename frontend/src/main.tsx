import { QueryClientProvider } from "@tanstack/react-query";
import { isTauri } from "@tauri-apps/api/core";
import React from "react";
import ReactDOM from "react-dom/client";
import { ApiProvider } from "@/api/app-api-provider";
import { createProductionApi } from "@/api/create-production-api";
import { AppRoot } from "@/app/app-root";
import { ManagerProvider } from "@/app/manager-provider";
import { queryClient } from "@/app/query-client";
import { UpdateProvider } from "@/components/updates/update-provider";
import i18n from "@/i18n/i18n";
import { createTauriClient } from "@/shared/clients/tauri-client";
import "./index.css";

async function main() {
  const element = document.getElementById("root");
  if (!element) throw new Error("Application root is missing");
  const root = ReactDOM.createRoot(element);
  const preview =
    import.meta.env.DEV && new URLSearchParams(location.search).get("preview") === "1";
  if (!isTauri() && !preview) {
    root.render(
      <div className="desktop-required">
        <img src="/assets/app-icon.png" alt="" width={64} height={64} />
        <h1>ADOFAI Mod Manager</h1>
        <p>{i18n.t("desktop.required")}</p>
        {import.meta.env.DEV && <a href="?preview=1">Browser preview</a>}
      </div>,
    );
    return;
  }
  const api = preview
    ? (await import("@/mocks/create-mock-api")).createMockApi()
    : createProductionApi(createTauriClient());
  root.render(
    <React.StrictMode>
      <QueryClientProvider client={queryClient}>
        <ApiProvider api={api}>
          <ManagerProvider>
            <UpdateProvider>
              <AppRoot preview={preview} />
            </UpdateProvider>
          </ManagerProvider>
        </ApiProvider>
      </QueryClientProvider>
    </React.StrictMode>,
  );
}
void main();
