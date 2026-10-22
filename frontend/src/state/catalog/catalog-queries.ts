import { queryOptions } from "@tanstack/react-query";
import type { CatalogApi } from "@/api/catalog/catalog-api";
export const catalogQuery = (api: CatalogApi) =>
  queryOptions({ queryKey: ["catalog"], queryFn: () => api.list(), staleTime: 300_000, retry: 1 });
