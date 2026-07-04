# WBS Detail: UI-001 PTCS Web UI extension/RFC

WBS ID：`UI-001`
狀態：Done
Progress：100
StartTime：2026-07-04 23:13 +08:00
UpdatedAt：2026-07-04 23:23 +08:00
Previous：`E2E-002`, `DOC-003`
SD：`SD §16.12`
Test：`T-UI-001`

## Scope

完成 PTCS Web UI extension 的 RFC 與 verifier plan。此 slice 不修改 PTCS UI source，不新增 fake/mock smoke，不宣稱 Web UI 已可用。

## Inputs Read

- `G:\PulseTrade2.fs\Libs\PulseTrade.Comm.Spa\doc\Requirement.md`
- `G:\PulseTrade2.fs\Libs\PulseTrade.Comm.Spa\doc\SA.md`
- `G:\PulseTrade2.fs\Libs\PulseTrade.Comm.Spa\doc\SD.md`
- `G:\PulseTrade2.fs\Libs\PulseTrade.Comm.Spa\doc\RFC-PTC-SPA-0006.dynamic-client-extension-points.md`
- `G:\PulseTrade2.fs\Libs\PulseTrade.Comm.Spa\doc\RFC-PTC-SPA-0008.unified-sdui-target-extension-contract.md`
- `G:\PulseTrade2.fs\Libs\PulseTrade.Comm.Spa\doc\RFC-PTC-SPA-0010.actors-page-dynamic-dsl-rendering.md`
- `G:\PulseTrade2.fs\Libs\PulseTrade.Comm.Spa\doc\RFC-SPA-UPSTREAM-0001.shared-sharded-message-fabric-contract.zh-Hant.md`
- `G:\PulseTrade2.fs\Libs\PulseTrade.Comm.Spa\doc\RFC-SPA-UPSTREAM-0002.external-actor-system-attachment.zh-Hant.md`
- `G:\PulseTrade2.fs\Libs\PulseTrade.Comm.Spa\doc\RFC-SPA-UPSTREAM-0003.shared-durable-ingress-fabric-contract.zh-Hant.md`

## Decision

- codex.fs Web UI must be a PTCS extension consumer, not a new UI fabric.
- Prompt/reply truth remains PTCS `CommSpaMessageFabric`.
- Host control is HTTP status/control only; it is not a MessageFabric transport.
- Non-dev UI/control paths must use LAN/DNS-reachable advertised URI or PTCS same-origin allow-list handler.
- Future UI package should be separate from PTCS core and should not force PTCS core to reference codex.fs.

## Deliverables

- `doc/RFC/RFC-UI-0001.ptcs-web-ui-extension.md`
- Updated `doc/WBS.md`
- Updated `doc/Test.md`
- Updated `doc/DevLog.md`
- Updated `MCP.KM.md`

## Verification

- RFC includes background, goals, non-goals, scenarios, decisions, impact, acceptance, and source references.
- `T-UI-001` records that this is an RFC/verifier-plan pass, not a working UI pass.
- Future UI implementation must add real browser/MessageFabric verifier cases listed in RFC acceptance.

## Blockers

- None for this RFC slice.

## Deferred

- Actual PTCS UI package/source implementation.
- Browser Playwright verifier against a real PTCS host with codex.fs extension registered.
- Durable UI path over `CommSpaDurableMessageFabric` / `DurableIngress`.
