import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { useApi } from "@/api/app-api-provider";
import type { LogSource } from "@/models/mods/mod-model";
export function useLogs(gamePath: string | null) {
  const api = useApi();
  const [source, setSource] = useState<LogSource>("game");
  const query = useQuery({
    queryKey: ["logs", gamePath, source],
    queryFn: () => api.logs.load(gamePath ?? "", source),
    enabled: !!gamePath,
    retry: 1,
  });
  return { ...query, source, setSource };
}
