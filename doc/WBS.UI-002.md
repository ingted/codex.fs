# WBS Detail: UI-002 PTCS local82 chat profile/browser correction

WBS ID：`UI-002`
狀態：Done
Progress：100
Previous：`UI-001`, `HOST-005`
Test：`T-UI-002`
Test case：`TC-UI-002 real PTCS Host 82 login/send + worker visibility boundary`
SD：`SD §9`, `SD §16.12`
RFC：`doc/RFC/RFC-UI-0002.ptcs-web-worker-chat-profile.md`

## Goal

校正 PTCS Web 驗收入口與產品判斷。Current PTCS Host has two profiles:

- `http://127.0.0.1:82/chat`: local PTCS.Login chat entry for this environment.
- `https://my-ai.co.in:81/chat`: public GitHub OAuth profile; redirecting to GitHub login is expected.

## Evidence

- PTCS Host implementation: `G:\PulseTrade.fs\Libs\PulseTrade.Comm\src\PulseTrade.Comm.Spa.Host`.
- PTCS package implementation: `G:\PulseTrade2.fs\Libs\PulseTrade.Comm.Spa`.
- Real browser evidence:
  - `G:\codex.fs\.codex.fs\ptcs-web-inspect-20260705012257-local82\summary.json`
  - `G:\codex.fs\.codex.fs\ptcs-web-inspect-20260705012257-local82-send\summary.json`
  - `G:\codex.fs\.codex.fs\ptcs-web-inspect-20260705012257-local82-send\02-after-send.png`

Observed behavior:

- Local 82 login reaches `/chat`.
- Public channel is visible.
- Sending a public prompt renders the prompt in the thread.
- No codex.fs worker/session participant is visible in that PTCS Host yet.

## Decision

codex.fs must not build a parallel web chat or message store. PTCS Web integration requires the same PTCS `CommHub` / `CommSpaMessageFabric` / ActorFabric boundary used by `PulseTrade.Comm.Spa.Host`. `HOST-005` provides the package seam for caller-owned `CommSpaMessageFabric`.

## Boundary

This slice verifies the existing PTCS Web profile and corrects the integration boundary. It does not yet implement the full worker actor loop inside the live PTCS Host process.
