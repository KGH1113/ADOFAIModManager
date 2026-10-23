import { ScrollArea as ScrollAreaPrimitive } from "radix-ui";
import type { ComponentProps } from "react";
import { cn } from "@/shared/lib/cn";

// shadcn ScrollArea (radix-maia), with a keyboard-accessible reading viewport.
export function ScrollArea({
  className,
  children,
  viewportLabel,
  ...props
}: ComponentProps<typeof ScrollAreaPrimitive.Root> & { viewportLabel: string }) {
  return (
    <ScrollAreaPrimitive.Root
      data-slot="scroll-area"
      className={cn("relative overflow-hidden", className)}
      {...props}
    >
      <ScrollAreaPrimitive.Viewport
        data-slot="scroll-area-viewport"
        className="size-full rounded-[inherit] outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring"
        tabIndex={0}
        role="region"
        aria-label={viewportLabel}
      >
        {children}
      </ScrollAreaPrimitive.Viewport>
      <ScrollBar />
      <ScrollAreaPrimitive.Corner />
    </ScrollAreaPrimitive.Root>
  );
}

export function ScrollBar({
  className,
  orientation = "vertical",
  ...props
}: ComponentProps<typeof ScrollAreaPrimitive.ScrollAreaScrollbar>) {
  return (
    <ScrollAreaPrimitive.ScrollAreaScrollbar
      data-slot="scroll-area-scrollbar"
      orientation={orientation}
      className={cn(
        "flex touch-none select-none p-px",
        orientation === "vertical"
          ? "h-full w-2 border-l border-l-transparent"
          : "h-2 flex-col border-t border-t-transparent",
        className,
      )}
      {...props}
    >
      <ScrollAreaPrimitive.ScrollAreaThumb className="relative flex-1 rounded-full bg-input" />
    </ScrollAreaPrimitive.ScrollAreaScrollbar>
  );
}
