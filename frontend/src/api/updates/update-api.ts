export interface AppUpdate {
  version: string;
  notes: string;
}

export interface UpdateApi {
  check(): Promise<AppUpdate | null>;
  download(): Promise<void>;
  install(): Promise<void>;
}
