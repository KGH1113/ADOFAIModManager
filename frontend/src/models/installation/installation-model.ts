import type { EngineResponseDto, InstalledModDto } from "@/schemas/engine/engine-schema";
export type InstalledMod = InstalledModDto;
export type Installation = ReturnType<typeof mapInstallation>;
export type InstallationAction = "install" | "repair" | "remove" | "restore";
export function mapInstallation(dto: EngineResponseDto) {
  return {
    detected: dto.detected,
    gamePath: dto.status?.appPath ?? null,
    gameRoot: dto.status?.gameRoot ?? null,
    modsPath: dto.status?.modsPath ?? null,
    installed: !!dto.status?.hookInstalled && !!dto.status?.managerInstalled,
    version: dto.status?.managerVersion ?? null,
    bundledVersion: dto.bundledVersion ?? null,
    hasBackup: dto.status?.hasBackup ?? false,
    warning: dto.status?.warning ?? null,
    steamBranch: dto.status?.steamBranch ?? null,
    alphaInstalled: dto.status?.alphaInstalled ?? false,
    gameRunning: dto.gameRunning,
    mods: dto.mods.filter((mod) => mod.installed),
    log: dto.log,
  };
}
