import { useState } from "react";
import { useTranslation } from "react-i18next";
import { useApi } from "@/api/app-api-provider";
import { useManager } from "@/app/manager-provider";
import { PageTitle } from "@/components/layout/page-title";
import { ModRow } from "@/components/mods/mod-row";
import { useModUpdates } from "@/hooks/mods/use-mod-updates";
import type { InstalledMod } from "@/models/installation/installation-model";
import { Button } from "@/shared/ui/button";
import { Dialog } from "@/shared/ui/dialog";
import { Menu, MenuItem } from "@/shared/ui/dropdown-menu";
import { EmptyState } from "@/shared/ui/empty-state";
import { Icon } from "@/shared/ui/icon";
import { runOperation } from "@/state/operations/operations-store";
import { usePreferences } from "@/state/preferences/preferences-store";
export function ModsPage() {
  const { t } = useTranslation();
  const { game, imports, busy, locked } = useManager();
  const api = useApi();
  const navigate = usePreferences((state) => state.navigate);
  const [removing, setRemoving] = useState<InstalledMod | null>(null);
  const mods = game.data?.mods ?? [];
  const updates = useModUpdates(mods);
  return (
    <div className="workspace-page">
      <PageTitle
        title={t("모드")}
        eyebrow="INSTALLED MODS"
        subtitle={t("mods.count", { count: mods.length })}
        actions={
          <>
            <Button
              size="small"
              disabled={locked || !mods.length}
              pending={updates.isFetching}
              onClick={() => {
                void updates.refetch();
              }}
            >
              <Icon name="refresh" size={16} />
              {t("mods.update.check")}
            </Button>
            {imports.download && (
              <Button
                variant="ghost"
                size="small"
                onClick={() => {
                  void imports.cancelDownload();
                }}
              >
                {t("common.cancel")}
              </Button>
            )}
            <Menu
              trigger={
                <Button variant="ghost" size="small" aria-label={t("목록 관리")}>
                  <Icon name="more" />
                  {t("목록 관리")}
                </Button>
              }
            >
              <MenuItem
                icon="refresh"
                disabled={locked}
                onSelect={() => {
                  void game.refresh();
                }}
              >
                {t("common.refresh")}
              </MenuItem>
              <MenuItem
                icon="folder"
                disabled={!game.data?.modsPath}
                onSelect={() => {
                  if (game.data?.modsPath)
                    void runOperation("common.showFolder", () =>
                      api.desktop.reveal(game.data?.modsPath ?? ""),
                    );
                }}
              >
                {t("common.showFolder")}
              </MenuItem>
            </Menu>
            <Button
              variant="primary"
              size="small"
              disabled={locked || !game.data?.detected || game.data.gameRunning}
              onClick={() => {
                void imports.choose();
              }}
            >
              <Icon name="add" />
              {t("모드 추가")}
            </Button>
          </>
        }
      />
      {updates.isError ? (
        <p className="inline-warning" role="alert">
          {t("mods.update.failed")}
        </p>
      ) : (
        updates.results && (
          <p className="workspace-footnote" role="status">
            {t("mods.update.summary", { count: updates.available })}
          </p>
        )
      )}
      {mods.length ? (
        <ul className="mod-list">
          {mods.map((mod) => (
            <ModRow
              key={mod.path}
              mod={mod}
              disabled={locked || !!game.data?.gameRunning}
              toggle={() => {
                void game.toggle(mod);
              }}
              remove={() => setRemoving(mod)}
              update={updates.results?.get(mod.path)}
              downloadUpdate={() => {
                const latest = updates.results?.get(mod.path)?.latest;
                if (latest) void imports.fromCatalog(latest, mod);
              }}
            />
          ))}
        </ul>
      ) : (
        <EmptyState title={t("mods.empty")} description={t("mods.emptyHint")}>
          <Button onClick={() => navigate("catalog")}>{t("모드 찾기")}</Button>
        </EmptyState>
      )}
      <p className="workspace-footnote">{t("mods.dropHint")}</p>
      <Dialog
        open={!!removing}
        onOpenChange={() => setRemoving(null)}
        title={t("mods.removeTitle")}
        description={t("mods.removeHint", { name: removing?.name })}
        locked={busy}
      >
        <div className="dialog-actions">
          <Button disabled={busy} onClick={() => setRemoving(null)}>
            {t("common.cancel")}
          </Button>
          <Button
            variant="danger"
            pending={busy}
            onClick={() => {
              if (removing) void game.remove(removing).then(() => setRemoving(null));
            }}
          >
            {t("common.remove")}
          </Button>
        </div>
      </Dialog>
    </div>
  );
}
