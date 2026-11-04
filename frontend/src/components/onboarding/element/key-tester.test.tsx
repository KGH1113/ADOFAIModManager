import { act, fireEvent, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it } from "vitest";
import i18n from "@/i18n/i18n";
import { KeyTester } from "./key-tester";

beforeEach(async () => {
  await i18n.changeLanguage("en");
});

describe("Element key checker", () => {
  it("only captures keys while the keyboard has focus", async () => {
    render(<KeyTester platform="windows" />);
    const board = screen.getByRole("button", { name: /Key checker/ });
    const status = screen.getByRole("status");
    expect(status.textContent).toBe("Click the keyboard, then press the keys you use");
    expect(fireEvent.keyDown(window, { code: "KeyF" })).toBe(true);
    expect(status.textContent).toBe("Click the keyboard, then press the keys you use");

    act(() => board.focus());
    expect(status.textContent).toBe("Press keys to check · Esc to stop");
    // Space and Tab must not scroll or move focus while the player is testing them.
    expect(fireEvent.keyDown(board, { code: "Tab" })).toBe(false);
    expect(status.textContent).toBe("Tab works in the game");
    fireEvent.keyDown(board, { code: "KeyF" });
    expect(status.textContent).toBe("F doesn't count with the 16K tab");
    expect(document.querySelector('[data-code="KeyF"]')?.getAttribute("data-hit")).toBe("blocked");

    fireEvent.keyDown(board, { code: "Escape" });
    expect(document.activeElement).not.toBe(board);
    expect(board.getAttribute("data-listening")).toBe("false");
  });
  it("follows the selected ElementNote tab and accepts clicks on keys", async () => {
    const user = userEvent.setup();
    render(<KeyTester platform="macos" />);
    expect(screen.getByRole("button", { name: /^16K/ }).getAttribute("aria-pressed")).toBe("true");
    expect(document.querySelectorAll('[data-allowed="true"]')).toHaveLength(16);
    await user.click(screen.getByRole("button", { name: "4K" }));
    expect(document.querySelectorAll('[data-allowed="true"]')).toHaveLength(4);
    await user.click(screen.getByText("E"));
    expect(screen.getByRole("status").textContent).toBe("E works in the game");
    await user.click(screen.getByText("Q"));
    expect(screen.getByRole("status").textContent).toBe("Q doesn't count with the 4K tab");
  });
});
