import { queryOptions } from "@tanstack/react-query";
import type { InstallationApi } from "@/api/installation/installation-api";
export const installationKey = (path: string | null) => ["installation", path] as const;
export const installationQuery = (api: InstallationApi, path: string | null) =>
  queryOptions({
    queryKey: installationKey(path),
    queryFn: () => api.load(path),
    staleTime: 15_000,
    retry: 1,
  });
