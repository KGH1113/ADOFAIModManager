import { queryOptions } from "@tanstack/react-query";
import type { CatalogApi } from "@/api/catalog/catalog-api";
export const catalogQuery = (api: CatalogApi) =>
  queryOptions({ queryKey: ["catalog"], queryFn: () => api.list(), staleTime: 300_000, retry: 1 });
export const catalogDetailQuery = (api: CatalogApi, id: string | null) =>
  queryOptions({
    queryKey: ["catalog", "detail", id],
    queryFn: () => {
      if (!id) throw new Error("No mod selected.");
      return api.details(id);
    },
    enabled: id !== null,
    staleTime: 300_000,
    retry: 1,
  });
