import { render, screen } from "@testing-library/react";
import { beforeEach, expect, it } from "vitest";
import i18n from "@/i18n/i18n";
import { type InjectionMethod, mapInstallation } from "@/models/installation/installation-model";
import { engineResponseSchema } from "@/schemas/engine/engine-schema";
import { GameSetupCard } from "./game-setup-card";

beforeEach(async () => {
  await i18n.changeLanguage("en");
});

function installation(status: {
  injectionMethod: InjectionMethod;
  hookInstalled: boolean;
  managerInstalled: boolean;
}) {
  return mapInstallation(
    engineResponseSchema.parse({
      ok: true,
      detected: true,
      gameRunning: false,
      status: { appPath: "/Games/ADOFAI", managerVersion: "0.33.3.0", ...status },
    }),
  );
}

const card = (status: {
  injectionMethod: InjectionMethod;
  hookInstalled: boolean;
  managerInstalled: boolean;
}) => (
  <GameSetupCard
    installation={installation(status)}
    busy={false}
    loading={false}
    choose={() => {}}
    refresh={() => {}}
  />
);

it("shows a Doorstop deployment as installed without a double-injection warning", () => {
  render(card({ injectionMethod: "doorstop", hookInstalled: false, managerInstalled: true }));
  expect(screen.getByRole("heading", { name: "UMM is installed." })).not.toBeNull();
  expect(screen.getByText("Installed · 0.33.3.0 · Doorstop")).not.toBeNull();
  expect(screen.queryByRole("alert")).toBeNull();
});

it("warns when an assembly patch and Doorstop are both active", () => {
  render(card({ injectionMethod: "both", hookInstalled: true, managerInstalled: true }));
  expect(screen.getByText("Installed · 0.33.3.0 · Assembly patch and Doorstop")).not.toBeNull();
  expect(screen.getByRole("alert").textContent).toMatch(/may start twice/);
});

it("keeps a manager without an injection method not installed", () => {
  render(card({ injectionMethod: "none", hookInstalled: false, managerInstalled: true }));
  expect(screen.getByText("Not installed")).not.toBeNull();
  expect(screen.queryByRole("alert")).toBeNull();
});
