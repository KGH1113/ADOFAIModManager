import { useState } from "react";
import { useTranslation } from "react-i18next";
import { useManager } from "@/app/manager-provider";
import { GameSetupCard } from "@/components/installation/game-setup-card";
import { PageTitle } from "@/components/layout/page-title";
import { Button } from "@/shared/ui/button";
import { Dialog } from "@/shared/ui/dialog";
import { Menu, MenuItem } from "@/shared/ui/dropdown-menu";
import { Icon } from "@/shared/ui/icon";
import { useOperations } from "@/state/operations/operations-store";
export function InstallationWorkspace() {
  const { t } = useTranslation();
  const { game, busy, locked } = useManager();
  const label = useOperations((state) => state.label);
  const [confirmation, setConfirmation] = useState<"remove" | "restore" | null>(null);
  return (
    <div className="scroll-page installation-page">
      <PageTitle
        title={t("manager.install.title")}
        subtitle={t("setup.pageHint")}
        eyebrow="SETUP"
      />
      <div className="page-body">
        <GameSetupCard
          loaderActions={
            <div className="setup-actions">
              <Button
                variant="primary"
                disabled={locked || !game.data?.detected || game.data.gameRunning}
                pending={busy}
                onClick={() => {
                  void game.execute(game.data?.installed ? "repair" : "install");
                }}
              >
                <Icon name={game.data?.installed ? "refresh" : "download"} />
                {label
                  ? t(label)
                  : t(game.data?.installed ? "다시 설치" : "onboarding.umm.install")}
              </Button>
              <Menu
                trigger={
                  <Button variant="ghost" size="small" disabled={locked || !game.data?.detected}>
                    <Icon name="more" size={16} />
                    {t("더 보기")}
                  </Button>
                }
              >
                <MenuItem
                  icon="settings"
                  onSelect={() => {
                    void game.execute("repair");
                  }}
                  disabled={!!game.data?.gameRunning}
                >
                  {t("설치 문제 해결")}
                </MenuItem>
                <MenuItem
                  icon="trash"
                  onSelect={() => setConfirmation("remove")}
                  disabled={!game.data?.installed || game.data.gameRunning}
                >
                  {t("UMM 제거")}
                </MenuItem>
                <MenuItem
                  icon="refresh"
                  onSelect={() => setConfirmation("restore")}
                  disabled={!game.data?.hasBackup || game.data.gameRunning}
                >
                  {t("원본 복원")}
                </MenuItem>
              </Menu>
            </div>
          }
          installation={game.data}
          busy={locked}
          loading={game.isFetching}
          choose={() => {
            void game.chooseGame();
          }}
          refresh={() => {
            void game.refresh();
          }}
        />
        {game.isError && (
          <p className="inline-warning" role="alert">
            {t("operation.failed")}{" "}
            <Button
              variant="ghost"
              size="small"
              onClick={() => {
                void game.refresh();
              }}
            >
              {t("operation.retry")}
            </Button>
          </p>
        )}
      </div>
      <Dialog
        open={!!confirmation}
        onOpenChange={() => setConfirmation(null)}
        title={t(confirmation === "restore" ? "install.restoreTitle" : "install.removeTitle")}
        description={t("install.confirmHint")}
        locked={busy}
      >
        <div className="dialog-actions">
          <Button onClick={() => setConfirmation(null)} disabled={busy}>
            {t("common.cancel")}
          </Button>
          <Button
            variant="danger"
            pending={busy}
            onClick={() => {
              if (confirmation) void game.execute(confirmation).then(() => setConfirmation(null));
            }}
          >
            {t(confirmation === "restore" ? "원본 복원" : "UMM 제거")}
          </Button>
        </div>
      </Dialog>
    </div>
  );
}
