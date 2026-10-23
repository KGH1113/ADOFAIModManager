import { Select as SelectPrimitive } from "radix-ui";
import type { ComponentProps } from "react";
import { cn } from "@/shared/lib/cn";
import { Icon } from "@/shared/ui/icon";

// shadcn Select (radix-maia), adapted to the app's control tokens and icons.
export const Select = SelectPrimitive.Root;
export const SelectValue = SelectPrimitive.Value;
export const SelectGroup = SelectPrimitive.Group;

export function SelectTrigger({
  className,
  children,
  ...props
}: ComponentProps<typeof SelectPrimitive.Trigger>) {
  return (
    <SelectPrimitive.Trigger
      data-slot="select-trigger"
      className={cn("select-trigger", className)}
      {...props}
    >
      {children}
      <SelectPrimitive.Icon asChild>
        <Icon name="down" size={16} />
      </SelectPrimitive.Icon>
    </SelectPrimitive.Trigger>
  );
}

export function SelectContent({
  className,
  children,
  position = "popper",
  sideOffset = 6,
  ...props
}: ComponentProps<typeof SelectPrimitive.Content>) {
  return (
    <SelectPrimitive.Portal>
      <SelectPrimitive.Content
        data-slot="select-content"
        className={cn("select-content", className)}
        position={position}
        sideOffset={sideOffset}
        {...props}
      >
        <SelectPrimitive.ScrollUpButton className="select-scroll-button">
          <Icon name="down" size={16} className="rotate-180" />
        </SelectPrimitive.ScrollUpButton>
        <SelectPrimitive.Viewport className="select-viewport">{children}</SelectPrimitive.Viewport>
        <SelectPrimitive.ScrollDownButton className="select-scroll-button">
          <Icon name="down" size={16} />
        </SelectPrimitive.ScrollDownButton>
      </SelectPrimitive.Content>
    </SelectPrimitive.Portal>
  );
}

export function SelectItem({
  className,
  children,
  ...props
}: ComponentProps<typeof SelectPrimitive.Item>) {
  return (
    <SelectPrimitive.Item
      data-slot="select-item"
      className={cn("select-item", className)}
      {...props}
    >
      <SelectPrimitive.ItemText>{children}</SelectPrimitive.ItemText>
      <SelectPrimitive.ItemIndicator className="select-indicator">
        <Icon name="check" size={16} />
      </SelectPrimitive.ItemIndicator>
    </SelectPrimitive.Item>
  );
}
