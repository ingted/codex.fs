# WBS.ACTOR-001 Session / Worker Actor Model RFC

狀態：Done  
開始時間：2026-07-05 04:16 +08:00  
更新時間：2026-07-05 04:34 +08:00  
關聯 RFC：`doc/RFC/RFC-ACTOR-0001.session-worker-actor-model.md`  
關聯 Test：`T-ACTOR-001`

## 目標

定義 sharded `WorkerActor` / specialized `SessionActor` contract，讓後續 actor implementation 可直接使用 PTCS ActorFabric/MessageFabric 與 `codex.fs.runtime`，不再把 prompt loop 或 mailbox fabric 放進 host route。

## 完成內容

- Accepted `RFC-ACTOR-0001`.
- 定義 `WorkerActor` common capability 與 `SessionActor` Foreman/session specialization。
- 定義 Foreman/session/child worker entity id 與 participant id mapping。
- 定義 direct/public/group MessageFabric consume policy。
- 定義 runtime call、reply、ready-to-ack、MessageFabric ack 與 actor delivery confirm ordering。
- 定義 PTCS external ActorSystem attachment and LAN/DNS advertise requirement。

## 非完成內容

- 尚未新增 `codex.fs.actor` project。
- 尚未實作 Akka actor/sharding code。
- 尚未新增或執行 `misc/verifyActorFabricSessionWorker.fsx`。

## 後續

- Implementation WBS 應先新增 actor protocol/types/tests，再做 real PTCS ActorFabric region/proxy verifier。
- `CLI-010` 與 `WEB-001` 可依此 actor participant model 定義 target/perspective switching。
