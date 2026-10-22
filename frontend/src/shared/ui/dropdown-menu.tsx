import { DropdownMenu } from "radix-ui";
import type { ReactNode } from "react";
import { Icon, type IconName } from "./icon";
export function Menu({
  trigger,
  children,
  align = "end",
}: {
  trigger: ReactNode;
  children: ReactNode;
  align?: "start" | "end";
}) {
  return (
    <DropdownMenu.Root>
      <DropdownMenu.Trigger asChild>{trigger}</DropdownMenu.Trigger>
      <DropdownMenu.Portal>
        <DropdownMenu.Content sideOffset={8} align={align} className="menu-content">
          {children}
        </DropdownMenu.Content>
      </DropdownMenu.Portal>
    </DropdownMenu.Root>
  );
}
export function MenuItem({
  icon,
  children,
  onSelect,
  disabled = false,
  danger = false,
}: {
  icon?: IconName;
  children: ReactNode;
  onSelect(): void;
  disabled?: boolean;
  danger?: boolean;
}) {
  return (
    <DropdownMenu.Item
      className={`menu-item ${danger ? "text-fire" : ""}`}
      disabled={disabled}
      onSelect={onSelect}
    >
      {icon && <Icon name={icon} size={16} />}
      {children}
    </DropdownMenu.Item>
  );
}
export function MenuSeparator() {
  return <DropdownMenu.Separator className="menu-separator" />;
}
