import { useState } from "react";
import { useTranslation } from "react-i18next";
import { useApi } from "@/api/app-api-provider";
import { Button } from "@/shared/ui/button";
import { Icon } from "@/shared/ui/icon";
import { runOperation } from "@/state/operations/operations-store";

export function GameLocation({ path }: { path: string }) {
  return <p className="game-location-path">{path}</p>;
}

export function CopyGamePath({ path }: { path: string }) {
  const { t } = useTranslation();
  const [copied, setCopied] = useState(false);
  return (
    <Button
      variant="secondary"
      size="icon"
      className="game-location-copy"
      aria-label={t(copied ? "setup.copied" : "setup.copyPath")}
      title={t(copied ? "setup.copied" : "setup.copyPath")}
      onBlur={() => setCopied(false)}
      onClick={() =>
        void runOperation("setup.copyPath", async () => {
          await navigator.clipboard.writeText(path);
          setCopied(true);
        })
      }
    >
      <Icon name={copied ? "check" : "copy"} size={16} />
    </Button>
  );
}

export function RevealGameFolder({ path }: { path: string }) {
  const { t } = useTranslation();
  const api = useApi();
  return (
    <Button
      variant="secondary"
      size="icon"
      aria-label={t("common.showFolder")}
      title={t("common.showFolder")}
      onClick={() => void runOperation("common.showFolder", () => api.desktop.reveal(path))}
    >
      <Icon name="external" size={18} />
    </Button>
  );
}
