import type { EngineGateway } from "@/api/engine/engine-gateway";
import { mapInstallation } from "@/models/installation/installation-model";
import type { ArchiveSource } from "@/models/mods/mod-model";
import type { ModsApi } from "./mods-api";

const archiveArgs = (source: ArchiveSource) =>
  source.token ? { downloadToken: source.token } : { zipPath: source.path };
export function createModsApi(engine: EngineGateway): ModsApi {
  return {
    async loadUmmShortcut(gamePath) {
      const result = await engine("ummshortcut", gamePath);
      if (!result.ummShortcut) throw new Error("UMM shortcut is unavailable.");
      return result.ummShortcut;
    },
    async saveUmmShortcut(gamePath, shortcut) {
      const result = await engine("saveummshortcut", gamePath, {
        shortcutKey: shortcut.keyCode,
        shortcutModifiers: shortcut.modifiers,
      });
      if (!result.ummShortcut) throw new Error("UMM shortcut could not be saved.");
      return result.ummShortcut;
    },
    async configureOnboarding(gamePath, kind) {
      await engine("onboardingsetup", gamePath, { presetKind: kind });
    },
    async loadJrpKeys(gamePath) {
      const result = await engine("jrpkeys", gamePath);
      if (!result.keyBindings) throw new Error("JRP key settings are unavailable.");
      return result.keyBindings;
    },
    async saveJrpKeys(gamePath, keyBindings, keyViewerStyle) {
      const result = await engine("savejrpkeys", gamePath, { keyBindings, keyViewerStyle });
      if (!result.keyBindings) throw new Error("JRP key settings could not be saved.");
      return result.keyBindings;
    },
    async inspect(gamePath, source) {
      const result = await engine("inspectmod", gamePath, archiveArgs(source));
      if (!result.modInspection)
        throw new Error("The archive does not contain valid mod metadata.");
      return result.modInspection;
    },
    install: async (gamePath, source) =>
      mapInstallation(await engine("installmod", gamePath, archiveArgs(source))),
    setEnabled: async (gamePath, modId, enabled) =>
      mapInstallation(await engine("setmodenabled", gamePath, { modId, enabled })),
    remove: async (gamePath, mod) =>
      mapInstallation(await engine("removemod", gamePath, { path: mod.path })),
    async loadPreset(gamePath, modId, presetKind) {
      const result = await engine("preset", gamePath, { modId, presetKind });
      if (!result.preset) throw new Error("Mod settings are unavailable.");
      return result.preset;
    },
    async savePreset(gamePath, modId, presetKind, presetValues) {
      const result = await engine("savepreset", gamePath, { modId, presetKind, presetValues });
      if (!result.preset) throw new Error("Mod settings could not be saved.");
      return result.preset;
    },
  };
}
