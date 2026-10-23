import type { CatalogMod, CatalogModDetail } from "@/models/catalog/catalog-model";
export interface CatalogApi {
  list(): Promise<CatalogMod[]>;
  details(id: string): Promise<CatalogModDetail>;
  download(url: string, requestId: string): Promise<string>;
  cancel(requestId: string): Promise<void>;
  discard(token: string): Promise<void>;
}
