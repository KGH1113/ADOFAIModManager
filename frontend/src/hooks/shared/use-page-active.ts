import { useEffect, useState } from "react";
export function usePageActive(requireFocus = true) {
  const [active, setActive] = useState(
    () => document.visibilityState !== "hidden" && (!requireFocus || document.hasFocus()),
  );
  useEffect(() => {
    const update = () =>
      setActive(document.visibilityState !== "hidden" && (!requireFocus || document.hasFocus()));
    document.addEventListener("visibilitychange", update);
    window.addEventListener("focus", update);
    window.addEventListener("blur", update);
    return () => {
      document.removeEventListener("visibilitychange", update);
      window.removeEventListener("focus", update);
      window.removeEventListener("blur", update);
    };
  }, [requireFocus]);
  return active;
}
