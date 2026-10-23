import { z } from "zod";
export const catalogTagSchema = z.object({
  id: z.number().int(),
  name: z.string(),
  color: z.string().nullish(),
});
export const catalogVersionSchema = z.object({
  id: z.number().int(),
  version: z.string().min(1),
  downloadUrl: z.string().nullish(),
  githubUrl: z.string().nullish(),
  notes: z.string().nullish(),
  releasedAt: z.string().nullish(),
  source: z.string().nullish(),
});
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
  projectUrl: z.string().nullish(),
  deprecatedAfter: z.string().nullish(),
  isPinned: z.boolean().nullish(),
  likes: z.number().nonnegative().nullish(),
  downloadCount: z.number().nonnegative().nullish(),
  tags: z.array(catalogTagSchema).nullish(),
  assignees: z.array(z.object({ name: z.string().nullish() })).nullish(),
});
export const catalogDetailSchema = z.object({
  mod: catalogModSchema.extend({
    versions: z.array(catalogVersionSchema).nullish(),
    latestVersion: catalogVersionSchema.nullish(),
  }),
});
export const catalogSchema = z.array(catalogModSchema);
export type CatalogModDto = z.infer<typeof catalogModSchema>;
export type CatalogDetailDto = z.infer<typeof catalogDetailSchema>["mod"];
