import {
  Add01Icon,
  Alert02Icon,
  ArrowDown01Icon,
  ArrowLeft01Icon,
  ArrowRight01Icon,
  ArrowUpRight01Icon,
  Cancel01Icon,
  CheckmarkCircle01Icon,
  Delete02Icon,
  Download01Icon,
  File01Icon,
  Folder01Icon,
  Globe02Icon,
  KeyboardIcon,
  Loading03Icon,
  MoreHorizontalIcon,
  Package01Icon,
  PauseIcon,
  PlayIcon,
  PuzzleIcon,
  RefreshIcon,
  Search01Icon,
  Settings01Icon,
  SlidersHorizontalIcon,
  Tick01Icon,
} from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";

const icons = {
  add: Add01Icon,
  down: ArrowDown01Icon,
  package: Package01Icon,
  search: Search01Icon,
  puzzle: PuzzleIcon,
  file: File01Icon,
  right: ArrowRight01Icon,
  external: ArrowUpRight01Icon,
  refresh: RefreshIcon,
  more: MoreHorizontalIcon,
  settings: Settings01Icon,
  folder: Folder01Icon,
  close: Cancel01Icon,
  check: Tick01Icon,
  download: Download01Icon,
  left: ArrowLeft01Icon,
  trash: Delete02Icon,
  globe: Globe02Icon,
  loading: Loading03Icon,
  alert: Alert02Icon,
  ready: CheckmarkCircle01Icon,
  play: PlayIcon,
  pause: PauseIcon,
  keyboard: KeyboardIcon,
  sliders: SlidersHorizontalIcon,
};
export type IconName = keyof typeof icons;
export function Icon({
  name,
  size = 20,
  className,
}: {
  name: IconName;
  size?: number;
  className?: string;
}) {
  return (
    <HugeiconsIcon
      icon={icons[name]}
      size={Math.max(size, 18)}
      strokeWidth={1.7}
      className={className}
      aria-hidden="true"
    />
  );
}
