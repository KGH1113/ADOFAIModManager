import { z } from "zod";
import { filterCatalog, mapCatalogDetail, mapCatalogMod } from "@/models/catalog/catalog-model";
import { catalogDetailSchema, catalogSchema } from "@/schemas/catalog/catalog-schema";
import type { DesktopClient } from "@/shared/clients/desktop-client";
import type { CatalogApi } from "./catalog-api";
export function createCatalogApi(client: DesktopClient): CatalogApi {
  return {
    list: async () => {
      const data = await client.invoke("catalog_fetch");
      return filterCatalog(catalogSchema.parse(data).map(mapCatalogMod), "");
    },
    details: async (id) => {
      const data = catalogDetailSchema.parse(await client.invoke("catalog_details", { slug: id }));
      if (data.mod.slug !== id) throw new Error("The catalog returned a different mod.");
      return mapCatalogDetail(data.mod);
    },
    download: async (url, requestId) =>
      z.string().parse(await client.invoke("download_mod", { url, requestId })),
    cancel: async (requestId) => {
      await client.invoke("cancel_download", { requestId });
    },
    discard: async (token) => {
      await client.invoke("discard_download", { token });
    },
  };
}
