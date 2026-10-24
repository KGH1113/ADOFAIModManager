import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { StrictMode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiProvider } from "@/api/app-api-provider";
import { ManagerProvider } from "@/app/manager-provider";
import i18n from "@/i18n/i18n";
import { createMockApi } from "@/mocks/create-mock-api";
import { useOperations } from "@/state/operations/operations-store";
import { UpdateProvider, UpdateStatus } from "./update-provider";

beforeEach(async () => {
  useOperations.setState({ label: null, error: null, notice: null });
  await i18n.changeLanguage("en");
});

function mount(api: ReturnType<typeof createMockApi>) {
  return render(
    <StrictMode>
      <QueryClientProvider
        client={new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } })}
      >
        <ApiProvider api={api}>
          <ManagerProvider>
            <UpdateProvider>
              <p>Workspace</p>
              <UpdateStatus settings />
            </UpdateProvider>
          </ManagerProvider>
        </ApiProvider>
      </QueryClientProvider>
    </StrictMode>,
  );
}

describe("update interface", () => {
  it("checks once in Strict Mode and offers a manual check after startup", async () => {
    const api = createMockApi();
    api.updates.check = vi.fn().mockResolvedValue(null);
    mount(api);
    await screen.findByText("Workspace");
    expect(api.updates.check).toHaveBeenCalledOnce();
    expect(screen.getByText("You're up to date.")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Check for updates" }));
    await waitFor(() => expect(api.updates.check).toHaveBeenCalledTimes(2));
  });
  it("shows a deferred download, release notes, and a restart button locked during mod operations", async () => {
    const api = createMockApi();
    let finishDownload!: () => void;
    api.updates.check = vi.fn().mockResolvedValue({ version: "0.3.0", notes: "Fixed mod import." });
    api.updates.download = () =>
      new Promise<void>((resolve) => {
        finishDownload = resolve;
      });
    api.updates.install = vi.fn().mockRejectedValue(new Error("permission denied"));
    mount(api);
    await screen.findByText("Downloading version 0.3.0…");
    fireEvent.click(screen.getByRole("button", { name: "Update later" }));
    await screen.findByText("Workspace");
    await act(async () => finishDownload());
    expect(screen.getByText("Version 0.3.0 is ready.")).toBeTruthy();
    expect(screen.getByText("Fixed mod import.")).toBeTruthy();
    act(() => useOperations.setState({ label: "Installing mod" }));
    expect(
      (screen.getByRole("button", { name: "Update and restart" }) as HTMLButtonElement).disabled,
    ).toBe(true);
    act(() => useOperations.setState({ label: null }));
    fireEvent.click(screen.getByRole("button", { name: "Update and restart" }));
    await screen.findByText("Couldn't complete the update. Please try again.");
    expect(useOperations.getState().label).toBeNull();
    expect(api.updates.install).toHaveBeenCalledOnce();
  });
});
