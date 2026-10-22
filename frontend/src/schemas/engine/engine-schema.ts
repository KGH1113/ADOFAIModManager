import { z } from "zod";

const keyArray = z.array(z.number().int().min(0).max(0x10fff));

export const installedModSchema = z.object({
  id: z.string(),
  name: z.string(),
  version: z.string(),
  path: z.string(),
  status: z.string(),
  installed: z.boolean(),
  enabled: z.boolean(),
  managerVersion: z.string().nullish(),
  homePage: z.string().nullish(),
  requirements: z
    .array(z.object({ id: z.string(), version: z.string().nullish(), state: z.string() }))
    .default([]),
});
export const inspectionSchema = z.object({
  id: z.string(),
  name: z.string(),
  version: z.string(),
  alreadyInstalled: z.boolean(),
});
export const engineResponseSchema = z.object({
  ok: z.boolean(),
  detected: z.boolean(),
  error: z.string().nullish(),
  gameRunning: z.boolean().default(false),
  bundledVersion: z.string().nullish(),
  status: z
    .object({
      gameRoot: z.string().optional(),
      appPath: z.string().optional(),
      managedPath: z.string().optional(),
      modsPath: z.string().optional(),
      processArch: z.string().optional(),
      gameArch: z.string().optional(),
      hookInstalled: z.boolean().optional(),
      managerInstalled: z.boolean().optional(),
      managerVersion: z.string().nullish(),
      hasBackup: z.boolean().optional(),
      backupPath: z.string().optional(),
      warning: z.string().nullish(),
      steamBranch: z.string().nullish(),
      alphaInstalled: z.boolean().default(false),
    })
    .optional(),
  mods: z.array(installedModSchema).default([]),
  log: z.array(z.object({ level: z.string(), message: z.string() })).default([]),
  logText: z.string().optional(),
  logExists: z.boolean().optional(),
  logPath: z.string().optional(),
  modInspection: inspectionSchema.optional(),
  preset: z.tuple([z.boolean(), z.boolean(), z.boolean()]).optional(),
  keyBindings: z
    .object({
      style: z.union([z.literal(4), z.literal(8), z.literal(10), z.literal(12), z.literal(16)]),
      layouts: z.object({
        4: keyArray.length(4),
        8: keyArray.length(8),
        10: keyArray.length(10),
        12: keyArray.length(12),
        16: keyArray.length(16),
      }),
    })
    .optional(),
});
export type EngineResponseDto = z.infer<typeof engineResponseSchema>;
export type InstalledModDto = z.infer<typeof installedModSchema>;
