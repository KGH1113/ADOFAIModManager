import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiProvider } from "@/api/app-api-provider";
import { ManagerProvider } from "@/app/manager-provider";
import i18n from "@/i18n/i18n";
import { createMockApi } from "@/mocks/create-mock-api";
import {
  copyKeyLayouts,
  defaultKey10,
  defaultKeyLayouts,
  keyViewerStyles,
} from "@/models/onboarding/key-bindings";
import { useOperations } from "@/state/operations/operations-store";
import { usePreferences } from "@/state/preferences/preferences-store";
import { JrpKeysStep } from "./jrp-keys-step";

const path = "/Preview/ADanceOfFireAndIce.app";
beforeEach(async () => {
  useOperations.setState({ label: null, error: null, notice: null });
  usePreferences.setState({ gamePath: path, welcomeOpen: false });
  await i18n.changeLanguage("en");
});
function mount(api = createMockApi()) {
  const next = vi.fn();
  const cache = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
  render(
    <QueryClientProvider client={cache}>
      <ApiProvider api={api}>
        <ManagerProvider>
          <JrpKeysStep next={next} />
        </ManagerProvider>
      </ApiProvider>
    </QueryClientProvider>,
  );
  return { api, next };
}

const slot = (index: number) => {
  const button = document.querySelector<HTMLButtonElement>(`[data-key-slot="${index}"]`);
  if (!button) throw new Error(`Missing key slot ${index}`);
  return button;
};
const hand = (side: "left" | "right") => document.querySelector(`[data-hand="${side}"]`);

describe("JRP key registration and hand guidance", () => {
  it("highlights the matching finger on hover without changing key capture", async () => {
    mount();
    const user = userEvent.setup();
    await screen.findByRole("button", { name: "left little finger, current key Tab" });
    const buttons = Array.from(document.querySelectorAll<HTMLButtonElement>("[data-key-slot]"));
    const left = document.querySelector('[data-hand="left"]');
    const right = document.querySelector('[data-hand="right"]');
    const fingers = [
      [0, null],
      [1, null],
      [2, null],
      [3, null],
      [null, 3],
      [null, 2],
      [null, 1],
      [null, 0],
      [4, null],
      [null, 4],
    ];
    for (const [index, button] of buttons.entries()) {
      await user.hover(button);
      expect(left?.getAttribute("data-highlighted-finger")).toBe(
        String(fingers[index][0] ?? "none"),
      );
      expect(right?.getAttribute("data-highlighted-finger")).toBe(
        String(fingers[index][1] ?? "none"),
      );
      expect(left?.getAttribute("data-active-finger")).toBe("none");
      expect(right?.getAttribute("data-active-finger")).toBe("none");
      await user.unhover(button);
      expect(left?.getAttribute("data-highlighted-finger")).toBe("none");
      expect(right?.getAttribute("data-highlighted-finger")).toBe("none");
    }
    expect(screen.queryByRole("button", { name: /Cancel/ })).toBeNull();
    await user.click(buttons[3]);
    await user.hover(buttons[9]);
    expect(left?.getAttribute("data-active-finger")).toBe("3");
    expect(right?.getAttribute("data-highlighted-finger")).toBe("4");
    await user.unhover(buttons[9]);
    expect(left?.getAttribute("data-active-finger")).toBe("3");
    expect(right?.getAttribute("data-highlighted-finger")).toBe("none");
    fireEvent.keyDown(window, { code: "Escape" });
    expect(left?.getAttribute("data-active-finger")).toBe("none");
  });

  it("lowers the selected finger while waiting and saves a key already assigned to another slot", async () => {
    const { api, next } = mount();
    const save = vi.spyOn(api.mods, "saveJrpKeys");
    const user = userEvent.setup();
    const little = await screen.findByRole("button", {
      name: "left little finger, current key Tab",
    });
    await user.click(little);
    expect(document.querySelector('[data-hand="left"]')?.getAttribute("data-active-finger")).toBe(
      "0",
    );
    fireEvent.keyDown(window, { code: "KeyQ", key: "ㅂ", repeat: true });
    expect(little.getAttribute("aria-pressed")).toBe("true");
    fireEvent.keyDown(window, { code: "KeyE", key: "ㄷ" });
    expect(document.querySelector('[data-hand="left"]')?.getAttribute("data-active-finger")).toBe(
      "none",
    );
    expect(screen.getByRole("button", { name: "left little finger, current key E" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "left index finger, current key E" })).toBeTruthy();
    expect(little.getAttribute("aria-pressed")).toBe("false");
    await user.click(screen.getByRole("button", { name: "Save & continue" }));
    await waitFor(() => expect(next).toHaveBeenCalledTimes(1));
    expect(save).toHaveBeenCalledWith(path, [101, ...defaultKey10.slice(1)], 10);
  });
  it("switches fingers and cancels capture on Escape or window blur", async () => {
    mount();
    const user = userEvent.setup();
    await user.click(await screen.findByRole("button", { name: "left thumb, current key Space" }));
    expect(document.querySelector('[data-hand="left"]')?.getAttribute("data-active-finger")).toBe(
      "4",
    );
    await user.click(screen.getByRole("button", { name: "right thumb, current key ," }));
    expect(document.querySelector('[data-hand="left"]')?.getAttribute("data-active-finger")).toBe(
      "none",
    );
    expect(document.querySelector('[data-hand="right"]')?.getAttribute("data-active-finger")).toBe(
      "4",
    );
    fireEvent.keyDown(window, { code: "Escape" });
    expect(document.querySelector('[data-hand="right"]')?.getAttribute("data-active-finger")).toBe(
      "none",
    );
    await user.click(screen.getByRole("button", { name: "right index finger, current key P" }));
    act(() => window.dispatchEvent(new Event("blur")));
    expect(screen.queryByRole("button", { name: /Cancel/ })).toBeNull();
    expect(document.querySelector('[data-hand="right"]')?.getAttribute("data-active-finger")).toBe(
      "none",
    );
  });
  it.each(keyViewerStyles)(
    "loads the selected Key%i style and saves its own bindings",
    async (style) => {
      const api = createMockApi();
      vi.spyOn(api.mods, "loadJrpKeys").mockResolvedValue({
        style,
        layouts: copyKeyLayouts(defaultKeyLayouts),
      });
      const save = vi.spyOn(api.mods, "saveJrpKeys");
      const { next } = mount(api);
      const user = userEvent.setup();
      const choice = await screen.findByRole("radio", { name: `Key${style}` });
      await waitFor(() => expect((choice as HTMLInputElement).checked).toBe(true));
      expect(document.querySelectorAll("[data-key-slot]")).toHaveLength(style);
      expect(screen.queryByText("KPS")).toBeNull();
      expect(screen.queryByText("Total")).toBeNull();
      await user.click(screen.getByRole("button", { name: "Save & continue" }));
      await waitFor(() => expect(next).toHaveBeenCalledOnce());
      expect(save).toHaveBeenLastCalledWith(path, defaultKeyLayouts[style], style);
    },
  );

  it.each([12, 16] as const)(
    "uses the same thumb motion for both thumb keys in Key%i",
    async (style) => {
      mount();
      const user = userEvent.setup();
      await user.click(await screen.findByRole("radio", { name: `Key${style}` }));
      for (const index of [8, 9, 10, 11]) {
        const side = index < 10 ? "left" : "right";
        await user.hover(slot(index));
        expect(hand(side)?.getAttribute("data-highlighted-finger")).toBe("4");
        await user.click(slot(index));
        expect(hand(side)?.getAttribute("data-active-finger")).toBe("4");
        fireEvent.keyDown(window, { code: `F${index + 4}` });
        expect(hand(side)?.getAttribute("data-active-finger")).toBe("none");
        await user.unhover(slot(index));
      }
    },
  );

  it.each([16] as const)(
    "only highlights the hand edge for the four edge keys in Key%i",
    async (style) => {
      mount();
      const user = userEvent.setup();
      await user.click(await screen.findByRole("radio", { name: `Key${style}` }));
      for (const index of [12, 13, 14, 15]) {
        const side = index < 14 ? "left" : "right";
        const other = index < 14 ? "right" : "left";
        await user.hover(slot(index));
        expect(hand(side)?.getAttribute("data-highlighted-edge")).toBe("true");
        expect(hand(other)?.getAttribute("data-highlighted-edge")).toBe("false");
        expect(hand(side)?.getAttribute("data-highlighted-finger")).toBe("none");
        await user.click(slot(index));
        expect(slot(index).getAttribute("aria-pressed")).toBe("true");
        expect(hand("left")?.getAttribute("data-active-finger")).toBe("none");
        expect(hand("right")?.getAttribute("data-active-finger")).toBe("none");
        fireEvent.keyDown(window, { code: "Escape" });
        await user.unhover(slot(index));
        expect(hand(side)?.getAttribute("data-highlighted-edge")).toBe("false");
      }
    },
  );

  it("offers Key4 and Key8 and removes Key20", async () => {
    const { api, next } = mount();
    const save = vi.spyOn(api.mods, "saveJrpKeys");
    const user = userEvent.setup();
    await user.click(await screen.findByRole("radio", { name: "Key4" }));
    expect(screen.queryByRole("radio", { name: "Key20" })).toBeNull();
    expect(screen.getByRole("radio", { name: "Key8" })).toBeTruthy();
    await user.click(slot(0));
    expect(hand("left")?.getAttribute("data-active-finger")).toBe("2");
    fireEvent.keyDown(window, { code: "KeyQ" });
    await user.click(screen.getByRole("button", { name: "Save & continue" }));
    await waitFor(() => expect(next).toHaveBeenCalledOnce());
    expect(save).toHaveBeenLastCalledWith(path, [113, ...defaultKeyLayouts[4].slice(1)], 4);
  });

  it("keeps each style's draft and cancels capture when switching styles", async () => {
    mount();
    const user = userEvent.setup();
    await user.click(await screen.findByRole("radio", { name: "Key12" }));
    await user.click(slot(0));
    fireEvent.keyDown(window, { code: "KeyQ" });
    await user.click(slot(8));
    await user.click(screen.getByRole("radio", { name: "Key16" }));
    expect(slot(0).textContent).toContain("Tab");
    expect(hand("left")?.getAttribute("data-active-finger")).toBe("none");
    fireEvent.keyDown(window, { code: "KeyW" });
    expect(slot(8).textContent).toContain("Space");
    await user.click(screen.getByRole("radio", { name: "Key12" }));
    expect(slot(0).textContent).toContain("Q");
    expect(slot(8).textContent).toContain("Space");
  });

  it("keeps the editor available after a failed save and retries the selected style", async () => {
    const api = createMockApi();
    vi.spyOn(api.mods, "loadJrpKeys").mockResolvedValue({
      style: 16,
      layouts: copyKeyLayouts(defaultKeyLayouts),
    });
    const original = api.mods.saveJrpKeys;
    const save = vi.spyOn(api.mods, "saveJrpKeys").mockRejectedValueOnce(new Error("game_running"));
    const { next } = mount(api);
    const user = userEvent.setup();
    await screen.findByRole("radio", { name: "Key16" });
    await user.click(screen.getByRole("button", { name: "Save & continue" }));
    await screen.findByRole("alert");
    expect(next).not.toHaveBeenCalled();
    expect(save).toHaveBeenLastCalledWith(path, defaultKeyLayouts[16], 16);
    save.mockImplementation(original);
    await user.click(screen.getByRole("radio", { name: "Key12" }));
    await user.click(screen.getByRole("button", { name: "Save & continue" }));
    await waitFor(() => expect(next).toHaveBeenCalledTimes(1));
    expect(save).toHaveBeenLastCalledWith(path, defaultKeyLayouts[12], 12);
  });
});
