import { useQuery } from "@tanstack/react-query";
import { useDeferredValue, useMemo, useState } from "react";
import { useApi } from "@/api/app-api-provider";
import { type CatalogSort, filterCatalog } from "@/models/catalog/catalog-model";
import { catalogDetailQuery, catalogQuery } from "@/state/catalog/catalog-queries";
export function useCatalog() {
  const api = useApi();
  const query = useQuery(catalogQuery(api.catalog));
  const [search, setSearch] = useState("");
  const deferred = useDeferredValue(search);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [sort, setSort] = useState<CatalogSort>("newest");
  const [tagId, setTagId] = useState<number | null>(null);
  const detail = useQuery(catalogDetailQuery(api.catalog, selectedId));
  const items = useMemo(
    () => filterCatalog(query.data ?? [], deferred, sort, tagId),
    [query.data, deferred, sort, tagId],
  );
  const tags = useMemo(
    () =>
      [
        ...new Map(
          (query.data ?? []).flatMap((mod) => mod.tags).map((tag) => [tag.id, tag]),
        ).values(),
      ].sort((a, b) => a.name.localeCompare(b.name)),
    [query.data],
  );
  const selected = query.data?.find((item) => item.id === selectedId) ?? null;
  return {
    ...query,
    items,
    search,
    setSearch,
    selected,
    selectedId,
    select: setSelectedId,
    detail,
    sort,
    setSort,
    tagId,
    setTagId,
    tags,
    total: query.data?.length ?? 0,
  };
}
