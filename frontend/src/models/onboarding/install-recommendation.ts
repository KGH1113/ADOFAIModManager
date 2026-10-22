import type { AppApi } from "@/api/app-api";
import type { Installation } from "@/models/installation/installation-model";
import { compareVersions } from "@/models/mods/mod-updates";
import { type RecommendationKey, type RecommendedMod, recommendations } from "./onboarding-model";

export interface RecommendationSession {
  installed: Set<string>;
  configured: Set<string>;
}
export const sessionIdentity = (path: string, key: string) => JSON.stringify([path, key]);
export async function installRecommendation(
  api: AppApi,
  gamePath: string,
  key: RecommendationKey,
  session: RecommendationSession,
  signal: AbortSignal,
  progress: (name: string, requestId?: string) => void,
): Promise<Installation> {
  const checkCancellation = () => {
    if (signal.aborted) throw new Error("cancelled");
  };
  let game = await api.installation.load(gamePath);
  if (!game.installed || game.gameRunning)
    throw new Error(game.gameRunning ? "game_running" : "choose_game");
  const mod: RecommendedMod = recommendations[key];
  const plan = [...(mod.dependencies ?? []), key];
  const archives: { key: RecommendationKey; token: string }[] = [];
  const installed = (id: string) =>
    game.mods.find((m) => m.installed && m.id.toLowerCase() === id.toLowerCase());
  try {
    const catalog = await api.catalog.list();
    checkCancellation();
    // Resolve every required download before starting the installation plan.
    const downloads = new Map(
      plan.map((entry) => {
        const item = recommendations[entry];
        if (entry === key && installed(item.installedId)) return [entry, undefined] as const;
        const remote = catalog.find((candidate) => candidate.id === item.slug);
        if (!remote) throw new Error("download_unavailable");
        return [entry, remote] as const;
      }),
    );
    // Inspect the complete plan before modifying the game, including dependency identities.
    for (const entry of plan) {
      checkCancellation();
      const item: RecommendedMod = recommendations[entry];
      const existing = installed(item.installedId);
      if (existing && entry === key) continue;
      const remote = downloads.get(entry);
      if (!remote) throw new Error("download_unavailable");
      if (existing && (compareVersions(existing.version, remote.version) ?? 0) >= 0) continue;
      const requestId = crypto.randomUUID();
      progress(item.name, requestId);
      const token = await api.catalog.download(remote.url, requestId);
      archives.push({ key: entry, token });
      checkCancellation();
      const inspection = await api.mods.inspect(gamePath, { token });
      if (inspection.id.toLowerCase() !== item.installedId.toLowerCase())
        throw new Error("onboarding.recommend.archiveMismatch");
    }
    for (const entry of plan) {
      checkCancellation();
      const item = recommendations[entry];
      progress(item.name);
      const archive = archives.find((a) => a.key === entry);
      if (archive) {
        game = await api.mods.install(gamePath, { token: archive.token });
        session.installed.add(sessionIdentity(gamePath, entry));
      }
      // A pre-existing disabled dependency must be enabled for the chosen mod to work.
      const dependency = installed(item.installedId);
      if (entry !== key && dependency && !dependency.enabled)
        game = await api.mods.setEnabled(gamePath, dependency.id, true);
    }
    checkCancellation();
    const identity = sessionIdentity(gamePath, key);
    if (session.installed.has(identity) && !session.configured.has(identity)) {
      progress(mod.name);
      const kind = {
        jrp: "jrp-defaults",
        chatter: "chatter-defaults",
        tuf: "tuf-overlay",
        countdown: "countdown-defaults",
      } as const;
      if (key in kind) await api.mods.configureOnboarding(gamePath, kind[key as keyof typeof kind]);
      session.configured.add(identity);
    }
    return game;
  } finally {
    // Failure/cancellation may leave completed dependency installs; retry skips those safely.
    await Promise.allSettled(archives.map((a) => api.catalog.discard(a.token)));
  }
}
