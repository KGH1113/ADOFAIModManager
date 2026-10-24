import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiProvider } from "@/api/app-api-provider";
import i18n from "@/i18n/i18n";
import { createMockApi } from "@/mocks/create-mock-api";
import { useOperations } from "@/state/operations/operations-store";
import { usePreferences } from "@/state/preferences/preferences-store";
import { AppRoot } from "./app-root";
import { ManagerProvider } from "./manager-provider";

beforeEach(async () => {
  localStorage.clear();
  useOperations.setState({ label: null, error: null, notice: null });
  usePreferences.setState({
    hydrated: true,
    language: "en",
    page: "install",
    gamePath: null,
    completed: false,
    welcomeOpen: true,
    settingsOpen: false,
    onboarding: { step: "experience", readyBack: "experience" },
  });
  await i18n.changeLanguage("en");
  vi.spyOn(HTMLMediaElement.prototype, "play").mockResolvedValue();
  vi.spyOn(HTMLMediaElement.prototype, "pause").mockImplementation(() => {});
});
function mount(api = createMockApi()) {
  const cache = new QueryClient({
    defaultOptions: { queries: { retry: false, retryDelay: 0, gcTime: 0 } },
  });
  render(
    <QueryClientProvider client={cache}>
      <ApiProvider api={api}>
        <ManagerProvider>
          <AppRoot />
        </ManagerProvider>
      </ApiProvider>
    </QueryClientProvider>,
  );
  return api;
}
describe("desktop user journeys", () => {
  it.each([false, true])(
    "starts on welcome after restoring preferences with completed=%s",
    async (completed) => {
      const gamePath = "/Preview/ADanceOfFireAndIce.app";
      localStorage.setItem(
        "adofai-mod-manager.preferences.v1",
        JSON.stringify({ state: { completed, gamePath, language: "en" }, version: 1 }),
      );
      await usePreferences.persist.rehydrate();
      mount();
      expect(await screen.findByRole("heading", { name: "ADOFAI Mod Manager" })).toBeTruthy();
      expect(usePreferences.getState()).toMatchObject({
        completed,
        gamePath,
        language: "en",
        welcomeOpen: true,
        onboarding: { step: "welcome" },
      });
      expect(screen.queryByTestId("app-settings")).toBeNull();
      const user = userEvent.setup();
      await user.click(screen.getByRole("button", { name: "Continue" }));
      await screen.findByTestId("experience-returning");
    },
  );

  it.each([
    ["tuf", ["tuf", "tufLite"]],
    ["more", ["countdown", "calibration", "youtube", "effects"]],
  ] as const)("installs all remaining mods on the %s screen", async (step, keys) => {
    const api = createMockApi();
    await api.installation.execute("/Preview/ADanceOfFireAndIce.app", "install");
    usePreferences.setState({ onboarding: { step, readyBack: "experience" } });
    mount(api);
    const user = userEvent.setup();
    const first = await screen.findByTestId(`install-${keys[0]}`);
    await waitFor(() => expect(first.hasAttribute("disabled")).toBe(false));
    await user.click(first);
    await waitFor(() => expect(first.textContent).toBe("Installed"));
    const download = vi.spyOn(api.catalog, "download");
    const all = screen.getByRole("button", { name: "Install all" });
    await waitFor(() => expect(all.hasAttribute("disabled")).toBe(false));
    await user.click(all);
    await screen.findByRole("button", { name: "All installed" });
    for (const key of keys)
      expect(screen.getByTestId(`install-${key}`).textContent).toBe("Installed");
    expect(download).toHaveBeenCalledTimes(step === "tuf" ? 1 : 4);
    expect(screen.getByRole("button", { name: "All installed" }).hasAttribute("disabled")).toBe(
      true,
    );
    expect(screen.getByRole("button", { name: "Continue" }).hasAttribute("disabled")).toBe(false);
  });
  it("stops install all on failure and retries the remaining TUF mods", async () => {
    const api = createMockApi();
    await api.installation.execute("/Preview/ADanceOfFireAndIce.app", "install");
    const download = vi.spyOn(api.catalog, "download").mockRejectedValueOnce(new Error("offline"));
    usePreferences.setState({ onboarding: { step: "tuf", readyBack: "experience" } });
    mount(api);
    const user = userEvent.setup();
    const all = await screen.findByRole("button", { name: "Install all" });
    await waitFor(() => expect(all.hasAttribute("disabled")).toBe(false));
    await user.click(all);
    await waitFor(() => expect(useOperations.getState().error).toBe("offline"));
    expect(download).toHaveBeenCalledTimes(1);
    await user.keyboard("{Escape}");
    await waitFor(() => expect(all.hasAttribute("disabled")).toBe(false));
    await user.click(all);
    await screen.findByRole("button", { name: "All installed" });
    expect(download).toHaveBeenCalledTimes(3);
  });
  it("shows both TUF previews and keeps setup details and independent installs available", async () => {
    const api = createMockApi();
    await api.installation.execute("/Preview/ADanceOfFireAndIce.app", "install");
    usePreferences.setState({ onboarding: { step: "tuf", readyBack: "experience" } });
    mount(api);
    const user = userEvent.setup();
    const lite = await screen.findByTestId("install-tufLite");
    await waitFor(() => expect(lite.hasAttribute("disabled")).toBe(false));
    expect(document.querySelectorAll("video")).toHaveLength(2);
    expect(screen.queryByRole("tablist")).toBeNull();
    expect(screen.queryByRole("button", { name: "TUFHelperLite setup details" })).toBeNull();
    expect(screen.queryByText(/ADOFAI IPC/)).toBeNull();
    await user.click(screen.getByRole("button", { name: "TUFHelper setup details" }));
    expect((await screen.findByRole("dialog")).textContent).toContain("Level Info");
    await user.keyboard("{Escape}");
    await user.click(await screen.findByTestId("install-tufLite"));
    await waitFor(() =>
      expect(screen.getByTestId("install-tufLite").textContent).toBe("Installed"),
    );
    await user.click(await screen.findByTestId("install-tuf"));
    await waitFor(() => expect(screen.getByTestId("install-tuf").textContent).toBe("Installed"));
    expect(document.querySelectorAll("video")).toHaveLength(2);
    expect(screen.getByTestId("install-tuf").textContent).toBe("Installed");
    expect(screen.getByTestId("install-tufLite").textContent).toBe("Installed");
  });
  it("checks installed mods against TUF and downloads updates through the import confirmation", async () => {
    const path = "/Preview/ADanceOfFireAndIce.app";
    const api = createMockApi();
    await api.installation.execute(path, "install");
    await api.mods.install(path, { path: "/Preview/mod.zip" });
    const list = api.catalog.list;
    const catalog = vi
      .spyOn(api.catalog, "list")
      .mockImplementation(async () =>
        (await list()).map((mod) => (mod.id === "jrp" ? { ...mod, version: "2.0" } : mod)),
      );
    const download = vi.spyOn(api.catalog, "download");
    const install = vi.spyOn(api.mods, "install");
    const inspect = api.mods.inspect;
    vi.spyOn(api.mods, "inspect").mockImplementation(async (game, source) => ({
      ...(await inspect(game, source)),
      version: "2.0",
    }));
    act(() =>
      usePreferences.setState({
        welcomeOpen: false,
        completed: true,
        page: "mods",
        gamePath: path,
      }),
    );
    mount(api);
    const user = userEvent.setup();
    const check = await screen.findByRole("button", { name: "Check for updates" });
    await waitFor(() => expect(check.hasAttribute("disabled")).toBe(false));
    expect(catalog).not.toHaveBeenCalled();
    await user.click(check);
    await screen.findByText("Updates available: 1");
    await user.click(screen.getByRole("button", { name: "Update JipperResourcePack" }));
    await screen.findByRole("dialog", { name: "Confirm mod installation" });
    expect(download).toHaveBeenCalled();
    expect(install).not.toHaveBeenCalled();
    await user.click(screen.getByRole("button", { name: "Install" }));
    await waitFor(() => expect(install).toHaveBeenCalledWith(path, { token: expect.any(String) }));
  });
  it("shows update lookup failures and allows retry", async () => {
    const path = "/Preview/ADanceOfFireAndIce.app";
    const api = createMockApi();
    await api.mods.install(path, { path: "/Preview/mod.zip" });
    const list = api.catalog.list;
    const lookup = vi.spyOn(api.catalog, "list").mockRejectedValue(new Error("offline"));
    act(() =>
      usePreferences.setState({
        welcomeOpen: false,
        completed: true,
        page: "mods",
        gamePath: path,
      }),
    );
    mount(api);
    const user = userEvent.setup();
    const check = await screen.findByRole("button", { name: "Check for updates" });
    await waitFor(() => expect(check.hasAttribute("disabled")).toBe(false));
    await user.click(check);
    await screen.findByText("Unable to check for updates. Please try again.");
    lookup.mockImplementation(list);
    await user.click(check);
    await screen.findByText("Updates available: 0");
  });
  it("rejects an update archive for another mod and releases the download", async () => {
    const path = "/Preview/ADanceOfFireAndIce.app";
    const api = createMockApi();
    await api.mods.install(path, { path: "/Preview/mod.zip" });
    const list = api.catalog.list;
    vi.spyOn(api.catalog, "list").mockImplementation(async () =>
      (await list()).map((mod) => (mod.id === "jrp" ? { ...mod, version: "2.0" } : mod)),
    );
    vi.spyOn(api.mods, "inspect").mockResolvedValue({
      id: "WrongMod",
      name: "Wrong",
      version: "2.0",
      alreadyInstalled: false,
    });
    const discard = vi.spyOn(api.catalog, "discard");
    const install = vi.spyOn(api.mods, "install");
    act(() =>
      usePreferences.setState({
        welcomeOpen: false,
        completed: true,
        page: "mods",
        gamePath: path,
      }),
    );
    mount(api);
    const user = userEvent.setup();
    const check = await screen.findByRole("button", { name: "Check for updates" });
    await waitFor(() => expect(check.hasAttribute("disabled")).toBe(false));
    await user.click(check);
    await user.click(await screen.findByRole("button", { name: "Update JipperResourcePack" }));
    await waitFor(() => expect(useOperations.getState().error).toBe("mods.update.archiveMismatch"));
    expect(screen.queryByRole("dialog", { name: "Confirm mod installation" })).toBeNull();
    expect(discard).toHaveBeenCalledTimes(1);
    expect(install).not.toHaveBeenCalled();
  });
  it("opens settings directly and reopens welcome from the settings dialog", async () => {
    mount();
    const user = userEvent.setup();
    await user.click(screen.getByTestId("experience-returning"));
    await user.click(await screen.findByTestId("get-started"));
    await screen.findByTestId("app-settings");
    expect(usePreferences.getState().completed).toBe(true);
    await user.click(screen.getByTestId("app-settings"));
    await screen.findByRole("dialog", { name: "App settings" });
    const language = screen.getByRole("combobox", { name: "Language" });
    expect(document.activeElement).toBe(screen.getByRole("heading", { name: "App settings" }));
    expect(document.activeElement).not.toBe(language);
    expect(screen.queryByRole("menu")).toBeNull();
    await user.click(screen.getByRole("button", { name: "Back to welcome" }));
    await screen.findByRole("heading", { name: "ADOFAI Mod Manager" });
    expect(usePreferences.getState().completed).toBe(true);
    expect(document.querySelectorAll(".orbital-artwork img")).toHaveLength(0);
  });
  it("changes settings language through the shadcn select with keyboard navigation", async () => {
    mount();
    const user = userEvent.setup();
    await user.keyboard("{Control>},{/Control}");
    await screen.findByRole("dialog", { name: "App settings" });
    const language = screen.getByRole("combobox", { name: "Language" });
    expect(language.getAttribute("data-slot")).toBe("select-trigger");
    await user.tab();
    expect(document.activeElement).toBe(language);
    await user.keyboard(" ");
    await screen.findByRole("listbox");
    expect(screen.getAllByRole("option")).toHaveLength(4);
    await user.keyboard("{Home}{ArrowDown}{Enter}");
    await waitFor(() => expect(usePreferences.getState().language).toBe("ko"));
    expect(language.textContent).toContain("한국어");
    expect(screen.queryByRole("listbox")).toBeNull();
    expect(screen.getByRole("dialog")).toBeTruthy();
    await user.keyboard("{Escape}");
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
  });
  it("installs the new recommendations independently, configures only onboarding installs, and finishes", async () => {
    const api = mount();
    const setup = vi.spyOn(api.mods, "configureOnboarding");
    const user = userEvent.setup();
    await user.click(screen.getByTestId("experience-new"));
    await screen.findByRole("heading", { name: "Switch ADOFAI to alpha" });
    expect(screen.getAllByRole("img")).toHaveLength(2);
    expect(
      screen.getByRole("button", { name: "Waiting for alpha update…" }).hasAttribute("disabled"),
    ).toBe(true);
    await user.click(screen.getByRole("button", { name: "Skip" }));
    const installUmm = await screen.findByRole("button", { name: "Install UMM" });
    await waitFor(() => expect(installUmm.hasAttribute("disabled")).toBe(false));
    await user.click(installUmm);
    await user.click(await screen.findByRole("button", { name: "Continue" }));
    await screen.findByRole("heading", { name: "You can now open the mod menu" });
    expect(document.querySelector(".mod-settings-guide video")).not.toBeNull();
    const shortcutVideo = document.querySelector<HTMLVideoElement>(".mod-settings-guide video");
    if (!shortcutVideo) throw new Error("Missing mod settings video");
    const keys = document.querySelectorAll(".shortcut-preview kbd");
    for (const [frame, ctrl, f10] of [
      [159, false, false],
      [160, true, false],
      [176, true, false],
      [177, true, true],
      [200, true, true],
      [201, false, false],
      [144, false, false],
    ] as const) {
      shortcutVideo.currentTime = frame / 60;
      fireEvent.seeked(shortcutVideo);
      expect(keys[0]?.getAttribute("data-pressed")).toBe(String(ctrl));
      expect(keys[1]?.getAttribute("data-pressed")).toBe(String(f10));
    }
    const changeShortcut = await screen.findByRole("button", {
      name: "Change shortcut: Ctrl + F10",
    });
    await waitFor(() => expect(changeShortcut.hasAttribute("disabled")).toBe(false));
    await user.click(changeShortcut);
    fireEvent.keyDown(window, { code: "Escape" });
    expect(changeShortcut.getAttribute("aria-pressed")).toBe("false");
    await user.click(changeShortcut);
    fireEvent.keyDown(window, { code: "KeyK", ctrlKey: true, shiftKey: true });
    await screen.findByRole("button", { name: "Change shortcut: Ctrl + Shift + K" });
    expect(
      screen.getByText("Launch A Dance of Fire and Ice, then press Ctrl + Shift + K."),
    ).toBeTruthy();
    expect(await api.mods.loadUmmShortcut("/Preview/ADanceOfFireAndIce.app")).toEqual({
      keyCode: "K",
      modifiers: 3,
    });
    shortcutVideo.currentTime = 177 / 60;
    fireEvent.seeked(shortcutVideo);
    expect(
      [...document.querySelectorAll(".shortcut-preview kbd")].every(
        (key) => key.getAttribute("data-pressed") === "true",
      ),
    ).toBe(true);
    await user.click(screen.getByRole("button", { name: "Got it" }));
    await user.click(await screen.findByTestId("install-jrp"));
    await waitFor(() => expect(screen.getByTestId("install-jrp").textContent).toBe("Installed"));
    expect(setup).toHaveBeenCalledWith("/Preview/ADanceOfFireAndIce.app", "jrp-defaults");
    await user.click(screen.getByRole("button", { name: "Continue" }));
    await screen.findByRole("heading", { name: "Key bindings" });
    await user.click(await screen.findByRole("button", { name: "Save & continue" }));
    await user.click(await screen.findByTestId("install-chatter"));
    await waitFor(() =>
      expect(setup).toHaveBeenCalledWith("/Preview/ADanceOfFireAndIce.app", "chatter-defaults"),
    );
    await user.click(await screen.findByRole("button", { name: "Continue" }));
    await user.click(await screen.findByTestId("install-tufLite"));
    await waitFor(() =>
      expect(screen.getByTestId("install-tufLite").textContent).toBe("Installed"),
    );
    await user.click(screen.getByTestId("install-tuf"));
    await waitFor(() =>
      expect(setup).toHaveBeenCalledWith("/Preview/ADanceOfFireAndIce.app", "tuf-overlay"),
    );
    await waitFor(() =>
      expect(screen.getByRole("button", { name: "Continue" }).hasAttribute("disabled")).toBe(false),
    );
    await user.click(screen.getByRole("button", { name: "Continue" }));
    await screen.findByRole("heading", { name: "More mods" });
    expect(screen.queryByRole("heading", { name: "PACL2" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Skip" })).toBeNull();
    expect(screen.queryByRole("heading", { name: "Recommended settings" })).toBeNull();
    await user.click(screen.getByRole("button", { name: "Continue" }));
    await user.click(await screen.findByTestId("get-started"));
    await screen.findByTestId("nav-mods");
    expect(usePreferences.getState().page).toBe("mods");
  });
  it("requires a completed alpha update before enabling the primary continue button", async () => {
    const api = createMockApi();
    const load = api.installation.load;
    const reload = vi.spyOn(api.installation, "load");
    mount(api);
    const user = userEvent.setup();
    await user.click(screen.getByTestId("experience-new"));
    await screen.findByRole("heading", { name: "Switch ADOFAI to alpha" });
    const waiting = screen.getByRole("button", { name: "Waiting for alpha update…" });
    expect(waiting.hasAttribute("disabled")).toBe(true);
    expect(screen.queryByRole("button", { name: "Check again" })).toBeNull();
    expect(screen.queryByText(/Wait for Steam to finish updating/)).toBeNull();
    await user.click(waiting);
    expect(usePreferences.getState().onboarding.step).toBe("alpha");
    reload.mockImplementation(async (path) => ({
      ...(await load(path)),
      steamBranch: "alpha",
      alphaInstalled: false,
    }));
    const calls = reload.mock.calls.length;
    act(() => window.dispatchEvent(new Event("focus")));
    await waitFor(() => expect(reload.mock.calls.length).toBeGreaterThan(calls));
    expect(
      screen.getByRole("button", { name: "Waiting for alpha update…" }).hasAttribute("disabled"),
    ).toBe(true);
    reload.mockImplementation(async (path) => ({
      ...(await load(path)),
      steamBranch: "alpha",
      alphaInstalled: true,
    }));
    act(() => window.dispatchEvent(new Event("focus")));
    const next = await screen.findByRole("button", { name: "Continue to UMM installation" });
    expect(next.hasAttribute("disabled")).toBe(false);
    expect(usePreferences.getState().onboarding.step).toBe("alpha");
    expect(screen.getByRole("progressbar").getAttribute("aria-valuenow")).toBe("2");
    await user.click(next);
    await screen.findByRole("button", { name: "Install UMM" });
    await user.click(screen.getByRole("button", { name: "Back" }));
    await screen.findByTestId("experience-new");
    await user.click(screen.getByTestId("experience-new"));
    await screen.findByRole("button", { name: "Install UMM" });
    expect(usePreferences.getState().onboarding.step).toBe("installation");
  });
  it("still skips the guide when alpha is already installed before setup begins", async () => {
    const api = createMockApi();
    const load = api.installation.load;
    const reload = vi.spyOn(api.installation, "load");
    reload.mockImplementation(async (path) => ({
      ...(await load(path)),
      steamBranch: "alpha",
      alphaInstalled: true,
    }));
    mount(api);
    const user = userEvent.setup();
    await waitFor(() => expect(reload).toHaveBeenCalled());
    await user.click(screen.getByTestId("experience-new"));
    await screen.findByRole("button", { name: "Install UMM" });
    expect(usePreferences.getState().onboarding.step).toBe("installation");
    expect(screen.queryByRole("heading", { name: "Switch ADOFAI to alpha" })).toBeNull();
  });
  it("detects a completed alpha update automatically while the guide stays open", async () => {
    const api = createMockApi();
    const load = api.installation.load;
    const reload = vi.spyOn(api.installation, "load");
    mount(api);
    const user = userEvent.setup();
    await user.click(screen.getByTestId("experience-new"));
    await screen.findByRole("heading", { name: "Switch ADOFAI to alpha" });
    expect(
      screen.getByRole("button", { name: "Waiting for alpha update…" }).hasAttribute("disabled"),
    ).toBe(true);
    reload.mockImplementation(async (path) => ({
      ...(await load(path)),
      steamBranch: "alpha",
      alphaInstalled: true,
    }));
    await waitFor(
      () =>
        expect(
          screen
            .getByRole("button", { name: "Continue to UMM installation" })
            .hasAttribute("disabled"),
        ).toBe(false),
      {
        timeout: 4_000,
      },
    );
    expect(usePreferences.getState().onboarding.step).toBe("alpha");
    await user.click(screen.getByRole("button", { name: "Continue to UMM installation" }));
    await screen.findByRole("button", { name: "Install UMM" });
    const calls = reload.mock.calls.length;
    act(() => window.dispatchEvent(new Event("focus")));
    expect(reload).toHaveBeenCalledTimes(calls);
  });
  it("cancels a ZIP import without installing and retains the inspected game when selection changes", async () => {
    act(() => usePreferences.setState({ welcomeOpen: false, page: "mods", completed: true }));
    const api = mount();
    const install = vi.spyOn(api.mods, "install");
    const user = userEvent.setup();
    const add = await screen.findByRole("button", { name: /Add Mod/ });
    await waitFor(() => expect(add.hasAttribute("disabled")).toBe(false));
    await user.click(add);
    await user.click(await screen.findByRole("button", { name: "Cancel" }));
    expect(install).not.toHaveBeenCalled();
    expect(screen.queryByTestId("install-celebration")).toBeNull();
    await user.click(add);
    await screen.findByRole("dialog", { name: "Confirm mod installation" });
    act(() => usePreferences.getState().setGamePath("/other/game"));
    await user.click(screen.getByRole("button", { name: "Install" }));
    await waitFor(() =>
      expect(install).toHaveBeenCalledWith("/Preview/ADanceOfFireAndIce.app", {
        path: "/Preview/mod.zip",
      }),
    );
  });
});
