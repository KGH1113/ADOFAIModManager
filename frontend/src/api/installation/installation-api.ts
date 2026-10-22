import type { Installation, InstallationAction } from "@/models/installation/installation-model";
export interface InstallationApi {
  load(gamePath: string | null): Promise<Installation>;
  execute(gamePath: string, action: InstallationAction): Promise<Installation>;
}
