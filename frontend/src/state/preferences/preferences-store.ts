import { create } from "zustand";
import { createJSONStorage, persist } from "zustand/middleware";
import {
  type OnboardingEvent,
  type OnboardingState,
  transition,
} from "@/models/onboarding/onboarding-model";
export type Page = "install" | "catalog" | "mods" | "logs";
export type Language = "system" | "ko" | "en" | "zh";
interface Preferences {
  page: Page;
  gamePath: string | null;
  language: Language;
  completed: boolean;
  hydrated: boolean;
  welcomeOpen: boolean;
  onboarding: OnboardingState;
  settingsOpen: boolean;
  navigate(page: Page): void;
  setGamePath(path: string | null): void;
  setLanguage(language: Language): void;
  send(event: OnboardingEvent, alphaInstalled?: boolean, jrpInstalled?: boolean): void;
  complete(installed: boolean): void;
  replay(): void;
  setSettingsOpen(open: boolean): void;
  initialize(): void;
}
export const usePreferences = create<Preferences>()(
  persist(
    (set) => ({
      page: "install",
      gamePath: null,
      language: "system",
      completed: false,
      hydrated: false,
      welcomeOpen: true,
      onboarding: { step: "welcome", readyBack: "experience" },
      settingsOpen: false,
      navigate: (page) => set({ page }),
      setGamePath: (gamePath) => set({ gamePath }),
      setLanguage: (language) => set({ language }),
      send: (event, alphaInstalled, jrpInstalled) =>
        set((state) => ({
          onboarding: transition(state.onboarding, event, alphaInstalled, jrpInstalled),
        })),
      complete: (installed) =>
        set({ completed: true, welcomeOpen: false, page: installed ? "mods" : "install" }),
      replay: () =>
        set({
          welcomeOpen: true,
          settingsOpen: false,
          onboarding: { step: "welcome", readyBack: "experience" },
        }),
      setSettingsOpen: (settingsOpen) => set({ settingsOpen }),
      initialize: () => set((state) => ({ hydrated: true, welcomeOpen: !state.completed })),
    }),
    {
      name: "adofai-mod-manager.preferences.v1",
      storage: createJSONStorage(() => localStorage),
      version: 1,
      partialize: ({ completed, gamePath, language }) => ({ completed, gamePath, language }),
      onRehydrateStorage: () => (state) => state?.initialize(),
      merge: (persisted, current) => {
        const value = persisted as Partial<Preferences> | undefined;
        return {
          ...current,
          completed: value?.completed === true,
          gamePath: typeof value?.gamePath === "string" ? value.gamePath : null,
          language: ["system", "ko", "en", "zh"].includes(value?.language ?? "")
            ? (value?.language as Language)
            : "system",
        };
      },
    },
  ),
);
