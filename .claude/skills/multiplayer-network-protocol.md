# 网络协议与联机架构

## 架构总览

联机基于 **Unity Netcode for GameObjects + Unity Transport (UDP)**。一台设备做 Host（主机），另一台做 Client（客户端）。Host 同时运行 Server，球物理权威在 Host 端。

```
iPhone (Host/Server) ←→ 热点/WiFi ←→ Clone (Client)
```

## 核心文件

| 文件 | 职责 |
|------|------|
| `MultiplayerManager.cs` | 创建/加入/退出房间，管理 NetworkManager 生命周期 |
| `NetworkLobbySync.cs` | 大厅消息：选人、就绪、进入场景（LS 通道） |
| `NetworkBattleSync.cs` | 战斗消息：位置/挥拍/击球/比分（BS 通道） |
| `NetworkUIState.cs` | UI 状态同步 |
| `NetworkActionButton.cs` | 网络按钮交互 |

## 消息协议

两条命名消息通道：`LS`（Lobby Sync）和 `BS`（Battle Sync），均在 `Update` 中延迟注册确保 NetworkManager 就绪。

### LS 通道（大厅）

| 消息 | 格式 | 说明 |
|------|------|------|
| Select | `S\|slot\|index` | slot=1(主机) 或 2(客户端), index=角色索引 |
| Ready | `R\|slot` | 选人完成就绪 |
| SceneReady | `G\|slot` | Two Player 点击就绪 |

- 主机收到远程消息后 `Forward()` 转发给所有客户端
- `OnBothReady` → 进入战斗场景
- `OnBothSceneReady` → 进入选人场景

### BS 通道（战斗）

**P 消息（位置状态，每 3 帧发一次）**：

```
P|H/C|posX|posY|facing|swing|serve|walk|birdieX|birdieY|birdieInPlay|leftScore|rightScore|server|hitId|hitSpeed|hitDir|hitPosX|hitPosY
```

- `H` = Host→Client
- `C` = Client→Host
- 挥拍/发球状态不节流（立即发送）
- 球数据：H 消息总是接受，C 消息只在球未飞行时接受（发球阶段球位置同步）
- 比分数据：`remoteTotal > localTotal` 才更新（单调递增防回退）
- 击球数据：hitId 去重，H 事件丢失时 P 消息兜底

**H 消息（击球事件）**：`H|speed|dir|posX|posY|hitId`

**S 消息（比分同步）**：`S|leftScore|rightScore|server`

**W 消息（挥拍事件）**：`W|isServe(0/1)|eventId`

**M 消息（暂停）**：`M|P/R/M`（Pause/Resume/Menu）

## 关键设计决策

1. **UDP 丢包处理**：H 事件和 S 消息通过 P 消息冗余备份（周期性重发），hitId 去重
2. **球物理权威**：只在 Host 端检测得分，客户端 `OnShuttlecockLanded` 直接 return
3. **状态同步**：Host 端 H 事件处理器自动切 `WaitingToServe → Playing`（防止发球太快导致球落地不算分）
4. **比分单调递增**：`remoteTotal > localTotal` 防止旧 P 消息覆盖新比分
5. **Handler 重注册**：退房重连时自动重置标志，确保 LS/BS handler 绑定到新 NetworkManager

## 联机调试技巧

1. 用 **ParrelSync** 或手动复制项目文件夹创建 Clone
2. Host 先创建房间 → Client 再通过 IP 加入
3. Console 过滤 `[BS]`、`[LS]`、`[MP]`、`[GM]` 查看日志
4. 调试快捷键：F1 左边发球、F2 右边发球、F3 仅反手模式、F4 强制胜利、F6 切换 AI
