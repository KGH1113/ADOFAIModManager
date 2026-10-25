import type { UseQueryResult } from "@tanstack/react-query";
import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { useApi } from "@/api/app-api-provider";
import { useManager } from "@/app/manager-provider";
import { CatalogTags, catalogDate, ModArtwork } from "@/components/catalog/catalog-card";
import { ModDescription } from "@/components/catalog/mod-description";
import type { CatalogMod, CatalogModDetail } from "@/models/catalog/catalog-model";
import type { InstalledMod } from "@/models/installation/installation-model";
import { sameModVersion } from "@/models/mods/mod-updates";
import { Button } from "@/shared/ui/button";
import { Icon } from "@/shared/ui/icon";
import { ScrollArea } from "@/shared/ui/scroll-area";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/shared/ui/select";
import { runOperation } from "@/state/operations/operations-store";

export function CatalogDetail({
  mod: summary,
  detail,
  back,
  installed,
}: {
  mod: CatalogMod;
  detail: UseQueryResult<CatalogModDetail, Error>;
  back(): void;
  installed?: InstalledMod;
}) {
  const { t, i18n } = useTranslation();
  const api = useApi();
  const { game, imports, locked } = useManager();
  const heading = useRef<HTMLHeadingElement>(null);
  const [versionId, setVersionId] = useState<string | null>(null);
  useEffect(() => {
    heading.current?.focus();
  }, []);
  const mod = detail.data ?? summary;
  const versions = detail.data?.versions ?? [];
  const selected =
    versions.find((version) => version.id === versionId) ??
    versions.find((version) => version.version === detail.data?.latestVersion) ??
    versions[0];
  const current = !!installed && !!selected && sameModVersion(installed.version, selected.version);
  const openUrl = (url: string) => {
    void runOperation("catalog.website", () => api.desktop.openUrl(url));
  };
  const blockedReason = !game.data?.detected
    ? t("error.choose_game")
    : game.data.gameRunning
      ? t("error.game_running")
      : null;
  return (
    <section className="catalog-detail-page" aria-label={mod.name}>
      <div className="catalog-detail-nav">
        <Button size="small" variant="ghost" onClick={back}>
          <Icon name="left" size={16} />
          {t("catalog.back")}
        </Button>
      </div>
      <div className="catalog-detail-body">
        <header className="catalog-detail-heading">
          <ModArtwork mod={mod} />
          <div>
            <div className="catalog-title-line">
              <h1 tabIndex={-1} ref={heading}>
                {mod.name}
              </h1>
              {mod.pinned && <span className="catalog-pinned">{t("catalog.pinned")}</span>}
            </div>
            <p>{mod.author || "—"}</p>
            {installed && (
              <p className="catalog-installed">
                {t("catalog.installedVersion", { version: installed.version })}
              </p>
            )}
          </div>
        </header>
        <div className="catalog-detail-stats">
          <span>
            <Icon name="download" size={16} />
            {t("catalog.downloads", { count: mod.downloadCount })}
          </span>
          <span>
            <Icon name="heart" size={16} />
            {t("catalog.likes", { count: mod.likes })}
          </span>
          <span>
            <Icon name="calendar" size={16} />
            {catalogDate(mod.uploadedAt, i18n.language)}
          </span>
        </div>
        <CatalogTags mod={mod} />
        {mod.deprecatedAfter && (
          <p className="inline-warning">
            <Icon name="alert" size={16} />
            {t("catalog.deprecated", { version: mod.deprecatedAfter })}
          </p>
        )}
        <div className="catalog-detail-columns">
          <section
            className="catalog-description-section"
            aria-labelledby="catalog-description-title"
          >
            <h2 id="catalog-description-title">{t("catalog.description")}</h2>
            <ScrollArea
              className="catalog-description-scroll"
              viewportLabel={t("catalog.description")}
            >
              <div className="catalog-reading-content">
                <ModDescription
                  text={mod.description || t("catalog.noDescription")}
                  openUrl={openUrl}
                />
                {selected && (
                  <div className="catalog-release-notes">
                    <h3>
                      {t("catalog.releaseNotes")} · {selected.version}
                    </h3>
                    <ModDescription
                      text={selected.notes || t("catalog.noReleaseNotes")}
                      openUrl={openUrl}
                    />
                  </div>
                )}
              </div>
            </ScrollArea>
            {mod.projectUrl && (
              <Button
                size="small"
                variant="ghost"
                onClick={() => openUrl(mod.projectUrl as string)}
              >
                <Icon name="external" size={16} />
                {t("catalog.website")}
              </Button>
            )}
          </section>
          <section className="catalog-version-panel" aria-labelledby="catalog-versions-title">
            <h2 id="catalog-versions-title">{t("catalog.versions")}</h2>
            <p className="catalog-version-hint">{t("catalog.versionsHint")}</p>
            {detail.isPending ? (
              <p className="catalog-version-status" role="status">
                {t("catalog.versionsLoading")}
              </p>
            ) : detail.isError ? (
              <div className="catalog-version-status" role="alert">
                <p>{t("catalog.versionsError")}</p>
                <Button size="small" onClick={() => void detail.refetch()}>
                  {t("operation.retry")}
                </Button>
              </div>
            ) : !selected ? (
              <p className="catalog-version-status">{t("catalog.versionsEmpty")}</p>
            ) : (
              <>
                <div className="catalog-select">
                  <label htmlFor="catalog-version">{t("catalog.version")}</label>
                  <Select value={selected.id} onValueChange={setVersionId}>
                    <SelectTrigger id="catalog-version">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {versions.map((version) => (
                        <SelectItem key={version.id} value={version.id}>
                          {version.version}
                          {version.version === detail.data?.latestVersion
                            ? ` · ${t("catalog.latest")}`
                            : ""}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                {selected.releasedAt > 0 && (
                  <p className="catalog-release-date">
                    {t("catalog.released", {
                      date: catalogDate(selected.releasedAt, i18n.language),
                    })}
                  </p>
                )}
                <Button
                  variant={current ? "secondary" : "primary"}
                  className={current ? "catalog-install-complete" : undefined}
                  disabled={locked || !!blockedReason || !selected.url || current}
                  pending={imports.download?.id === mod.id}
                  onClick={() =>
                    void imports.fromCatalog({
                      ...mod,
                      version: selected.version,
                      url: selected.url,
                    })
                  }
                >
                  <Icon name={current ? "check" : "download"} size={18} />
                  {t(current ? "catalog.installed" : "catalog.download")}
                </Button>
                {blockedReason && <p className="catalog-version-hint">{blockedReason}</p>}
                {selected.projectUrl && (
                  <Button
                    variant="ghost"
                    size="small"
                    onClick={() => openUrl(selected.projectUrl as string)}
                  >
                    <Icon name="external" size={16} />
                    {t("catalog.releaseSource")}
                  </Button>
                )}
              </>
            )}
          </section>
        </div>
      </div>
    </section>
  );
}
