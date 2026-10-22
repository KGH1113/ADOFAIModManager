import { useQuery } from "@tanstack/react-query";
import { useMemo } from "react";
import { useApi } from "@/api/app-api-provider";
import type { InstalledMod } from "@/models/installation/installation-model";
import { checkModUpdate } from "@/models/mods/mod-updates";
import { catalogQuery } from "@/state/catalog/catalog-queries";

export function useModUpdates(mods: InstalledMod[]) {
  const api = useApi();
  const query = useQuery({ ...catalogQuery(api.catalog), enabled: false });
  const results = useMemo(
    () =>
      query.data && !query.isError
        ? new Map(mods.map((mod) => [mod.path, checkModUpdate(mod, query.data)]))
        : null,
    [mods, query.data, query.isError],
  );
  const available = results
    ? [...results.values()].filter((result) => result.state === "available").length
    : 0;
  return { ...query, results, available };
}
