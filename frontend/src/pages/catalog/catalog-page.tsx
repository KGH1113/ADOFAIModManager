import { useTranslation } from "react-i18next";
import { useApi } from "@/api/app-api-provider";
import { useManager } from "@/app/manager-provider";
import { ModDescription } from "@/components/catalog/mod-description";
import { PageTitle } from "@/components/layout/page-title";
import { useCatalog } from "@/hooks/catalog/use-catalog";
import { Button } from "@/shared/ui/button";
import { EmptyState } from "@/shared/ui/empty-state";
import { Icon } from "@/shared/ui/icon";
import { runOperation } from "@/state/operations/operations-store";
export function CatalogPage() {
  const { t, i18n } = useTranslation();
  const catalog = useCatalog();
  const { game, imports, locked } = useManager();
  const api = useApi();
  const mod = catalog.selected;
  const openUrl = (url: string) => {
    void runOperation("catalog.website", () => api.desktop.openUrl(url));
  };
  return (
    <div className="workspace-page">
      <PageTitle
        title={t("모드 찾기")}
        subtitle={t("manager.discover.subtitle")}
        eyebrow="MOD CATALOG"
        actions={
          <Button
            size="small"
            disabled={catalog.isFetching}
            onClick={() => {
              void catalog.refetch();
            }}
          >
            <Icon name="refresh" size={16} className={catalog.isFetching ? "animate-spin" : ""} />
            {t("목록 새로 고침")}
          </Button>
        }
      />
      <div className="catalog-workspace">
        <aside className="catalog-list-panel">
          <label className="search-field">
            <Icon name="search" size={16} />
            <input
              value={catalog.search}
              onChange={(event) => catalog.setSearch(event.target.value)}
              placeholder={t("catalog.search")}
              aria-label={t("catalog.search")}
            />
            {catalog.search && (
              <button
                type="button"
                onClick={() => catalog.setSearch("")}
                aria-label={t("manager.clearSearch")}
              >
                <Icon name="close" size={14} />
              </button>
            )}
          </label>
          <nav className="catalog-list" aria-label={t("manager.discover.title")}>
            {catalog.items.map((item) => (
              <button
                type="button"
                key={item.id}
                onClick={() => catalog.select(item.id)}
                aria-pressed={mod?.id === item.id}
                className={`catalog-list-item ${mod?.id === item.id ? "selected" : ""}`}
              >
                <strong>{item.name}</strong>
                <span>
                  {item.version || "—"} · {item.author}
                </span>
              </button>
            ))}
          </nav>
        </aside>
        <section className="catalog-detail" aria-label={t("catalog.description")}>
          {catalog.isPending ? (
            <EmptyState icon="loading" title={t("catalog.loading")} />
          ) : catalog.isError ? (
            <EmptyState icon="alert" title={t("operation.failed")}>
              <Button
                onClick={() => {
                  void catalog.refetch();
                }}
              >
                {t("operation.retry")}
              </Button>
            </EmptyState>
          ) : !mod ? (
            <EmptyState icon="search" title={t("catalog.empty")} />
          ) : (
            <>
              <div className="catalog-detail-scroll" key={mod.id}>
                <h2>{mod.name}</h2>
                <div className="mod-metadata">
                  <span>{mod.version}</span>
                  <span>{mod.author}</span>
                  {mod.uploadedAt > 0 && (
                    <time>
                      {new Date(
                        mod.uploadedAt < 1e12 ? mod.uploadedAt * 1000 : mod.uploadedAt,
                      ).toLocaleDateString(i18n.language)}
                    </time>
                  )}
                </div>
                <ModDescription text={mod.description} openUrl={openUrl} />
              </div>
              <footer className="catalog-actions">
                <Button
                  variant="primary"
                  size="small"
                  disabled={locked || !mod.url || !game.data?.detected || game.data.gameRunning}
                  pending={imports.download?.id === mod.id}
                  onClick={() => {
                    void imports.fromCatalog(mod);
                  }}
                >
                  <Icon name="download" size={17} />
                  {t("catalog.download")}
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
              </footer>
            </>
          )}
        </section>
      </div>
    </div>
  );
}
