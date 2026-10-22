import { useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef, useState } from "react";
import { useApi } from "@/api/app-api-provider";
import { useManager } from "@/app/manager-provider";
import {
  installRecommendation,
  type RecommendationSession,
} from "@/models/onboarding/install-recommendation";
import type { RecommendationKey } from "@/models/onboarding/onboarding-model";
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
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      controller.current?.abort();
      if (request.current) void api.catalog.cancel(request.current).catch(() => {});
    };
  }, [api]);
  return {
    active,
    failed,
    install: (key: RecommendationKey) =>
      runOperation("onboarding.recommend.installing", async () => {
        const gamePath = game.data?.gamePath;
        if (!gamePath) throw new Error("choose_game");
        const abort = new AbortController();
        controller.current = abort;
        setFailed(null);
        try {
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
        } catch (error) {
          if (mounted.current) setFailed(key);
          throw error;
        } finally {
          request.current = undefined;
          controller.current = null;
          await cache.invalidateQueries({ queryKey: ["installation"] });
          if (mounted.current) setActive(null);
        }
      }),
    cancel: () => {
      controller.current?.abort();
      if (request.current) void api.catalog.cancel(request.current).catch(() => {});
    },
  };
}
