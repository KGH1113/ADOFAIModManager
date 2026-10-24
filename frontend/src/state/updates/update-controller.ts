import { createStore } from "zustand/vanilla";
import type { AppUpdate, UpdateApi } from "@/api/updates/update-api";

// Only an explicitly built E2E app polls quickly; normal release builds use 30 minutes.
export const UPDATE_INTERVAL = import.meta.env.MODE === "updater-e2e" ? 5_000 : 30 * 60 * 1000;
type Phase = "idle" | "checking" | "downloading" | "ready" | "installing" | "error";
interface UpdateState {
  phase: Phase;
  startup: boolean;
  update: AppUpdate | null;
  error: boolean;
  checked: boolean;
}

export function createUpdateController(
  api: UpdateApi,
  lock: { acquire(): boolean; release(): void },
) {
  const store = createStore<UpdateState>(() => ({
    phase: "idle",
    startup: true,
    update: null,
    error: false,
    checked: false,
  }));
  let running = false;
  let started = false;
  let downloaded = false;
  let lastCheck = -Infinity;

  async function install() {
    if (!downloaded || !lock.acquire()) return;
    store.setState({ phase: "installing", error: false });
    try {
      await api.install();
      // Native installation restarts/exits the app. Keep the UI locked while
      // that happens; a resolved mock must not permit another installation.
    } catch {
      store.setState({ phase: "ready", error: true, startup: false });
      lock.release();
    }
  }

  async function check(automatic = false) {
    if (running || downloaded || store.getState().phase === "installing") return;
    if (automatic && Date.now() - lastCheck < UPDATE_INTERVAL) return;
    running = true;
    lastCheck = Date.now();
    store.setState({ phase: "checking", error: false });
    try {
      const update = await api.check();
      store.setState({ update, checked: true });
      if (!update) {
        store.setState({ phase: "idle" });
        return;
      }
      store.setState({ phase: "downloading" });
      await api.download();
      downloaded = true;
      store.setState({ phase: "ready" });
      // Re-read after downloading: "Later" must revoke startup installation
      // even if a check or download is still in flight.
      if (store.getState().startup) await install();
    } catch {
      store.setState({ phase: "error", error: true });
    } finally {
      running = false;
      if (store.getState().phase !== "installing") store.setState({ startup: false });
    }
  }

  return {
    store,
    check,
    start() {
      if (started) return;
      started = true;
      void check();
    },
    defer() {
      if (store.getState().phase !== "installing") store.setState({ startup: false });
    },
    async restart() {
      if (running || store.getState().phase !== "ready") return;
      running = true;
      try {
        await install();
      } finally {
        running = false;
      }
    },
  };
}

export type UpdateController = ReturnType<typeof createUpdateController>;
