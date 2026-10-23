import { Popover as PopoverPrimitive } from "radix-ui";
import type { ComponentProps } from "react";
import { cn } from "@/shared/lib/cn";

export const Popover = PopoverPrimitive.Root;
export const PopoverTrigger = PopoverPrimitive.Trigger;
export function PopoverContent({
  className,
  align = "start",
  sideOffset = 10,
  ...props
}: ComponentProps<typeof PopoverPrimitive.Content>) {
  return (
    <PopoverPrimitive.Portal>
      <PopoverPrimitive.Content
        data-slot="popover-content"
        align={align}
        sideOffset={sideOffset}
        collisionPadding={16}
        className={cn("app-popover", className)}
        {...props}
      />
    </PopoverPrimitive.Portal>
  );
}
