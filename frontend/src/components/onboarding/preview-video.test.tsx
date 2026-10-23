import { fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/i18n/i18n";
import { PreviewVideo } from "./preview-video";

beforeEach(async () => {
  await i18n.changeLanguage("en");
  vi.spyOn(HTMLMediaElement.prototype, "play").mockResolvedValue();
  vi.spyOn(HTMLMediaElement.prototype, "pause").mockImplementation(() => {});
});

describe("trimmed onboarding video", () => {
  it("skips the introduction on initial playback and every repeat, preserving source timestamps", () => {
    const onMediaTime = vi.fn();
    render(
      <PreviewVideo
        kind="modSettings"
        name="Mod settings preview"
        startTime={2.4}
        onMediaTime={onMediaTime}
      />,
    );
    const player = screen.getByLabelText("Mod settings preview") as HTMLVideoElement;
    expect(player.autoplay).toBe(false);
    expect(player.loop).toBe(false);
    expect(player.getAttribute("src")).toMatch(/#t=2\.4$/);
    expect(player.play).not.toHaveBeenCalled();

    Object.defineProperty(player, "readyState", { configurable: true, value: 1 });
    fireEvent.loadedMetadata(player);
    expect(player.currentTime).toBe(2.4);
    expect(player.play).toHaveBeenCalledOnce();
    player.currentTime = 177 / 60;
    fireEvent.seeked(player);
    expect(onMediaTime).toHaveBeenLastCalledWith(177 / 60);

    player.currentTime = 6.233333;
    fireEvent.ended(player);
    expect(player.currentTime).toBe(2.4);
    expect(player.play).toHaveBeenCalledTimes(2);
    fireEvent.seeked(player);
    expect(onMediaTime).toHaveBeenLastCalledWith(2.4);
  });

  it("keeps ordinary recommendation videos on native autoplay and looping", () => {
    render(<PreviewVideo kind="jrp" name="JRP preview" />);
    const player = screen.getByLabelText("JRP preview") as HTMLVideoElement;
    expect(player.autoplay).toBe(true);
    expect(player.loop).toBe(true);
    expect(player.currentTime).toBe(0);
    expect(player.getAttribute("src")).not.toContain("#t=");
  });
});
