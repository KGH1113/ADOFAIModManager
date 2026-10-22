import { engineResponseSchema } from "@/schemas/engine/engine-schema";
import type { DesktopClient } from "@/shared/clients/desktop-client";
import { ApiError } from "@/shared/errors/api-error";
export type EngineGateway = ReturnType<typeof createEngineGateway>;
export function createEngineGateway(client: DesktopClient) {
  return async (action: string, gamePath: string | null, args: Record<string, unknown> = {}) => {
    const response = engineResponseSchema.parse(
      await client.invoke("engine_request", { request: { action, gamePath, ...args } }),
    );
    if (!response.ok && !(action === "status" && !response.detected))
      throw new ApiError(
        response.error ?? "The operation failed.",
        response.error === "game_running" ? "game_running" : "operation_failed",
      );
    return response;
  };
}
