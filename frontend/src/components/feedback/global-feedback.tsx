import { useEffect } from "react";
import { useTranslation } from "react-i18next";
import { useManager } from "@/app/manager-provider";
import { Button } from "@/shared/ui/button";
import { Dialog } from "@/shared/ui/dialog";
import { Icon } from "@/shared/ui/icon";
import { useOperations } from "@/state/operations/operations-store";
export function GlobalFeedback() {
  const { t } = useTranslation();
  const { imports, busy } = useManager();
  const { error, setError, notice, setNotice } = useOperations();
  useEffect(() => {
    if (!notice) return;
    const timer = window.setTimeout(() => setNotice(null), 4500);
    return () => window.clearTimeout(timer);
  }, [notice, setNotice]);
  const known =
    error &&
    ["choose_game", "invalid_game", "game_running", "download_unavailable"].includes(error);
  return (
    <>
      <Dialog
        open={!!imports.pending}
        onOpenChange={(open) => {
          if (!open) imports.cancel();
        }}
        title={t("mods.importTitle")}
        description={t(
          imports.pending?.inspection.alreadyInstalled ? "mods.replaceHint" : "mods.importHint",
        )}
        locked={busy}
      >
        <div className="import-metadata">
          <strong>{imports.pending?.inspection.name}</strong>
          <span>
            {imports.pending?.inspection.id} · {imports.pending?.inspection.version}
          </span>
          <code>{imports.pending?.gamePath}</code>
        </div>
        <div className="dialog-actions">
          <Button disabled={busy} onClick={imports.cancel}>
            {t("common.cancel")}
          </Button>
          <Button
            variant="primary"
            pending={busy}
            onClick={() => {
              void imports.confirm();
            }}
          >
            {t("common.install")}
          </Button>
        </div>
      </Dialog>
      <Dialog
        open={!!error}
        onOpenChange={() => setError(null)}
        title={t("operation.failed")}
        description={known ? t(`error.${error}`) : undefined}
      >
        {!known && (
          <details className="error-details" open>
            <summary>{t("operation.details")}</summary>
            <p>{error}</p>
          </details>
        )}
        <div className="dialog-actions">
          <Button onClick={() => setError(null)}>{t("common.close")}</Button>
        </div>
      </Dialog>
      {notice && (
        <div className="notice" role="status">
          <Icon name="check" size={16} />
          {t(notice)}
        </div>
      )}
    </>
  );
}
