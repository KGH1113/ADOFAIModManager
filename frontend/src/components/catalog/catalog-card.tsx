import { useState } from "react";
import { useTranslation } from "react-i18next";
import type { CatalogMod } from "@/models/catalog/catalog-model";
import { Button } from "@/shared/ui/button";
import { Icon } from "@/shared/ui/icon";

export function catalogDate(timestamp: number, language: string) {
  return timestamp > 0
    ? new Date(timestamp < 1e12 ? timestamp * 1000 : timestamp).toLocaleDateString(language)
    : "—";
}

export function ModArtwork({ mod }: { mod: CatalogMod }) {
  const [failed, setFailed] = useState(false);
  return mod.imageUrl && !failed ? (
    <img
      className="catalog-artwork"
      src={mod.imageUrl}
      alt=""
      loading="lazy"
      referrerPolicy="no-referrer"
      onError={() => setFailed(true)}
    />
  ) : (
    <span className="catalog-artwork catalog-artwork-fallback" aria-hidden="true">
      {mod.name.slice(0, 1)}
    </span>
  );
}

export function CatalogTags({ mod }: { mod: CatalogMod }) {
  return mod.tags.length > 0 ? (
    <div className="catalog-tags">
      {mod.tags.map((tag) => (
        <span key={tag.id}>{tag.name}</span>
      ))}
    </div>
  ) : null;
}

export function CatalogCard({
  mod,
  open,
  download,
  canDownload,
  downloading,
}: {
  mod: CatalogMod;
  open(): void;
  download(): void;
  canDownload: boolean;
  downloading: boolean;
}) {
  const { t, i18n } = useTranslation();
  const description = mod.description
    .replace(/!\[([^\]]*)\]\([^)]*\)/g, "$1")
    .replace(/\[([^\]]+)\]\([^)]*\)/g, "$1")
    .replace(/<[^>]+>/g, "")
    .replace(/[#*_`~]/g, "")
    .replace(/\s+/g, " ")
    .trim();
  return (
    <article className="catalog-card" aria-label={mod.name}>
      <header className="catalog-card-heading">
        <ModArtwork mod={mod} />
        <div className="catalog-card-name">
          <h2>
            <button type="button" className="catalog-card-link" data-mod-id={mod.id} onClick={open}>
              {mod.name}
            </button>
          </h2>
          {mod.pinned && <span className="catalog-pinned">{t("catalog.pinned")}</span>}
        </div>
        <span className="catalog-likes">
          <Icon name="heart" size={16} />
          <span aria-hidden="true">{mod.likes.toLocaleString(i18n.language)}</span>
          <span className="sr-only">{t("catalog.likes", { count: mod.likes })}</span>
        </span>
      </header>
      <p className="catalog-summary">{description || t("catalog.noDescription")}</p>
      <dl className="catalog-card-meta">
        <div>
          <dt>
            <Icon name="user" size={16} />
            <span className="sr-only">{t("catalog.author")}</span>
          </dt>
          <dd>{mod.author || "—"}</dd>
        </div>
        <div>
          <dt>
            <Icon name="package" size={16} />
            <span className="sr-only">{t("catalog.version")}</span>
          </dt>
          <dd>
            {t("catalog.version")}: {mod.version || "—"}
          </dd>
        </div>
        <div>
          <dt>
            <Icon name="calendar" size={16} />
            <span className="sr-only">{t("catalog.updated")}</span>
          </dt>
          <dd>
            <time>{catalogDate(mod.uploadedAt, i18n.language)}</time>
          </dd>
        </div>
      </dl>
      <CatalogTags mod={mod} />
      <p className="catalog-download-count">
        {t("catalog.downloads", { count: mod.downloadCount })}
      </p>
      {mod.deprecatedAfter && (
        <p className="catalog-deprecation">
          <Icon name="alert" size={16} />
          {t("catalog.deprecated", { version: mod.deprecatedAfter })}
        </p>
      )}
      <footer className="catalog-card-actions">
        <Button
          variant="primary"
          size="small"
          disabled={!canDownload || !mod.url}
          pending={downloading}
          onClick={download}
        >
          <Icon name="download" size={16} />
          {t("catalog.download")}
        </Button>
        <Button size="small" onClick={open}>
          {t("operation.details")}
          <Icon name="right" size={16} />
        </Button>
      </footer>
    </article>
  );
}
