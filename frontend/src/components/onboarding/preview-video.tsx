import { type ReactNode, useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { usePageActive } from "@/hooks/shared/use-page-active";

// Drop a named MP4 into this directory; Vite includes only files that actually exist.
const videos = import.meta.glob<string>("/src/assets/onboarding/*.mp4", {
  eager: true,
  query: "?url",
  import: "default",
});
export function PreviewVideo({
  kind,
  name,
  fallback,
  onMediaTime,
}: {
  kind: "jrp" | "chatter" | "tufLite" | "tuf" | "modSettings";
  name: string;
  fallback?: ReactNode;
  onMediaTime?: (time: number) => void;
}) {
  const { t } = useTranslation();
  const src = videos[`/src/assets/onboarding/${kind}.mp4`];
  const video = useRef<HTMLVideoElement>(null);
  const active = usePageActive(false);
  const [failed, setFailed] = useState(false);
  useEffect(() => {
    const player = video.current;
    if (!player || !src || failed || !onMediaTime) return;
    let frame = 0;
    let disposed = false;
    const useVideoFrames = typeof player.requestVideoFrameCallback === "function";
    const update = () => onMediaTime(player.currentTime);
    const tick: VideoFrameRequestCallback = (_now, metadata) => {
      if (disposed) return;
      onMediaTime(metadata.mediaTime);
      frame = player.requestVideoFrameCallback(tick);
    };
    const fallbackTick = () => {
      if (disposed) return;
      update();
      frame = requestAnimationFrame(fallbackTick);
    };
    update();
    if (useVideoFrames) frame = player.requestVideoFrameCallback(tick);
    else frame = requestAnimationFrame(fallbackTick);
    player.addEventListener("seeked", update);
    player.addEventListener("loadeddata", update);
    return () => {
      disposed = true;
      if (useVideoFrames) player.cancelVideoFrameCallback(frame);
      else cancelAnimationFrame(frame);
      player.removeEventListener("seeked", update);
      player.removeEventListener("loadeddata", update);
    };
  }, [src, failed, onMediaTime]);
  useEffect(() => {
    const player = video.current;
    if (!player || !src || failed) return;
    // Set the properties before play() for WebKit's muted autoplay policy.
    player.muted = true;
    player.defaultMuted = true;
    const play = () => {
      if (active) void player.play().catch(() => {});
      else player.pause();
    };
    player.addEventListener("canplay", play);
    play();
    return () => {
      player.removeEventListener("canplay", play);
      player.pause();
    };
  }, [active, src, failed]);
  if ((!src || failed) && fallback) return <>{fallback}</>;
  if (!src) return <div className="preview-video preview-video-empty" aria-hidden="true" />;
  return (
    <div className="preview-video">
      <video
        ref={video}
        src={src}
        autoPlay={active}
        muted
        loop
        playsInline
        preload="auto"
        onError={() => setFailed(true)}
        aria-label={name}
      />
      {failed && <p className="video-fallback">{t("onboarding.video.unavailable")}</p>}
    </div>
  );
}
