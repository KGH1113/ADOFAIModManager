import type { AppApi } from "@/api/app-api";
import { mapCatalogDetail, mapCatalogMod, tufDownloadUrl } from "@/models/catalog/catalog-model";
import type { Installation } from "@/models/installation/installation-model";
import {
  copyKeyLayouts,
  defaultKeyLayouts,
  type JrpKeyBindings,
} from "@/models/onboarding/key-bindings";
import { type RecommendedMod, recommendations } from "@/models/onboarding/onboarding-model";

type PreviewMod = RecommendedMod & { version: string };
const previewVersions: Record<string, string> = {
  jalib: "1.0.0.45 beta45",
  jrp: "1.5.2.0",
  keyboardchatterblocker: "0.1.3",
  tufhelperlite: "0.1.9",
  tufhelper: "3.1.1",
  "enhanced-countdown": "0.2.3",
  bettercalibration: "1.2.2.0",
  youtubestream: "1.0.5",
  enhancedeffectremover: "1.7.0",
  adofaiipc: "0.4.1",
};
// Explicit development-only preview. No native command or filesystem operation is performed.
export function createMockApi(): AppApi {
  let game: Installation = {
    detected: true,
    gamePath: "/Preview/ADanceOfFireAndIce.app",
    gameRoot: "/Preview",
    modsPath: "/Preview/Mods",
    installed: false,
    version: null,
    bundledVersion: "0.32.5.0",
    hasBackup: false,
    warning: null,
    steamBranch: "public",
    alphaInstalled: false,
    gameRunning: false,
    mods: [],
    log: [],
  };
  const items: PreviewMod[] = Object.values(recommendations).map((item) => ({
    ...item,
    version: previewVersions[item.slug],
  }));
  const catalog = items.map((item, index) =>
    mapCatalogMod({
      id: index + 1,
      slug: item.slug,
      name: item.name,
      version: item.version,
      description: item.name,
      creatorUsername: "Preview",
      sourceUploadedAt: new Date(1_790_000_000_000 - index).toISOString(),
      isPinned: index < 3,
      likes: 3 + index,
      downloadCount: 119 + index * 17,
      tags: [
        {
          id: item.slug === "jalib" ? 6 : 2,
          name: item.slug === "jalib" ? "Dependency" : "Quality Of Life",
          color: "#70ff9b",
        },
      ],
    }),
  );
  const historicalItems: PreviewMod[] = ["0.1.2", "0.1.1"].map((version) => ({
    ...recommendations.chatter,
    version,
  }));
  let current: PreviewMod = items[1];
  let keyBindings: JrpKeyBindings = { style: 10, layouts: copyKeyLayouts(defaultKeyLayouts) };
  const archives = new Map<string, PreviewMod>();
  const resolveArchive = (source: { token?: string }) =>
    source.token ? (archives.get(source.token) ?? current) : current;
  return {
    installation: {
      load: async () => ({ ...game }),
      execute: async (_, action) => {
        game = {
          ...game,
          installed: action === "install" || action === "repair",
          version: "0.32.5.0",
          hasBackup: true,
        };
        return { ...game };
      },
    },
    mods: {
      configureOnboarding: async () => {},
      loadJrpKeys: async () => ({ ...keyBindings, layouts: copyKeyLayouts(keyBindings.layouts) }),
      saveJrpKeys: async (_, keys, style) => {
        keyBindings = { style, layouts: { ...keyBindings.layouts, [style]: [...keys] } };
        return { ...keyBindings, layouts: copyKeyLayouts(keyBindings.layouts) };
      },
      inspect: async (_, source) => {
        current = resolveArchive(source);
        return {
          id: current.installedId,
          name: current.name,
          version: current.version,
          alreadyInstalled: game.mods.some((mod) => mod.id === current.installedId),
        };
      },
      install: async (_, source) => {
        current = resolveArchive(source);
        game = {
          ...game,
          mods: [
            ...game.mods.filter((mod) => mod.id !== current.installedId),
            {
              id: current.installedId,
              name: current.name,
              version: current.version,
              path: `/Preview/Mods/${current.installedId}`,
              status: "installed",
              installed: true,
              enabled: true,
              requirements: [],
            },
          ],
        };
        return { ...game };
      },
      setEnabled: async (_, id, enabled) => {
        game = {
          ...game,
          mods: game.mods.map((mod) => (mod.id === id ? { ...mod, enabled } : mod)),
        };
        return { ...game };
      },
      remove: async (_, target) => {
        game = { ...game, mods: game.mods.filter((mod) => mod.id !== target.id) };
        return { ...game };
      },
      loadPreset: async () => [true, true, false],
      savePreset: async (_, _id, _kind, values) => values,
    },
    catalog: {
      list: async () => catalog,
      details: async (id) => {
        const item = catalog.find((mod) => mod.id === id);
        if (!item) throw new Error("Unknown preview mod");
        const versions = [
          item.version,
          ...historicalItems.filter((mod) => mod.slug === id).map((mod) => mod.version),
        ].map((version, index) => ({
          id: index + 1,
          version,
          notes: index ? `Release ${version}` : "Latest release",
          releasedAt: new Date(item.uploadedAt - index * 86_400_000).toISOString(),
          source: "hosted",
        }));
        return {
          ...mapCatalogDetail({
            id: 1,
            slug: id,
            name: item.name,
            version: item.version,
            versions,
            latestVersion: versions[0],
          }),
          ...item,
        };
      },
      download: async (url) => {
        const item = [...items, ...historicalItems].find(
          (item) => tufDownloadUrl(item.slug, item.version) === url,
        );
        if (!item) throw new Error("Unknown preview archive");
        const token = crypto.randomUUID();
        archives.set(token, item);
        return token;
      },
      cancel: async () => {},
      discard: async (token) => {
        archives.delete(token);
      },
    },
    logs: {
      load: async () => ({
        exists: true,
        text: "[Preview] Game started\n[Preview] Unity Mod Manager loaded\n[Preview] No errors",
        path: "/Preview/Player.log",
      }),
    },
    desktop: {
      runtime: async () => ({ platform: "macos", version: "0.2.0" }),
      chooseGame: async () => game.gamePath,
      chooseArchive: async () => "/Preview/mod.zip",
      openUrl: async () => {},
      reveal: async () => {},
      onFiles: async () => () => {},
    },
  };
}
