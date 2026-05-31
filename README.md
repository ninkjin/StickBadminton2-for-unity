
<p align="center">
  <img src="Assets/picture/Characterpic/player1_idle_0.png" width="120" alt=""/>
</p>

<h1 align="center">Stick Badminton 2 / 火柴人打羽毛球2</h1>

<p align="center">A Unity 2D badminton game with AI and LAN multiplayer<br/>支持单机AI和局域网联机的2D羽毛球对战游戏</p>

---

**English** | [中文](#中文)

## Gameplay

- **Single Player**: Click "开始游戏" to play against AI
- **Local 2P**: Click "双人对战" for same-device two-player
- **LAN Multiplayer**: One player creates a room, the other joins by entering the host's IP

### Controls

| Key | Action |
|-----|--------|
| A / ← | Move Left |
| D / → | Move Right |
| W / ↑ | Jump (double jump available) |
| S / Space | Swing / Serve |

First to 7 points wins.

## Tech Stack

- **Engine**: Unity 2022.3.17f1c1
- **Networking**: Unity Netcode for GameObjects + Unity Transport (UDP)
- **Platforms**: Android 8.0+, Windows 10+
- **Language**: C#
- **Build Size**: ~85 MB (Android IL2CPP)

### Network Architecture

- Host-authoritative model: server runs ball physics, client runs local physics for visual smoothness
- Binary protocol: state sync at 90Hz (UnreliableSequenced), hits/scores (ReliableSequenced)
- UDP broadcast for LAN room discovery
- Built-in network simulator in Editor (20ms delay + 10ms jitter), stripped from builds

### Network Sync Model

- **Characters**: SmoothDamp (0.06s) toward authoritative position
- **Birdie**: Client-side local physics dominant + ultra-slow SmoothDamp (0.3s) drift correction
- **Score**: Delayed application with event timestamp, birdie visually lands before score triggers

## Project Structure

```
Assets/
├── Scenes/                    # SampleScene (Menu), CharacterSelect, Battle Background
├── Scripts/
│   ├── GameManager.cs           # Game state machine, scoring, camera setup
│   ├── BattleCharacter.cs       # Left player (animations, input, hitting, remote rendering)
│   ├── AIController.cs          # Right player (AI / human, client-side local character)
│   ├── Shuttlecock.cs           # Birdie physics (gravity, air resistance, collisions)
│   ├── NetworkBattleSync.cs     # Battle sync protocol (binary, 5 message types)
│   ├── NetworkSnapshotBuffer.cs # Snapshot interpolation buffer
│   ├── MultiplayerManager.cs    # Room management, UDP discovery, network simulator
│   └── ...                      # Audio, touch controls, UI helpers
├── Sprites/                   # Stick figure animation frames
├── picture/                   # Character portraits
└── Resources/                 # Audio assets
```

## Building

### Android APK
```
File → Build Settings → Android → Switch Platform → Build
```
Output: `Builds/Android/火柴人打羽毛球2.apk`

### Windows
```
File → Build Settings → Standalone Windows → Player subtarget → Build
```

## License

MIT

---

<h2 id="中文">中文</h2>

## 玩法

- **单机模式**：点击"开始游戏"，与 AI 对战
- **双人同屏**：点击"双人对战"
- **局域网联机**：一人点"创建房间"，另一人输入对方 IP 加入

### 操作

| 按键 | 动作 |
|------|------|
| A / 左按钮 | 左移 |
| D / 右按钮 | 右移 |
| W / 跳按钮 | 跳跃（可二段跳） |
| S / 挥拍按钮 | 挥拍/发球 |

先得 7 分者获胜。

## 技术说明

### 联机架构

- 主机权威模式：主机运行球物理，客户端本地物理辅助显示
- 二进制网络协议：状态同步 90Hz (UnreliableSequenced)，击球/计分 (ReliableSequenced)
- UDP 广播自动发现局域网房间
- Editor 内置网络模拟器（延迟 20ms / 抖动 10ms），打包自动移除

### 同步方案

- **人物**：SmoothDamp 弹簧阻尼（0.06s）追权威位置
- **球**：客户端本地物理主导 + 极慢 SmoothDamp（0.3s）漂移校正
- **计分**：带时间戳延迟应用，球先落地、后计分

### Editor 双开测试

两个 Unity Editor 同一台机器可对联：
1. 确保两个项目 `productGUID` 不同
2. 一个创建房间，另一个留空 IP 加入
3. Editor 内自动注入网络模拟

## 构建

### Android APK
```
File → Build Settings → Android → Switch Platform → Build
```

### Windows
```
File → Build Settings → Standalone Windows → Player 子目标 → Build
```

## 开源协议

MIT
