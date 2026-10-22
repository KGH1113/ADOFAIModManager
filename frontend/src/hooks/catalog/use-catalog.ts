import { useQuery } from "@tanstack/react-query";
import { useDeferredValue, useMemo, useState } from "react";
import { useApi } from "@/api/app-api-provider";
import { filterCatalog } from "@/models/catalog/catalog-model";
import { catalogQuery } from "@/state/catalog/catalog-queries";
export function useCatalog() {
  const api = useApi();
  const query = useQuery(catalogQuery(api.catalog));
  const [search, setSearch] = useState("");
  const deferred = useDeferredValue(search);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const items = useMemo(() => filterCatalog(query.data ?? [], deferred), [query.data, deferred]);
  const selected = items.find((item) => item.id === selectedId) ?? items[0] ?? null;
  return { ...query, items, search, setSearch, selected, select: setSelectedId };
}
