import { z } from "zod";
export const catalogModSchema = z.object({
  id: z.number().int(),
  slug: z.string().min(1),
  name: z.string(),
  version: z.string().nullish(),
  description: z.string().nullish(),
  creatorUsername: z.string().nullish(),
  sourceUploadedAt: z.string().nullish(),
  downloadUrl: z.string().nullish(),
  imageUrl: z.string().nullish(),
});
export const catalogSchema = z.array(catalogModSchema);
export type CatalogModDto = z.infer<typeof catalogModSchema>;
