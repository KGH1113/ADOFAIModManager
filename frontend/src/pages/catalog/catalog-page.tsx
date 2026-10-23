import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { useManager } from "@/app/manager-provider";
import { CatalogCard } from "@/components/catalog/catalog-card";
import { CatalogDetail } from "@/components/catalog/catalog-detail";
import { PageTitle } from "@/components/layout/page-title";
import { useCatalog } from "@/hooks/catalog/use-catalog";
import type { CatalogSort } from "@/models/catalog/catalog-model";
import { Button } from "@/shared/ui/button";
import { EmptyState } from "@/shared/ui/empty-state";
import { Icon } from "@/shared/ui/icon";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/shared/ui/select";

export function CatalogPage() {
  const { t } = useTranslation();
  const catalog = useCatalog();
  const { game, imports, locked } = useManager();
  const listRef = useRef<HTMLDivElement>(null);
  const scrollRef = useRef<HTMLDivElement>(null);
  const sentinelRef = useRef<HTMLDivElement>(null);
  const [renderWindow, setRenderWindow] = useState({ items: catalog.items, count: 18 });
  const visibleCount = renderWindow.items === catalog.items ? renderWindow.count : 18;
  const hasMore = visibleCount < catalog.items.length;
  useEffect(() => {
    // Filters and refreshes start a fresh window; detail navigation preserves it.
    if (catalog.items && scrollRef.current) scrollRef.current.scrollTop = 0;
  }, [catalog.items]);
  useEffect(() => {
    const root = scrollRef.current;
    const sentinel = sentinelRef.current;
    if (!root || !sentinel || !hasMore || catalog.selected) return;
    const observer = new IntersectionObserver(
      (entries) => {
        if (!entries.some((entry) => entry.isIntersecting)) return;
        observer.disconnect();
        setRenderWindow({ items: catalog.items, count: visibleCount + 18 });
      },
      { root, rootMargin: "0px 0px 400px 0px" },
    );
    observer.observe(sentinel);
    return () => observer.disconnect();
  }, [catalog.items, catalog.selected, hasMore, visibleCount]);
  const canDownload = !locked && !!game.data?.detected && !game.data.gameRunning;
  const back = () => {
    const id = catalog.selected?.id;
    catalog.select(null);
    requestAnimationFrame(() => {
      const buttons = listRef.current?.querySelectorAll<HTMLButtonElement>("[data-mod-id]");
      Array.from(buttons ?? [])
        .find((button) => button.dataset.modId === id)
        ?.focus();
    });
  };

  return (
    <div className="workspace-page catalog-page">
      <div className="catalog-browser" hidden={!!catalog.selected} ref={listRef}>
        <PageTitle
          title={t("모드 찾기")}
          subtitle={t("manager.discover.subtitle")}
          eyebrow="TUF MODS"
          actions={
            <Button
              size="small"
              disabled={catalog.isFetching}
              onClick={() => void catalog.refetch()}
            >
              <Icon name="refresh" size={16} className={catalog.isFetching ? "animate-spin" : ""} />
              {t("목록 새로 고침")}
            </Button>
          }
        />
        <div className="catalog-scroll" ref={scrollRef}>
          <div className="catalog-toolbar">
            <label className="search-field">
              <Icon name="search" size={18} />
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
            <div className="catalog-select">
              <label htmlFor="catalog-sort">{t("catalog.sort")}</label>
              <Select
                value={catalog.sort}
                onValueChange={(value) => catalog.setSort(value as CatalogSort)}
              >
                <SelectTrigger id="catalog-sort">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {(["newest", "oldest", "downloads", "likes", "name"] as const).map((sort) => (
                    <SelectItem key={sort} value={sort}>
                      {t(`catalog.sort.${sort}`)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="catalog-select">
              <label htmlFor="catalog-tags">{t("catalog.tags")}</label>
              <Select
                value={catalog.tagId === null ? "all" : String(catalog.tagId)}
                onValueChange={(value) => catalog.setTagId(value === "all" ? null : Number(value))}
              >
                <SelectTrigger id="catalog-tags">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">{t("catalog.tags.all")}</SelectItem>
                  {catalog.tags.map((tag) => (
                    <SelectItem key={tag.id} value={String(tag.id)}>
                      {tag.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>
          {!catalog.isPending && !catalog.isError && (
            <div className="catalog-results" role="status">
              <span>{t("catalog.results", { count: catalog.items.length })}</span>
              {(catalog.search || catalog.tagId !== null) && (
                <button
                  type="button"
                  onClick={() => {
                    catalog.setSearch("");
                    catalog.setTagId(null);
                  }}
                >
                  {t("catalog.clearFilters")}
                </button>
              )}
            </div>
          )}
          {catalog.isPending ? (
            <div
              className="catalog-grid"
              role="status"
              aria-busy="true"
              aria-label={t("catalog.loading")}
            >
              {[0, 1, 2, 3, 4, 5].map((key) => (
                <div key={key} className="catalog-skeleton" />
              ))}
            </div>
          ) : catalog.isError ? (
            <EmptyState icon="alert" title={t("catalog.loadError")}>
              <Button onClick={() => void catalog.refetch()}>{t("operation.retry")}</Button>
            </EmptyState>
          ) : !catalog.items.length ? (
            <EmptyState icon="search" title={t("catalog.empty")} />
          ) : (
            <div className="catalog-grid">
              {catalog.items.slice(0, visibleCount).map((mod) => (
                <CatalogCard
                  key={mod.id}
                  mod={mod}
                  open={() => catalog.select(mod.id)}
                  download={() => void imports.fromCatalog(mod)}
                  canDownload={canDownload}
                  downloading={imports.download?.id === mod.id}
                />
              ))}
            </div>
          )}
          {hasMore && (
            <div className="catalog-scroll-sentinel" ref={sentinelRef} aria-hidden="true" />
          )}
        </div>
      </div>
      {catalog.selected && (
        <CatalogDetail
          key={catalog.selected.id}
          mod={catalog.selected}
          detail={catalog.detail}
          back={back}
        />
      )}
      {imports.download && (
        <div className="catalog-download-status" role="status">
          <span>
            {t("catalog.downloading", {
              name:
                catalog.data?.find((mod) => mod.id === imports.download?.id)?.name ??
                imports.download.id,
            })}
          </span>
          <Button size="small" variant="ghost" onClick={() => void imports.cancelDownload()}>
            {t("common.cancel")}
          </Button>
        </div>
      )}
    </div>
  );
}
