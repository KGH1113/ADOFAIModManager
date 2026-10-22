export class ApiError extends Error {
  constructor(
    message: string,
    readonly code = "operation_failed",
  ) {
    super(message);
    this.name = "ApiError";
  }
}
export function errorMessage(error: unknown) {
  return error instanceof Error ? error.message : String(error);
}
