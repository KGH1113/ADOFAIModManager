import type { GameLog, LogSource } from "@/models/mods/mod-model";
export interface LogsApi {
  load(gamePath: string, source: LogSource): Promise<GameLog>;
}
