# TUF introduction

## Direction

A quiet, dark presentation canvas uses the app's existing Figtree type and lime accent, with real TUF screenshots providing the visual weight.

## Content

1. Community: a large homepage screenshot on the left, the requested introduction and website link on the right.
2. Discovery: a contained Levels screenshot including the search field, PGU/Q sliders, two level rows and download controls. Likes sorting is applied with the sort settings panel collapsed. No visible sort caption.
3. Difficulty: prominent PGU / Quantum / Other selectors; tier selection, official icon and range control grouped in one workspace; a separate context column explains the selection. Icons have no added glow, shadow or background lettering. Other choices stay at a fixed position and the selection hint sits directly above them.
4. Clears: a leaderboard screenshot dated Oct 24, 2026 beside definitions of clear score, best score per level and ranked score. No decorative formula diagram.

## Interaction

Directional slide transitions and responsive difficulty emblems respect reduced motion. Previous/next chevrons and keyboard navigation move between slides. The last right chevron continues to TUF mods. Skip sits immediately after Back in the shared footer. The slide index remains in the heading; the bottom slide contents navigation is removed so the content has more vertical space.

## Integration and verification

`tufIntro` sits immediately before `tuf`. Korean, English, and Chinese translations have matching keys and direct, factual headings.

The slideshow chevrons have persistent button surfaces and borders, visible short Previous/Next labels, and at least 44px-wide targets. The forward control uses the lime accent. Hover, press and focus states are distinct, and unavailable navigation stays dimmed. Narrow layouts reserve enough room for both the controls and translated content.

- `bun run check`: TypeScript, Biome, 5 script tests and 115 Vitest tests passed. One existing non-null-assertion warning remains in `umm-shortcut.test.ts`.
- `bun run frontend:build`: passed, with existing dependency annotation and bundle-size warnings.
- The final hint-placement adjustment passed the 8 introduction tests and 2 localization tests again.
- Chromium layout checks: 54 slide/mode views across Korean, English and Chinese at 900×560, 1280×800 and 390×844. No text/control overflow. All five Other selections retain the same button-row position in each layout.
- Native range ArrowRight changes a Quantum rating without advancing the slide. Tab arrow/Home/End navigation, focus continuity between slides, disabled controls, external homepage opening, footer Skip and final continuation are covered.
- `git diff --check`: passed.

## Sources

- [TUF](https://tuforums.com), [Levels](https://tuforums.com/levels), and the live leaderboard: real captures, with provenance in the onboarding assets README.
- [Official difficulty guide](https://the-universal-forums.notion.site/Main-Difficulty-System-PGU-3ca484d6071f8067a0fbeabf5ca2904d): PGU, Quantum ranges and special labels. The 75 official icons retain their original files; exact CDN sources are recorded beside them.
- [Official scoring guide](https://the-universal-forums.notion.site/How-scoring-works-3cb484d6071f8088877de922ba6f63d7): difficulty, X-Accuracy and speed affect clear score; best clear per level counts; ranked score uses up to the top 20 eligible scores with decreasing weight. The introduction deliberately omits the detailed formula and does not imply that playing faster always increases score.
