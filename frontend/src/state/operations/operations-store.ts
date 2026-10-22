import { create } from "zustand";
import { errorMessage } from "@/shared/errors/api-error";

interface Operations {
  label: string | null;
  error: string | null;
  notice: string | null;
  setError(error: string | null): void;
  setNotice(notice: string | null): void;
}
export const useOperations = create<Operations>((set) => ({
  label: null,
  error: null,
  notice: null,
  setError: (error) => set({ error }),
  setNotice: (notice) => set({ notice }),
}));
// One mutation owner across every page. The lock is acquired synchronously before IPC.
export async function runOperation<T>(
  label: string,
  operation: () => Promise<T>,
): Promise<T | undefined> {
  if (useOperations.getState().label) return undefined;
  useOperations.setState({ label, error: null, notice: null });
  try {
    return await operation();
  } catch (error) {
    if (errorMessage(error) !== "cancelled") useOperations.setState({ error: errorMessage(error) });
    return undefined;
  } finally {
    useOperations.setState({ label: null });
  }
}
