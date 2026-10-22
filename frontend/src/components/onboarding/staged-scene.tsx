import type { ReactNode } from "react";

// The parent scene owns the transition so controls enter together and remain responsive.
export function StagedScene({ children }: { kind: "installation" | "ready"; children: ReactNode }) {
  return <div className="staged-scene">{children}</div>;
}
