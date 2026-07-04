# RFC-UI-0002 PTCS Web Worker Chat Profile

ID：`RFC-UI-0002`
狀態：Accepted
日期：2026-07-05
關聯：`Requirement.md`, `SD.md`, `WBS.UI-002.md`, `WBS.HOST-005.md`, `Test.md`

## Background

PTCS Host already has a browser chat UI. The current deployment uses multiple profiles:

- `http://127.0.0.1:82/chat` for local PTCS.Login validation.
- `https://my-ai.co.in:81/chat` for the public GitHub OAuth profile.

Treating the 81 OAuth redirect as "not the chat UI" misses the documented deployment profile. codex.fs also had a second problem: the standalone host can start its own package-owned `CommSpaMessageFabric`, which is valid for CLI/API/docs verification but cannot make worker participants appear in an already running PTCS Web process.

## Goals

- Use the existing PTCS Web chat and MessageFabric semantics.
- Verify the correct local profile before judging UI availability.
- Make codex.fs host embeddable with caller-owned PTCS `CommSpaMessageFabric`, so PTCS Host can share participant truth with worker actors.
- Keep public documentation free of credential values.

## Non-goals

- Do not create a new codex.fs-only chat UI.
- Do not proxy PTCS Web through localhost-only codex.fs endpoints.
- Do not treat standalone package-owned MessageFabric as the production web integration path.
- Do not add fake/mock browser smoke tests as acceptance.

## Decision

1. PTCS Web validation must read the specified PTCS Host implementation/docs first, then verify the correct profile URL.
2. Local browser validation for the current environment uses `http://127.0.0.1:82/chat`.
3. `https://my-ai.co.in:81/chat` redirecting to GitHub OAuth is expected for that public profile.
4. codex.fs production web integration should run in the same PTCS fabric as the PTCS Host, using `HostRuntime.startWithMessageFabric` or an equivalent caller-owned PTCS fabric attachment.
5. Standalone `codex.fs.host` remains an operator/API/docs tool and an isolated verification surface, not the proof that PTCS Web participants are registered in the live PTCS Host.

## Impact

- `codex.fs.host` now exposes a caller-owned MessageFabric seam.
- WBS/Test distinguish "PTCS Web profile works" from "codex.fs workers are visible in PTCS Web".
- DEVOP documents the correct local 82 verification path and the 81 OAuth profile boundary.
- Harness must require reading specified implementation/docs and verifying the advertised user-facing URL before conclusions.

## Acceptance

- Real browser evidence exists for `http://127.0.0.1:82/chat` login and public prompt send.
- `tests/codex.fs.Tests` covers caller-owned MessageFabric identity.
- Public docs mention no credential values.
- `codex.fs` command is verified as a real installed global tool command.
