import { cva, type VariantProps } from "class-variance-authority";
import { type ButtonHTMLAttributes, forwardRef } from "react";
import { cn } from "@/shared/lib/cn";
import { Icon } from "./icon";

const styles = cva("button", {
  variants: {
    variant: {
      primary: "button-primary",
      secondary: "button-secondary",
      ghost: "button-ghost",
      danger: "button-danger",
    },
    size: { normal: "", small: "button-small", icon: "button-icon" },
  },
  defaultVariants: { variant: "secondary", size: "normal" },
});
type Props = ButtonHTMLAttributes<HTMLButtonElement> &
  VariantProps<typeof styles> & { pending?: boolean };
export const Button = forwardRef<HTMLButtonElement, Props>(function Button(
  { className, variant, size, pending, disabled, children, ...props },
  ref,
) {
  return (
    <button
      type="button"
      {...props}
      ref={ref}
      data-slot="button"
      data-variant={variant ?? "secondary"}
      data-size={size ?? "normal"}
      disabled={disabled || pending}
      aria-busy={pending || undefined}
      className={cn(styles({ variant, size }), className)}
    >
      {pending && <Icon name="loading" className="animate-spin" />}
      {children}
    </button>
  );
});
