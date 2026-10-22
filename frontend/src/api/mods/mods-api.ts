import type { Installation, InstalledMod } from "@/models/installation/installation-model";
import type {
  ArchiveSource,
  ModInspection,
  PresetKind,
  PresetValues,
} from "@/models/mods/mod-model";
import type { JrpKeyBindings, KeyViewerStyle } from "@/models/onboarding/key-bindings";
import type { OnboardingSetup } from "@/models/onboarding/onboarding-model";
export interface ModsApi {
  configureOnboarding(gamePath: string, kind: OnboardingSetup): Promise<void>;
  loadJrpKeys(gamePath: string): Promise<JrpKeyBindings>;
  saveJrpKeys(gamePath: string, keys: number[], style: KeyViewerStyle): Promise<JrpKeyBindings>;
  inspect(gamePath: string, source: ArchiveSource): Promise<ModInspection>;
  install(gamePath: string, source: ArchiveSource): Promise<Installation>;
  setEnabled(gamePath: string, id: string, enabled: boolean): Promise<Installation>;
  remove(gamePath: string, mod: InstalledMod): Promise<Installation>;
  loadPreset(gamePath: string, modId: string, kind: PresetKind): Promise<PresetValues>;
  savePreset(
    gamePath: string,
    modId: string,
    kind: PresetKind,
    values: PresetValues,
  ): Promise<PresetValues>;
}
