# 火柴人打羽毛球2 — 项目 Skill

## 项目概览

Unity 2022.3.17f1c1 2D 羽毛球双人对战游戏（仿 Stick Figure Badminton 2），支持单机AI、局域网联机（Netcode for GameObjects + Unity Transport UDP）、Android 打包。

---

## 项目结构

### 场景
- `SampleScene` — 主菜单，Canvas ConstantPixelSize
- `CharacterSelect` — 角色选择
- `Battle Background` — 战斗场景，Canvas ScaleWithScreenSize

### 核心脚本
| 脚本 | 职责 |
|------|------|
| `GameManager.cs` | 游戏状态机、计分、背景拉伸、摄像机边界 |
| `BattleCharacter.cs` | 左方角色（正手/反手/发球动画、移动、击球、网络远端渲染） |
| `AIController.cs` | 右方角色（AI逻辑、单机对手、联机时的客户端本地角色） |
| `Shuttlecock.cs` | 羽毛球物理（重力、阻力、碰撞）、本地物理主导 |
| `NetworkBattleSync.cs` | 联机战斗同步（二进制协议、快照缓冲、击球/挥拍/计分/暂停消息） |
| `NetworkSnapshotBuffer.cs` | 快照插值缓冲区（`BattleSnapshot`、`TrySample`） |
| `MultiplayerManager.cs` | 房间创建/加入、UDP广播发现、网络模拟器(Editor) |
| `NetworkLobbySync.cs` | 大厅同步 |
| `PauseMenu.cs` | 暂停菜单（主机权限控制） |
| `SoundManager.cs` | 音效/背景音乐 |
| `MobileInput.cs` | 键盘+触屏统一输入 |
| `TouchButton.cs` | 触屏按钮（IPointerDown/Up） |

### 场景对象
- Canvas → Screen Space - Camera, ScaleWithScreenSize(800×600, match=0.5)
- MultiplayerPanel → 中心锚定，z=0（之前z=-4797导致Free Aspect不可见）
- StartButton → 带ShowAfterAnimation开场动画控制
- OnlineBtn → TogglePanelButton 控制 MultiplayerPanel 显隐

---

## 网络架构（已验证的最佳方案）

### 协议：二进制 + 多消息类型
```
MsgState=1   → UnreliableSequenced (90Hz位置流)
MsgHit=2     → ReliableSequenced (击球)
MsgScore=3   → ReliableSequenced (计分，带落地位置和时间戳)  
MsgSwing=4   → ReliableSequenced (挥拍事件)
MsgPause=5   → ReliableSequenced (暂停)
```

### 同步模型（当前最佳）
- **人物**：`SmoothDamp(0.06s)` 追 legacy 字段（`sync.RemotePos`）
- **球**：**客户端本地物理主导**（`Shuttlecock.Update` 正常跑），网络仅做极慢 `SmoothDamp(0.3s)` 漂移校正
- **发送率**：`stateSendRate=90Hz`
- **NetworkDelivery**：State 用 `UnreliableSequenced`（丢弃旧包），Hit/Score/Swing 用 `ReliableSequenced`
- **Editor 模拟器**：`SetDebugSimulatorParameters(20, 10, 0)` — 延迟20ms 抖动10ms 丢包0%

### 关键设计决策
- **球本地物理 + 偶尔校正 > 网络位置追赶**：球速太快（50单位/秒），任何纯网络位置插值都会卡。两端跑的物理参数一样（重力35、阻力0.97、碰撞固定），轨迹自然一致。
- **`ServerListenAddress = "0.0.0.0"`**：不设这个 Unity Transport 默认只绑 127.0.0.1，局域网连不上。
- **W 挥拍事件**：远程端收到后触发 `SwingMe()`，本地动画播放 + 不执行真实击球。
- **H 击球事件**：收到后直接 `HitMe(speed, dir)` + 设位置，跳过本地物理判定。

### 计分延迟机制
Score 消息带 `eventTime`（主机落地时间）。客户端收到后 `QueueScoreSync`，等到 `eventTime + interpolationDelay` 才 `ApplyScoreSync`——球先落地、再计分，视觉上不分先后。

---

## 已废弃/失败的方案

| 方案 | 失败原因 |
|------|---------|
| 速度外推 | 时间戳同帧置零，外推时间恒为0 |
| 快照插值(100ms) | 球100ms延迟=5单位，发球"拉回"明显 |
| 纯Lerp | WiFi包到达间隔不均匀，目标跳动 |
| 纯SmoothDamp球(0.01s-0.04s) | 太低则卡，太高则延迟大 |
| 客户端跳过球物理 | 纯网络追位置，不够平滑 |

---

## Unity Editor 双开测试

### Editor 内联机
- 两个 Editor 同一台机器，一个 Host 一个 Client，自动发现通过 `IsLocalIP()` 检测同机后用 `127.0.0.1`
- **必须设不同 productGUID**（ProjectSettings/ProjectSettings.asset）
- Editor 内自动启用网络模拟器（`#if UNITY_EDITOR`），打包自动移除

### MCP Unity 连接
- 只能连一个 MCP（当前是 iPhone 项目）
- Clone 项目没有 MCP，文件修改需通过直接文件操作
- **不要用 PowerShell `Out-File` 处理含中文 UTF-8 文件**——会破坏编码。用 `git checkout` 或 Edit 工具。
- 当 `SetActiveInstance` 返回错误说 hash 不对——说明 clone 未连 MCP，直接走文件通路

### 两个项目同步
- iPhone (`iphone_game`) 和 Clone (`iphone_game_clone`) 大概率硬链接同文件
- 改 iPhone = 改 Clone，MD5 始终一致

---

## 打包

- Android：`manage_build target=android`，输出 `Builds/Android/火柴人打羽毛球2.apk`（~85MB IL2CPP）
- Windows：先切平台 `windows64`，`subtarget=player`（否则出两个 exe）
- 打包前 `EditorSceneManager.SaveOpenScenes()`
- APK 用 `Copy-Item` 到桌面
- 模拟器代码 `#if UNITY_EDITOR` 包裹，APK 自动排除

---

## 常见陷阱

1. **C# 文件中文编码**：只能用 Edit 工具或 git checkout 修改。PowerShell `Get-Content | Out-File -Encoding utf8` 100% 乱码。
2. **MultiplayerPanel z 轴**：必须为 0，非零值在 Screen Space - Camera + 不同宽高比下会被推到摄像机后。
3. **Free Aspect 不可见**：Screen Space - Camera 模式下宽高比极端时 Canvas 坐标偏移。
4. **`discoveredIP` 字段**：git 旧版没有，需手动加回。
5. **`GetLocalIP()` 方法**：git 旧版没有，需手动加回。
6. **`hitStartFrame`**：AIController 原来是 8，BattleCharacter 是 0——反手球拍激活延迟导致角度异常，统一为 0。
7. **`Application.targetFrameRate`**：90fps，git 旧版是 60。
8. **发球时误触**：`SwingMe()` 在 `!serving && !isInPlay` 时直接 return，防止 PointScored 过渡期浪费点击。

---

## AI 参数调优要点

- AI 回防时速度 ×0.85（不要太慢）
- 追球时近距直接追球位、远距看 `estimatedLandingX`
- 跳跃需 `ballApproaching` 条件（球朝自己飞）
- 挥拍冷却 ×0.6、范围 ×1.3
- `aiSkillLevel` 影响反应延迟和瞄准误差
