import { type KeyboardEvent, type MouseEvent, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import {
  acceptsKey,
  defaultElementNoteTab,
  type ElementNoteTab,
  elementNoteKeys,
  elementNoteTabs,
  keyboardRows,
  keyName,
  normalizeKeyCode,
  type Platform,
} from "@/models/onboarding/element-guide";

interface Hit {
  code: string;
  accepted: boolean;
  id: number;
}

/**
 * Shows which keys reach the game while ElementNote's key limiter is on, and lets the
 * player press their own keys to check. Keys are captured only while the board has focus,
 * so Tab and Space keep working everywhere else; Esc gives focus back. Space and Enter are
 * captured too, so they never "click" the board itself.
 */
export function KeyTester({ platform }: { platform: Platform }) {
  const { t } = useTranslation();
  const [tab, setTab] = useState<ElementNoteTab>(defaultElementNoteTab);
  const [listening, setListening] = useState(false);
  const [hit, setHit] = useState<Hit | null>(null);
  const rows = useMemo(() => keyboardRows(platform), [platform]);
  const allowed = elementNoteKeys[tab] as readonly string[];
  const register = (code: string) =>
    setHit((previous) => ({
      code: normalizeKeyCode(code),
      accepted: acceptsKey(tab, code),
      id: (previous?.id ?? 0) + 1,
    }));
  const onKeyDown = (event: KeyboardEvent<HTMLButtonElement>) => {
    if (event.code === "Escape") {
      event.currentTarget.blur();
      return;
    }
    event.preventDefault();
    if (!event.repeat && event.code) register(event.code);
  };
  const onClick = (event: MouseEvent<HTMLButtonElement>) => {
    const code = (event.target as HTMLElement).closest<HTMLElement>("[data-code]")?.dataset.code;
    if (code) register(code);
  };
  const tabName = `${tab}K`;
  const status = hit
    ? t(`onboarding.elementStart.tester.${hit.accepted ? "accepted" : "blocked"}`, {
        key: keyName(hit.code, platform),
        tab: tabName,
      })
    : t(`onboarding.elementStart.tester.${listening ? "listening" : "prompt"}`);

  return (
    <div className="key-tester">
      <div className="key-tester-toolbar">
        <fieldset className="key-tester-tabs">
          <legend className="sr-only">{t("onboarding.elementStart.tester.tabs")}</legend>
          {elementNoteTabs.map((count) => (
            <button
              key={count}
              type="button"
              aria-pressed={tab === count}
              onClick={() => {
                setTab(count);
                setHit(null);
              }}
            >
              {count}K
              {count === defaultElementNoteTab && (
                <small>{t("onboarding.elementStart.tester.default")}</small>
              )}
            </button>
          ))}
        </fieldset>
        <span className="key-tester-caption">
          {t("onboarding.elementStart.tester.tabs")} ·{" "}
          {t("onboarding.elementStart.tester.count", { count: allowed.length })}
        </span>
      </div>
      <button
        type="button"
        className="key-tester-board"
        aria-label={t("onboarding.elementStart.tester.label", {
          tab: tabName,
          keys: allowed.map((code) => keyName(code, platform)).join(", "),
        })}
        aria-describedby="key-tester-status"
        data-listening={listening}
        onKeyDown={onKeyDown}
        onClick={onClick}
        onFocus={() => setListening(true)}
        onBlur={() => setListening(false)}
      >
        {rows.map((row) => (
          <div key={row[0].code} className="key-tester-row">
            {row.map((key) => {
              const pressed = hit?.code === key.code;
              return (
                <span
                  key={pressed ? `${key.code}-${hit.id}` : key.code}
                  className="key-tester-key"
                  style={{ flexGrow: key.width ?? 1 }}
                  data-code={key.code}
                  data-allowed={allowed.includes(key.code)}
                  data-hit={pressed ? (hit.accepted ? "accepted" : "blocked") : undefined}
                >
                  {key.label}
                </span>
              );
            })}
          </div>
        ))}
      </button>
      <p
        id="key-tester-status"
        className="key-tester-status"
        role="status"
        data-result={hit ? (hit.accepted ? "accepted" : "blocked") : undefined}
      >
        {status}
      </p>
      <div className="key-tester-change">
        <p>{t("onboarding.elementStart.tester.change")}</p>
        <ol className="element-path" aria-label={t("onboarding.elementStart.tester.change")}>
          <li>ElementNote</li>
          <li>{t("onboarding.elementStart.tester.status")}</li>
          <li>{t("onboarding.elementStart.tester.limiter")}</li>
        </ol>
      </div>
    </div>
  );
}
