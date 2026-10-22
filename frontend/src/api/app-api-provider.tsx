import { createContext, type ReactNode, useContext } from "react";
import type { AppApi } from "./app-api";

const ApiContext = createContext<AppApi | null>(null);
export function ApiProvider({ api, children }: { api: AppApi; children: ReactNode }) {
  return <ApiContext value={api}>{children}</ApiContext>;
}
export function useApi() {
  const api = useContext(ApiContext);
  if (!api) throw new Error("ApiProvider is missing");
  return api;
}
