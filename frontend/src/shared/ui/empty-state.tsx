import type { ReactNode } from "react";
import { Icon, type IconName } from "./icon";
export function EmptyState({
  icon = "puzzle",
  title,
  description,
  children,
}: {
  icon?: IconName;
  title: string;
  description?: string;
  children?: ReactNode;
}) {
  return (
    <div className="empty-state">
      <Icon name={icon} size={32} />
      <h2>{title}</h2>
      {description && <p>{description}</p>}
      {children}
    </div>
  );
}
