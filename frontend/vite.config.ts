import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { fileURLToPath, URL } from "node:url";
import { defineConfig } from "vitest/config";

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: { alias: { "@": fileURLToPath(new URL("./src", import.meta.url)) } },
  clearScreen: false,
  server: { watch: { ignored: ["**/src-tauri/**", "**/Engine/**"] } },
  build: { target: "es2022" },
  test: { environment: "jsdom", setupFiles: ["./src/test-setup.ts"], restoreMocks: true },
});
