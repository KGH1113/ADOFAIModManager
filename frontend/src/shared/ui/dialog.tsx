import { Dialog as Primitive } from "radix-ui";
import { type ReactNode, useRef } from "react";
import { useTranslation } from "react-i18next";
import { Icon } from "./icon";
export function Dialog({
  open,
  onOpenChange,
  title,
  description,
  children,
  locked = false,
  autoFocusTitle = false,
  className = "",
}: {
  open: boolean;
  onOpenChange(open: boolean): void;
  title: string;
  description?: string;
  children: ReactNode;
  locked?: boolean;
  autoFocusTitle?: boolean;
  className?: string;
}) {
  const { t } = useTranslation();
  const titleRef = useRef<HTMLHeadingElement>(null);
  return (
    <Primitive.Root
      open={open}
      onOpenChange={(value) => {
        if (!locked) onOpenChange(value);
      }}
    >
      <Primitive.Portal>
        <Primitive.Overlay className="dialog-overlay" />
        <Primitive.Content
          className={`dialog-content ${className}`}
          onOpenAutoFocus={(event) => {
            if (autoFocusTitle) {
              event.preventDefault();
              titleRef.current?.focus();
            }
          }}
        >
          <Primitive.Title
            ref={titleRef}
            tabIndex={autoFocusTitle ? -1 : undefined}
            className="dialog-title"
          >
            {title}
          </Primitive.Title>
          {description ? (
            <Primitive.Description className="dialog-description">
              {description}
            </Primitive.Description>
          ) : (
            <Primitive.Description className="sr-only">{title}</Primitive.Description>
          )}
          {children}
          <Primitive.Close
            disabled={locked}
            className="dialog-close button button-ghost button-icon"
            aria-label={t("common.close")}
          >
            <Icon name="close" />
          </Primitive.Close>
        </Primitive.Content>
      </Primitive.Portal>
    </Primitive.Root>
  );
}
