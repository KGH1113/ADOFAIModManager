import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MotionConfig } from "motion/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiProvider } from "@/api/app-api-provider";
import i18n from "@/i18n/i18n";
import { createMockApi } from "@/mocks/create-mock-api";
import { useOperations } from "@/state/operations/operations-store";
import { TufIntro } from "./tuf-intro";

beforeEach(async () => {
  useOperations.setState({ label: null, error: null, notice: null });
  await i18n.changeLanguage("en");
});

function mount(disabled = false) {
  const api = createMockApi();
  const next = vi.fn();
  const view = (locked: boolean) => (
    <MotionConfig reducedMotion="always">
      <ApiProvider api={api}>
        <TufIntro next={next} disabled={locked} />
      </ApiProvider>
    </MotionConfig>
  );
  const { rerender } = render(view(disabled));
  return { api, next, setDisabled: (locked: boolean) => rerender(view(locked)) };
}

async function reachSlide(user: ReturnType<typeof userEvent.setup>, index: number) {
  const titles = [
    "What is TUF?",
    "Browse and download levels",
    "TUF difficulty system",
    "Clear submissions and scoring",
  ];
  for (const title of titles.slice(1, index + 1)) {
    await user.click(screen.getByRole("button", { name: "Next slide" }));
    await screen.findByRole("heading", { name: title });
  }
}

describe("TUF introduction", () => {
  it("navigates with chevrons and arrow keys", async () => {
    mount();
    const user = userEvent.setup();
    expect(screen.getByRole("heading", { name: "What is TUF?" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Previous slide" }).hasAttribute("disabled")).toBe(
      true,
    );

    await user.click(screen.getByRole("button", { name: "Next slide" }));
    await screen.findByRole("heading", { name: "Browse and download levels" });
    expect(screen.getByRole("group", { name: "2 of 4: Discover" })).toBeTruthy();
    await user.click(screen.getByRole("button", { name: "Previous slide" }));
    await screen.findByRole("heading", { name: "What is TUF?" });

    await reachSlide(user, 3);
    expect(screen.queryByRole("button", { name: "Next slide" })).toBeNull();
    expect(screen.getByRole("button", { name: "Continue to TUF mods" })).toBeTruthy();
    await user.keyboard("{ArrowLeft}");
    await screen.findByRole("heading", { name: "TUF difficulty system" });
    await user.keyboard("{ArrowRight}");
    await screen.findByRole("heading", { name: "Clear submissions and scoring" });
  });

  it("continues to TUF mods through the last slide's right chevron", async () => {
    const { next } = mount();
    const user = userEvent.setup();
    expect(screen.queryByRole("button", { name: "Continue to TUF mods" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Skip" })).toBeNull();
    await reachSlide(user, 3);
    await user.click(screen.getByRole("button", { name: "Continue to TUF mods" }));
    expect(next).toHaveBeenCalledTimes(1);
  });

  it("preserves keyboard navigation when the focused slide control unmounts", async () => {
    mount();
    const user = userEvent.setup();
    await user.tab();
    expect(document.activeElement).toBe(screen.getByRole("button", { name: "Open TUF Website" }));
    await user.keyboard("{ArrowRight}");
    await screen.findByRole("heading", { name: "Browse and download levels" });
    expect(document.activeElement).toBe(screen.getByRole("heading", { name: "About TUF" }));
    await user.keyboard("{ArrowRight}");
    await screen.findByRole("heading", { name: "TUF difficulty system" });
    expect(document.activeElement).toBe(screen.getByRole("heading", { name: "About TUF" }));
  });

  it("opens the real TUF homepage through the desktop API", async () => {
    const { api } = mount();
    const openUrl = vi.spyOn(api.desktop, "openUrl").mockResolvedValue(undefined);
    const user = userEvent.setup();
    await user.click(screen.getByRole("button", { name: "Open TUF Website" }));
    await waitFor(() => expect(openUrl).toHaveBeenCalledExactlyOnceWith("https://tuforums.com"));
  });

  it("leaves slider arrow keys available without changing the slide", async () => {
    mount();
    const user = userEvent.setup();
    await reachSlide(user, 2);
    const slider = await screen.findByRole("slider", { name: "Difficulty rating" });
    await user.click(slider);
    for (const key of ["ArrowLeft", "ArrowRight"]) {
      expect(fireEvent.keyDown(slider, { key, cancelable: true })).toBe(true);
      expect(screen.getByRole("group", { name: "3 of 4: Difficulty" })).toBeTruthy();
    }
    // JSDOM does not implement the browser's native range-keyboard value change.
    fireEvent.change(slider, { target: { value: "11" } });
    expect(screen.getByRole("img", { name: "P11" }).getAttribute("src")).toContain("/P11.png");
    expect(slider.getAttribute("aria-valuetext")).toBe("P11");
  });

  it("updates official icons and ranges for PGU, Quantum, and special ratings", async () => {
    mount();
    const user = userEvent.setup();
    await reachSlide(user, 2);
    await screen.findByRole("img", { name: "P10" });
    await user.click(screen.getByRole("button", { name: /Galactic/ }));
    expect(screen.getByRole("img", { name: "G10" }).getAttribute("src")).toContain("/G10.png");
    expect(screen.getByRole("heading", { name: "G1 — G20" })).toBeTruthy();

    await user.click(screen.getByRole("tab", { name: "Quantum" }));
    expect(screen.getByRole("tabpanel", { name: "Quantum" })).toBeTruthy();
    expect(screen.getByRole("img", { name: "GQ2" }).getAttribute("src")).toContain("/GQ2.png");
    expect(screen.getByRole("heading", { name: "G9 — G12" })).toBeTruthy();
    expect(screen.queryByRole("button", { name: /Planetary/ })).toBeNull();
    const slider = screen.getByRole("slider", { name: "Difficulty rating" });
    expect(slider.getAttribute("min")).toBe("0");
    expect(slider.getAttribute("max")).toBe("4");
    fireEvent.change(slider, { target: { value: "4" } });
    await user.click(screen.getByRole("button", { name: /Universal/ }));
    expect(screen.getByRole("img", { name: "UQ4" }).getAttribute("src")).toContain("/UQ4.png");
    expect(screen.getByRole("heading", { name: "U17 — U20" })).toBeTruthy();
    expect(slider.getAttribute("aria-valuetext")).toBe("UQ4: U17 – U20");

    await user.click(screen.getByRole("tab", { name: "PGU" }));
    expect(screen.getByRole("img", { name: "U10" })).toBeTruthy();
    expect(slider.getAttribute("min")).toBe("1");
    expect(slider.getAttribute("max")).toBe("20");
    await user.keyboard("{End}");
    expect(screen.getByRole("tabpanel", { name: "Other ratings" })).toBeTruthy();
    expect(screen.getByRole("heading", { name: "Below P1" })).toBeTruthy();
    await user.click(screen.getByRole("button", { name: "Qq" }));
    expect(screen.getByRole("img", { name: "Qq" }).getAttribute("src")).toContain("/Qq.png");
    expect(screen.getByRole("heading", { name: "Unknown" })).toBeTruthy();
    expect(screen.queryByRole("slider")).toBeNull();
  });

  it("disables navigation and external links while locked", async () => {
    const { api, next } = mount(true);
    const openUrl = vi.spyOn(api.desktop, "openUrl");
    const user = userEvent.setup();
    for (const button of screen.getAllByRole("button")) {
      expect(button.hasAttribute("disabled")).toBe(true);
      await user.click(button);
    }
    fireEvent.keyDown(screen.getByRole("heading", { name: "About TUF" }), { key: "ArrowRight" });
    expect(screen.getByRole("heading", { name: "What is TUF?" })).toBeTruthy();
    expect(next).not.toHaveBeenCalled();
    expect(openUrl).not.toHaveBeenCalled();
  });

  it("locks the interactive difficulty controls and final continue action", async () => {
    const { next, setDisabled } = mount();
    const user = userEvent.setup();
    await reachSlide(user, 2);
    await screen.findByRole("slider", { name: "Difficulty rating" });
    setDisabled(true);
    for (const control of [
      ...screen.getAllByRole("button"),
      ...screen.getAllByRole("tab"),
      screen.getByRole("slider"),
    ]) {
      expect(control.hasAttribute("disabled")).toBe(true);
    }
    await user.click(screen.getByRole("tab", { name: "Quantum" }));
    expect(screen.getByRole("tabpanel", { name: "PGU" })).toBeTruthy();

    setDisabled(false);
    await user.click(screen.getByRole("button", { name: "Next slide" }));
    await screen.findByRole("heading", { name: "Clear submissions and scoring" });
    setDisabled(true);
    const finish = screen.getByRole("button", { name: "Continue to TUF mods" });
    expect(finish.hasAttribute("disabled")).toBe(true);
    await user.click(finish);
    expect(next).not.toHaveBeenCalled();
  });
});
