import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect } from "react";
import { useApi } from "@/api/app-api-provider";
import type { InstallationAction, InstalledMod } from "@/models/installation/installation-model";
import { installationKey, installationQuery } from "@/state/installation/installation-queries";
import { runOperation, useOperations } from "@/state/operations/operations-store";
import { usePreferences } from "@/state/preferences/preferences-store";

export function useInstallation() {
  const api = useApi();
  const cache = useQueryClient();
  const selectedPath = usePreferences((state) => state.gamePath);
  const setGamePath = usePreferences((state) => state.setGamePath);
  const watchingAlpha = usePreferences(
    (state) => state.welcomeOpen && state.onboarding.step === "alpha",
  );
  const busy = useOperations((state) => !!state.label);
  const query = useQuery({
    ...installationQuery(api.installation, selectedPath),
    refetchInterval: watchingAlpha && !busy ? 3_000 : false,
    refetchOnWindowFocus: watchingAlpha ? "always" : false,
  });
  const { refetch } = query;
  useEffect(() => {
    if (!watchingAlpha || busy) return;
    // Steam can change the manifest while this app is open. Ignore the cached
    // status on entry and check immediately when returning from Steam.
    const check = () => {
      void refetch({ cancelRefetch: false });
    };
    check();
    window.addEventListener("focus", check);
    return () => window.removeEventListener("focus", check);
  }, [watchingAlpha, busy, refetch]);
  const runtime = useQuery({
    queryKey: ["runtime"],
    queryFn: api.desktop.runtime,
    staleTime: Infinity,
  });
  const path = query.data?.gamePath ?? selectedPath;
  const update = async (operation: () => ReturnType<typeof api.installation.load>) => {
    const result = await operation();
    cache.setQueryData(installationKey(selectedPath), result);
    await cache.invalidateQueries({ queryKey: ["logs"] });
    return result;
  };
  return {
    ...query,
    path,
    runtime: runtime.data,
    refresh: () =>
      runOperation("새로 고침", async () => {
        await query.refetch({ throwOnError: true });
      }),
    chooseGame: () =>
      runOperation("onboarding.game.choose", async () => {
        const chosen = await api.desktop.chooseGame(runtime.data?.platform ?? "macos");
        if (!chosen) return;
        const result = await api.installation.load(chosen);
        if (!result.detected || !result.gamePath) throw new Error("invalid_game");
        cache.setQueryData(installationKey(result.gamePath), result);
        setGamePath(result.gamePath);
      }),
    execute: (action: InstallationAction) =>
      runOperation(
        action === "install" || action === "repair" ? "onboarding.umm.install" : "처리 중…",
        async () => {
          if (!path) throw new Error("choose_game");
          await update(() => api.installation.execute(path, action));
          useOperations.getState().setNotice("operation.complete");
        },
      ),
    toggle: (mod: InstalledMod) =>
      runOperation("처리 중…", async () => {
        if (!path) throw new Error("choose_game");
        await update(() => api.mods.setEnabled(path, mod.id, !mod.enabled));
      }),
    remove: (mod: InstalledMod) =>
      runOperation("처리 중…", async () => {
        if (!path) throw new Error("choose_game");
        await update(() => api.mods.remove(path, mod));
      }),
  };
}
