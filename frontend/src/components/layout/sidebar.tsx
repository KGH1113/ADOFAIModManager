import { motion } from "motion/react";
import { useTranslation } from "react-i18next";
import { Button } from "@/shared/ui/button";
import { Icon, type IconName } from "@/shared/ui/icon";
import type { Page } from "@/state/preferences/preferences-store";
export const destinations: { page: Page; label: string; icon: IconName }[] = [
  { page: "install", label: "설치", icon: "package" },
  { page: "catalog", label: "모드 찾기", icon: "search" },
  { page: "mods", label: "모드", icon: "puzzle" },
  { page: "logs", label: "로그", icon: "file" },
];
export function Sidebar({
  page,
  navigate,
  settings,
}: {
  page: Page;
  navigate(page: Page): void;
  settings(): void;
}) {
  const { t } = useTranslation();
  return (
    <aside className="sidebar">
      <nav aria-label="Main">
        {destinations.map((item) => (
          <button
            key={item.page}
            type="button"
            className={`nav-item ${page === item.page ? "selected" : ""}`}
            aria-current={page === item.page ? "page" : undefined}
            onClick={() => navigate(item.page)}
            data-testid={`nav-${item.page}`}
          >
            {page === item.page && (
              <motion.span
                layoutId="sidebar-selection"
                className="nav-selection"
                transition={{ type: "spring", bounce: 0, duration: 0.35 }}
              />
            )}
            <Icon name={item.icon} size={19} />
            <span>{t(item.label)}</span>
          </button>
        ))}
      </nav>
      <div className="sidebar-bottom">
        <Button
          size="icon"
          variant="ghost"
          aria-label={t("settings.title")}
          title={t("settings.title")}
          data-testid="app-settings"
          onClick={settings}
        >
          <Icon name="settings" />
        </Button>
      </div>
    </aside>
  );
}
