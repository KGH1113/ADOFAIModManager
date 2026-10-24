import { useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef, useState } from "react";
import { useApi } from "@/api/app-api-provider";
import { useManager } from "@/app/manager-provider";
import {
  installRecommendation,
  type RecommendationSession,
} from "@/models/onboarding/install-recommendation";
import { type RecommendationKey, recommendations } from "@/models/onboarding/onboarding-model";
import { runOperation } from "@/state/operations/operations-store";

export function useRecommendations() {
  const api = useApi();
  const { game } = useManager();
  const cache = useQueryClient();
  const session = useRef<RecommendationSession>({
    installed: new Set(),
    configured: new Set(),
  });
  const controller = useRef<AbortController | null>(null);
  const mounted = useRef(true);
  const request = useRef<string | undefined>(undefined);
  const [active, setActive] = useState<{ key: RecommendationKey; name: string } | null>(null);
  const [failed, setFailed] = useState<RecommendationKey | null>(null);
  const [batch, setBatch] = useState(false);
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      controller.current?.abort();
      if (request.current) void api.catalog.cancel(request.current).catch(() => {});
    };
  }, [api]);
  const install = (keys: readonly RecommendationKey[], all = false) =>
    runOperation("onboarding.recommend.installing", async () => {
      const gamePath = game.data?.gamePath;
      if (!gamePath) throw new Error("choose_game");
      const abort = new AbortController();
      controller.current = abort;
      setBatch(all);
      const retry = failed;
      setFailed(null);
      let current = keys[0];
      try {
        for (const key of keys) {
          if (abort.signal.aborted) throw new Error("cancelled");
          if (
            all &&
            key !== retry &&
            game.data?.mods.some(
              (mod) =>
                mod.installed &&
                mod.id.toLowerCase() === recommendations[key].installedId.toLowerCase(),
            )
          )
            continue;
          current = key;
          if (mounted.current) setActive({ key, name: recommendations[key].name });
          await installRecommendation(
            api,
            gamePath,
            key,
            session.current,
            abort.signal,
            (name, requestId) => {
              request.current = requestId;
              if (mounted.current) setActive({ key, name });
            },
          );
          await cache.invalidateQueries({ queryKey: ["installation"] });
        }
      } catch (error) {
        if (mounted.current) setFailed(current);
        throw error;
      } finally {
        request.current = undefined;
        controller.current = null;
        await cache.invalidateQueries({ queryKey: ["installation"] });
        if (mounted.current) {
          setActive(null);
          setBatch(false);
        }
      }
    });
  return {
    active,
    failed,
    batch,
    install: (key: RecommendationKey) => install([key]),
    installAll: (keys: readonly RecommendationKey[]) => install(keys, true),
    cancel: () => {
      controller.current?.abort();
      if (request.current) void api.catalog.cancel(request.current).catch(() => {});
    },
  };
}
