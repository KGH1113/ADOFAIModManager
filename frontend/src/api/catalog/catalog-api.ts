import type { CatalogMod } from "@/models/catalog/catalog-model";
export interface CatalogApi {
  list(): Promise<CatalogMod[]>;
  download(url: string, requestId: string): Promise<string>;
  cancel(requestId: string): Promise<void>;
  discard(token: string): Promise<void>;
}
