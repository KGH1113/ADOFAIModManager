import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ApiProvider } from "@/api/app-api-provider";
import { ManagerProvider } from "@/app/manager-provider";
import { GlobalFeedback } from "@/components/feedback/global-feedback";
import i18n from "@/i18n/i18n";
import { createMockApi } from "@/mocks/create-mock-api";
import type { CatalogMod } from "@/models/catalog/catalog-model";
import { tufDownloadUrl } from "@/models/catalog/catalog-model";
import { useOperations } from "@/state/operations/operations-store";
import { usePreferences } from "@/state/preferences/preferences-store";
import { CatalogPage } from "./catalog-page";

const gamePath = "/Preview/ADanceOfFireAndIce.app";
afterEach(() => vi.unstubAllGlobals());

beforeEach(async () => {
  localStorage.clear();
  useOperations.setState({ label: null, error: null, notice: null });
  usePreferences.setState({
    hydrated: true,
    language: "en",
    page: "catalog",
    gamePath,
    completed: true,
    welcomeOpen: false,
    settingsOpen: false,
  });
  await i18n.changeLanguage("en");
});

function mount(api = createMockApi()) {
  const cache = new QueryClient({
    defaultOptions: { queries: { retry: false, retryDelay: 0, gcTime: 0 } },
  });
  render(
    <QueryClientProvider client={cache}>
      <ApiProvider api={api}>
        <ManagerProvider>
          <CatalogPage />
          <GlobalFeedback />
        </ManagerProvider>
      </ApiProvider>
    </QueryClientProvider>,
  );
  return api;
}

describe("mod catalog journeys", () => {
  it("detects a preinstalled mod from local metadata and only blocks its installed version", async () => {
    const api = createMockApi();
    const state = await api.installation.load(gamePath);
    const catalog = await api.catalog.list();
    const chatter = catalog.find((mod) => mod.id === "keyboardchatterblocker");
    if (!chatter) throw new Error("Missing preview mod");
    vi.spyOn(api.installation, "load").mockResolvedValue({
      ...state,
      mods: [
        {
          id: "KeyboardChatterBlocker",
          name: "Keyboard Chatter Blocker",
          version: chatter.version,
          installed: true,
          enabled: false,
          status: "OK",
          path: "/Mods/kcb",
          requirements: [],
        },
      ],
    });
    const download = vi.spyOn(api.catalog, "download");
    mount(api);
    const user = userEvent.setup();
    const card = await screen.findByRole("article", { name: chatter.name });
    const installedButton = await within(card).findByRole("button", { name: "Installed" });
    expect(installedButton.hasAttribute("disabled")).toBe(true);
    await user.click(installedButton);
    expect(download).not.toHaveBeenCalled();
    await user.click(within(card).getByRole("button", { name: chatter.name }));
    expect(await screen.findByText(`Installed version: ${chatter.version}`)).toBeTruthy();
    await waitFor(() =>
      expect(screen.getByRole("button", { name: "Installed" }).hasAttribute("disabled")).toBe(true),
    );
    await user.click(screen.getByRole("combobox", { name: "Version" }));
    await user.click(await screen.findByRole("option", { name: "0.1.2" }));
    expect(
      screen.getByRole("button", { name: "Download and install" }).hasAttribute("disabled"),
    ).toBe(false);
  });
  it("reveals cards in batches, preserves the window after details, and resets it for search", async () => {
    let intersect: IntersectionObserverCallback = () => {};
    const disconnect = vi.fn();
    vi.stubGlobal(
      "IntersectionObserver",
      class {
        constructor(callback: IntersectionObserverCallback) {
          intersect = callback;
        }
        observe() {}
        disconnect = disconnect;
      },
    );
    const api = createMockApi();
    const base = (await api.catalog.list())[0];
    const mods = Array.from({ length: 45 }, (_, index) => ({
      ...base,
      id: `mod-${index}`,
      name: `Mod ${index}`,
      uploadedAt: 45 - index,
    }));
    vi.spyOn(api.catalog, "list").mockResolvedValue(mods);
    vi.spyOn(api.catalog, "details").mockResolvedValue({
      ...mods[20],
      latestVersion: base.version,
      versions: [],
    });
    mount(api);
    const user = userEvent.setup();
    await screen.findByRole("button", { name: "Mod 0" });
    expect(screen.getAllByRole("article")).toHaveLength(18);
    act(() =>
      intersect(
        [{ isIntersecting: true } as IntersectionObserverEntry],
        {} as IntersectionObserver,
      ),
    );
    expect(screen.getAllByRole("article")).toHaveLength(36);
    await user.click(screen.getByRole("button", { name: "Mod 20" }));
    await user.click(screen.getByRole("button", { name: "Back to mods" }));
    expect(screen.getAllByRole("article")).toHaveLength(36);
    act(() =>
      intersect(
        [{ isIntersecting: true } as IntersectionObserverEntry],
        {} as IntersectionObserver,
      ),
    );
    expect(screen.getAllByRole("article")).toHaveLength(45);
    await user.type(screen.getByRole("textbox"), "Mod 44");
    await waitFor(() => expect(screen.getAllByRole("article")).toHaveLength(1));
    await user.clear(screen.getByRole("textbox"));
    await waitFor(() => expect(screen.getAllByRole("article")).toHaveLength(18));
    expect(disconnect).toHaveBeenCalled();
  });
  it("filters by creator and tag, sorts cards, and preserves filters after returning from details", async () => {
    const api = createMockApi();
    const base = (await api.catalog.list())[0];
    const overlay = { id: 1, name: "Overlay", color: "#a78bfa" };
    const gameplay = { id: 2, name: "Gameplay", color: "#f87171" };
    const mods: CatalogMod[] = [
      {
        ...base,
        id: "evening-overlay",
        name: "Evening Overlay",
        author: "Jongyeol",
        description: "A compact key display.",
        pinned: false,
        uploadedAt: 1_790_000_000_000,
        downloadCount: 120,
        tags: [overlay],
      },
      {
        ...base,
        id: "rhythm-tools",
        name: "Rhythm Tools",
        author: "Jongyeol",
        description: "Practice tools for difficult levels.",
        pinned: false,
        uploadedAt: 1_790_000_000_001,
        downloadCount: 340,
        tags: [gameplay],
      },
      {
        ...base,
        id: "daylight-overlay",
        name: "Daylight Overlay",
        author: "Mina",
        description: "An alternate key display.",
        pinned: false,
        uploadedAt: 1_790_000_000_002,
        downloadCount: 80,
        tags: [overlay],
      },
    ];
    vi.spyOn(api.catalog, "list").mockResolvedValue(mods);
    const details = vi.spyOn(api.catalog, "details").mockResolvedValue({
      ...mods[0],
      latestVersion: mods[0].version,
      versions: [],
    });
    mount(api);
    const user = userEvent.setup();
    await screen.findByRole("article", { name: "Evening Overlay" });
    expect(details).not.toHaveBeenCalled();

    await user.click(screen.getByRole("combobox", { name: "Sort by" }));
    await user.click(await screen.findByRole("option", { name: "Most downloaded" }));
    expect(screen.getAllByRole("article").map((card) => card.getAttribute("aria-label"))).toEqual([
      "Rhythm Tools",
      "Evening Overlay",
      "Daylight Overlay",
    ]);
    await user.click(screen.getByRole("combobox", { name: "Tags" }));
    await user.click(await screen.findByRole("option", { name: "Overlay" }));
    await waitFor(() => expect(screen.queryByRole("article", { name: "Rhythm Tools" })).toBeNull());
    const search = screen.getByRole("textbox");
    await user.type(search, "Jongyeol");
    await waitFor(() => expect(screen.getAllByRole("article")).toHaveLength(1));
    await user.click(screen.getByRole("button", { name: "Evening Overlay" }));
    await waitFor(() => expect(details).toHaveBeenCalledWith("evening-overlay"));
    await user.click(screen.getByRole("button", { name: "Back to mods" }));
    expect((screen.getByRole("textbox") as HTMLInputElement).value).toBe("Jongyeol");
    expect(screen.getByRole("combobox", { name: "Tags" }).textContent).toBe("Overlay");
    expect(screen.getByRole("combobox", { name: "Sort by" }).textContent).toBe("Most downloaded");
    expect(screen.getAllByRole("article")).toHaveLength(1);
  });

  it("downloads and confirms the selected historical version instead of the latest release", async () => {
    const api = createMockApi();
    const download = vi.spyOn(api.catalog, "download");
    const install = vi.spyOn(api.mods, "install");
    mount(api);
    const user = userEvent.setup();
    await user.click(await screen.findByRole("button", { name: "Keyboard Chatter Blocker" }));
    const versions = await screen.findByRole("combobox", { name: "Version" });
    expect(versions.tagName).toBe("BUTTON");
    await user.click(versions);
    await user.click(await screen.findByRole("option", { name: "0.1.2" }));
    expect(versions.textContent).toBe("0.1.2");
    const downloadButton = screen.getByRole("button", { name: "Download and install" });
    await waitFor(() => expect(downloadButton.hasAttribute("disabled")).toBe(false));
    await user.click(downloadButton);

    const confirmation = await screen.findByRole("dialog", { name: "Confirm mod installation" });
    expect(download).toHaveBeenCalledWith(
      tufDownloadUrl("keyboardchatterblocker", "0.1.2"),
      expect.any(String),
    );
    expect(within(confirmation).getByText(/0\.1\.2/)).not.toBeNull();
    expect(install).not.toHaveBeenCalled();
    await user.click(within(confirmation).getByRole("button", { name: "Install" }));
    await waitFor(() =>
      expect(install).toHaveBeenCalledWith(gamePath, { token: expect.any(String) }),
    );
    const installed = (await api.installation.load(gamePath)).mods;
    expect(installed.find((mod) => mod.id === "KeyboardChatterBlocker")?.version).toBe("0.1.2");
  });

  it("retries failed version history without losing the selected mod", async () => {
    const api = createMockApi();
    const loadDetails = api.catalog.details;
    const details = vi.spyOn(api.catalog, "details").mockRejectedValue(new Error("offline"));
    mount(api);
    const user = userEvent.setup();
    await user.click(await screen.findByRole("button", { name: "Keyboard Chatter Blocker" }));
    const retry = await screen.findByRole("button", { name: "Try again" });
    for (const button of screen.queryAllByRole("button", { name: "Download and install" })) {
      expect(button.hasAttribute("disabled")).toBe(true);
    }
    details.mockImplementation(loadDetails);
    await user.click(retry);
    const versions = await screen.findByRole("combobox", { name: "Version" });
    await user.click(versions);
    expect(await screen.findByRole("option", { name: "0.1.2" })).not.toBeNull();
    await user.keyboard("{Escape}");
    expect(details).toHaveBeenLastCalledWith("keyboardchatterblocker");
    await user.click(screen.getByRole("button", { name: "Back to mods" }));
    expect(screen.getByRole("article", { name: "Keyboard Chatter Blocker" })).not.toBeNull();
  });

  it.each(["loading", "failed"] as const)(
    "keeps a card download cancellable while another mod's details are %s",
    async (detailState) => {
      const api = createMockApi();
      let rejectDownload: (reason: Error) => void = () => {};
      const download = vi.spyOn(api.catalog, "download").mockImplementation(
        () =>
          new Promise<string>((_resolve, reject) => {
            rejectDownload = reject;
          }),
      );
      const cancel = vi.spyOn(api.catalog, "cancel").mockImplementation(async () => {
        rejectDownload(new Error("cancelled"));
      });
      vi.spyOn(api.catalog, "details").mockImplementation(() =>
        detailState === "failed" ? Promise.reject(new Error("offline")) : new Promise(() => {}),
      );
      const inspect = vi.spyOn(api.mods, "inspect");
      mount(api);
      const user = userEvent.setup();
      const card = await screen.findByRole("article", { name: "Keyboard Chatter Blocker" });
      const downloadButton = within(card).getByRole("button", { name: "Download and install" });
      await waitFor(() => expect(downloadButton.hasAttribute("disabled")).toBe(false));
      await user.click(downloadButton);
      await waitFor(() => expect(download).toHaveBeenCalledTimes(1));
      const requestId = download.mock.calls[0][1];
      expect(requestId).toEqual(expect.any(String));

      await user.click(screen.getByRole("button", { name: "JipperResourcePack" }));
      await screen.findByRole("heading", { name: "JipperResourcePack" });
      if (detailState === "failed") {
        await screen.findByRole("button", { name: "Try again" });
      } else {
        expect(screen.getByText(i18n.t("catalog.versionsLoading"))).not.toBeNull();
      }
      expect(screen.getAllByRole("button", { name: "Cancel" })).toHaveLength(1);
      await user.click(screen.getByRole("button", { name: "Cancel" }));
      await waitFor(() => expect(cancel).toHaveBeenCalledWith(requestId));
      await waitFor(() => expect(useOperations.getState().label).toBeNull());
      expect(inspect).not.toHaveBeenCalled();
      expect(screen.queryByRole("dialog", { name: "Confirm mod installation" })).toBeNull();
      expect(screen.queryByRole("button", { name: "Cancel" })).toBeNull();
    },
  );
});
