export interface DesktopClient {
  invoke(command: string, args?: Record<string, unknown>): Promise<unknown>;
  chooseGame(platform: string): Promise<string | null>;
  chooseArchive(): Promise<string | null>;
  onFiles(handler: (paths: string[]) => void): Promise<() => void>;
}
