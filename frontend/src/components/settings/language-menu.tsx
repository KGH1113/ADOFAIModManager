import { useTranslation } from "react-i18next";
import { Button } from "@/shared/ui/button";
import { Menu, MenuItem } from "@/shared/ui/dropdown-menu";
import { Icon } from "@/shared/ui/icon";
import { type Language, usePreferences } from "@/state/preferences/preferences-store";
export function LanguageMenu() {
  const { t } = useTranslation();
  const language = usePreferences((state) => state.language);
  const select = usePreferences((state) => state.setLanguage);
  const items: { key: Language; name: string }[] = [
    { key: "system", name: t("settings.system") },
    { key: "ko", name: "한국어" },
    { key: "en", name: "English" },
    { key: "zh", name: "简体中文" },
  ];
  return (
    <Menu
      trigger={
        <Button variant="ghost" size="icon" aria-label={t("settings.language")}>
          <Icon name="globe" size={16} />
        </Button>
      }
    >
      {items.map((item) => (
        <MenuItem
          key={item.key}
          icon={language === item.key ? "check" : undefined}
          onSelect={() => select(item.key)}
        >
          {item.name}
        </MenuItem>
      ))}
    </Menu>
  );
}
