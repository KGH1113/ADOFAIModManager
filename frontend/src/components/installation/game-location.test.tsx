import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, expect, it, vi } from "vitest";
import { ApiProvider } from "@/api/app-api-provider";
import i18n from "@/i18n/i18n";
import { createMockApi } from "@/mocks/create-mock-api";
import { useOperations } from "@/state/operations/operations-store";
import { CopyGamePath, GameLocation, RevealGameFolder } from "./game-location";

beforeEach(async () => {
  useOperations.setState({ label: null, error: null, notice: null });
  await i18n.changeLanguage("en");
});

it.each([
  "/Users/player/Library/Application Support/Steam/steamapps/common/A Dance of Fire and Ice/ADanceOfFireAndIce.app",
  "D:\\SteamLibrary\\steamapps\\common\\A Dance of Fire and Ice",
])("shows, copies and reveals the exact game path: %s", async (path) => {
  const user = userEvent.setup();
  const api = createMockApi();
  const reveal = vi.spyOn(api.desktop, "reveal");
  const copy = vi.spyOn(navigator.clipboard, "writeText");
  render(
    <ApiProvider api={api}>
      <GameLocation path={path} />
      <CopyGamePath path={path} />
      <RevealGameFolder path={path} />
    </ApiProvider>,
  );
  expect(screen.getByText(path)).not.toBeNull();
  await user.click(screen.getByRole("button", { name: "Copy path" }));
  await waitFor(() => expect(copy).toHaveBeenCalledWith(path));
  await screen.findByRole("button", { name: "Copied" });
  await user.click(screen.getByRole("button", { name: "Show in folder" }));
  await waitFor(() => expect(reveal).toHaveBeenCalledWith(path));
  expect(screen.getByText(path)).not.toBeNull();
});
