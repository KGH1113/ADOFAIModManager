import { useQueryClient } from "@tanstack/react-query";
import { useCallback, useEffect, useRef, useState } from "react";
import { useApi } from "@/api/app-api-provider";
import type { CatalogMod } from "@/models/catalog/catalog-model";
import type { InstalledMod } from "@/models/installation/installation-model";
import type { ArchiveSource, PendingImport } from "@/models/mods/mod-model";
import { compareVersions } from "@/models/mods/mod-updates";
import { runOperation, useOperations } from "@/state/operations/operations-store";
import { usePreferences } from "@/state/preferences/preferences-store";

export function useModImport(gamePath: string | null) {
  const api = useApi();
  const cache = useQueryClient();
  const [pending, setPending] = useState<PendingImport | null>(null);
  const [queue, setQueue] = useState<string[]>([]);
  const [download, setDownload] = useState<{ id: string; requestId: string } | null>(null);
  const [completedImport, setCompletedImport] = useState<{
    eventId: string;
    gamePath: string;
    modId: string;
  } | null>(null);
  const busy = useOperations((state) => !!state.label);
  const mounted = useRef(true);
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);
  const inspect = useCallback(
    async (source: ArchiveSource, path: string, updating?: InstalledMod) => {
      try {
        const inspection = await api.mods.inspect(path, source);
        if (
          updating &&
          (inspection.id.toLowerCase() !== updating.id.toLowerCase() ||
            (compareVersions(inspection.version, updating.version) ?? 0) <= 0)
        )
          throw new Error("mods.update.archiveMismatch");
        if (mounted.current) setPending({ source, inspection, gamePath: path });
        else if (source.token) await api.catalog.discard(source.token);
      } catch (error) {
        if (source.token) await api.catalog.discard(source.token);
        throw error;
      }
    },
    [api],
  );

  useEffect(() => {
    let disposed = false;
    let stop: (() => void) | undefined;
    void api.desktop
      .onFiles((paths) => setQueue((current) => [...current, ...paths]))
      .then((unlisten) => {
        if (disposed) unlisten();
        else stop = unlisten;
      })
      .catch((error) => useOperations.getState().setError(String(error)));
    return () => {
      disposed = true;
      stop?.();
    };
  }, [api]);

  useEffect(() => {
    if (!queue.length || busy || pending) return;
    if (!gamePath) {
      usePreferences.getState().navigate("install");
      return;
    }
    const first = queue[0];
    setQueue((current) => current.slice(1));
    void runOperation("onboarding.mods.inspecting", () => inspect({ path: first }, gamePath));
  }, [queue, busy, pending, gamePath, inspect]);

  const cancel = async () => {
    if (busy || !pending) return;
    if (pending.source.token) await api.catalog.discard(pending.source.token);
    setPending(null);
  };
  return {
    pending,
    download,
    completedImport,
    queued: queue.length,
    choose: () =>
      runOperation("모드 추가", async () => {
        if (!gamePath) throw new Error("choose_game");
        const path = await api.desktop.chooseArchive();
        if (path) await inspect({ path }, gamePath);
      }),
    fromCatalog: (mod: CatalogMod, updating?: InstalledMod) =>
      runOperation("onboarding.mods.loading", async () => {
        if (!mod.url) throw new Error("download_unavailable");
        if (!gamePath) throw new Error("choose_game");
        const requestId = crypto.randomUUID();
        setDownload({ id: mod.id, requestId });
        try {
          const token = await api.catalog.download(mod.url, requestId);
          await inspect({ token }, gamePath, updating);
        } finally {
          setDownload(null);
        }
      }),
    cancelDownload: async () => {
      if (download) await api.catalog.cancel(download.requestId);
    },
    cancel: () => {
      void cancel().catch((error) => useOperations.getState().setError(String(error)));
    },
    confirm: () =>
      runOperation("모드 설치", async () => {
        if (!pending) return;
        // The game and archive are captured during inspection, never re-read from selection.
        await api.mods.install(pending.gamePath, pending.source);
        if (pending.source.token) await api.catalog.discard(pending.source.token);
        setPending(null);
        await cache.invalidateQueries({ queryKey: ["installation"] });
        setCompletedImport({
          eventId: crypto.randomUUID(),
          gamePath: pending.gamePath,
          modId: pending.inspection.id,
        });
        useOperations.getState().setNotice("operation.complete");
      }),
  };
}
