import { useTranslation } from "react-i18next";
import { useApi } from "@/api/app-api-provider";
import { useManager } from "@/app/manager-provider";
import { PageTitle } from "@/components/layout/page-title";
import { useLogs } from "@/hooks/logs/use-logs";
import { Button } from "@/shared/ui/button";
import { EmptyState } from "@/shared/ui/empty-state";
import { Icon } from "@/shared/ui/icon";
import { runOperation } from "@/state/operations/operations-store";
export function LogsPage() {
  const { t } = useTranslation();
  const { game } = useManager();
  const logs = useLogs(game.data?.gamePath ?? null);
  const api = useApi();
  return (
    <div className="workspace-page">
      <PageTitle
        title={t("로그")}
        eyebrow="GAME LOGS"
        subtitle={t(logs.source === "game" ? "게임 실행 기록" : "Unity Mod Manager")}
        actions={
          <fieldset className="segmented" aria-label={t("로그 종류")}>
            <button
              type="button"
              aria-pressed={logs.source === "game"}
              onClick={() => logs.setSource("game")}
            >
              {t("게임 로그")}
            </button>
            <button
              type="button"
              aria-pressed={logs.source === "umm"}
              onClick={() => logs.setSource("umm")}
            >
              {t("UMM 로그")}
            </button>
          </fieldset>
        }
      />
      <div className="logs-workspace">
        <div className="logs-actions">
          <span>{t("logs.lines", { count: logs.source === "game" ? 30 : 400 })}</span>
          <div>
            <Button
              size="small"
              disabled={logs.isFetching || !game.data?.detected}
              onClick={() => {
                void logs.refetch();
              }}
            >
              <Icon name="refresh" size={16} />
              {t("common.refresh")}
            </Button>
            <Button
              size="small"
              disabled={!logs.data?.exists}
              onClick={() => {
                if (logs.data?.path)
                  void runOperation("common.showFolder", () =>
                    api.desktop.reveal(logs.data?.path ?? ""),
                  );
              }}
            >
              {t("common.showFolder")}
            </Button>
          </div>
        </div>
        {logs.data?.exists ? (
          // biome-ignore lint/a11y/noNoninteractiveTabindex: Keyboard users need to scroll long log output.
          <pre className="log-output" tabIndex={0}>
            {logs.data.text}
          </pre>
        ) : (
          <EmptyState
            icon="file"
            title={t(
              logs.isError ? "operation.failed" : logs.isFetching ? "common.loading" : "logs.empty",
            )}
            description={t("logs.gameHint")}
          />
        )}
      </div>
    </div>
  );
}
