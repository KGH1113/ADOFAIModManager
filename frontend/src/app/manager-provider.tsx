import { createContext, type ReactNode, useContext } from "react";
import { useInstallation } from "@/hooks/installation/use-installation";
import { useModImport } from "@/hooks/mods/use-mod-import";
import { useOperations } from "@/state/operations/operations-store";

const ManagerContext = createContext<{
  game: ReturnType<typeof useInstallation>;
  imports: ReturnType<typeof useModImport>;
  busy: boolean;
  locked: boolean;
} | null>(null);
export function ManagerProvider({ children }: { children: ReactNode }) {
  const game = useInstallation();
  const imports = useModImport(game.data?.gamePath ?? null);
  const busy = useOperations((state) => !!state.label);
  return (
    <ManagerContext value={{ game, imports, busy, locked: busy || !!imports.pending }}>
      {children}
    </ManagerContext>
  );
}
export function useManager() {
  const manager = useContext(ManagerContext);
  if (!manager) throw new Error("ManagerProvider is missing");
  return manager;
}
