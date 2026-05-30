# 项目开发指南

## 项目概述

基于 **Unity 2022.3** 的 2D 羽毛球对战游戏，仿"Stick Figure Badminton 2"。支持人机对战、双人对打、LAN 联机。

## 场景结构

| 场景 | 用途 |
|------|------|
| `SampleScene` | 主菜单：Start、Two Player、Online、调整操控按钮 |
| `CharacterSelect` | 选人界面，左右各选一个角色 |
| `Battle Background` | 战斗场景，含触屏按钮和游戏逻辑 |

## 核心系统

### 角色系统

- **BattleCharacter.cs** — 左边球员（主机控制），用 WASD/S 移动跳跃挥拍
- **AIController.cs** — 右边球员（AI 或客户端控制），用方向键/触屏操作
- 两者共享相同的挥拍物理、动画、碰撞检测逻辑
- `isNetworkRemote` 标志控制是否远程同步
- `[ExecuteAlways]` + `OnDrawGizmos` 可在 Scene 视图可视化调整参数

### 挥拍系统

- 正手（Overhead）：球在黄色分界线以上，顺时针弧线
- 反手（Underhand）：球在分界线以下，逆时针弧线
- 发球（Serve）：特定动画帧触发击球
- 击球检测：圆碰撞体 + 路径扫描（CheckSweptHit）
- 弧线可视化：GripMarker 子物体可在 Scene 视图拖动调整

### 羽毛球物理

- 手动物理（Shuttlecock.Update）：dt 驱动，不依赖 Unity Physics
- 重力 + 空气阻力 + 墙壁反弹 + 天花板反弹 + 网反弹
- 触地即得分，反弹仅视觉效果
- 预测落点（SimulateLandingX）供 AI 使用

### 输入系统

- `MobileInput` 静态类统一键盘+触屏输入
- `TouchButton` 圆形触屏按钮（Left/Right/Jump/Swing）
- 键盘：WASD 移动跳跃、S/Space 挥拍
- `TouchButtonLayout`：编辑模式拖动位置、+/-调大小、双指捏合缩放
- `TouchLayoutLoader`：战斗场景按钮启动时从 PlayerPrefs 加载布局

### UI 系统

- **开场动画**：`ShowAfterAnimation` 管理 CanvasGroup 显隐，动画播完 `ShowAll()`
- **暂停**：`PauseMenu` — 主机权威控制，M 消息同步暂停状态到客户端，客户端点击弹 Toast
- **联机面板**：`TogglePanelButton` 开关面板，`ModalPanel` 全屏遮罩阻止误触
- **按钮互斥**：`SceneSwitcher.disableInNetwork` + `requireConnection` 控制单机/联机按钮可点性

### 跨场景数据

`CharacterSelection`（静态类）：
- `LeftSprite` / `RightSprite`：选中的角色图片
- `TwoPlayerMode`：是否双人模式
- `SkipIntro`：返回主页时跳过开场动画
- `Clear()`：返回主页时重置

### 音效

`SoundManager` 管理 8 个 MP3（来自原版 Flash 游戏，需 ffmpeg 转码）：
- birdiehit, whoosh, ding, cheer, sigh, shotgun, background, menu

### AI 系统

- `aiControlled` 标志控制是否 AI
- AI 参数：`aiSkillLevel`（0~1）、`aiReactDelay`、`aiReactDistance`
- 策略：预测落点 → 移动到位 → 跳跃 → 挥拍
- 联机时 AI 关闭（`aiControlled = false`）

## 常见操作

### 添加新按钮到主菜单

1. 在 Canvas 下创建 GameObject，加 Image + Button + CanvasGroup
2. 如需开场动画控制：加入 `ShowAfterAnimation.hideDuringIntro` 数组
3. 如需联机/单机互斥：挂 `SceneSwitcher`，设 `disableInNetwork` 或 `requireConnection`

### 修改挥拍参数

- 在 Scene 视图选中左边/右边球员 → Inspector 调整 public 字段
- GripMarker 子物体可拖动调整握拍位置和弧线

### 调整羽毛球物理

- Scene 视图可直接看到地面、墙壁、天花板、网的 Gizmo 线
- 调整 `Shuttlecock` 组件上的 public 参数

### 联机调试

- F1~F6 快捷键在 GameManager.Update 中
- Console 过滤关键标签看日志
- 两个项目窗口分别对应 Host 和 Client

## 重要约定

1. **不要用 Unity 物理**：球和角色都是手动 Update 物理
2. **不要用 NetworkBehaviour**：全用 CustomMessagingManager 命名消息
3. **UDP 不可靠**：关键消息（H 事件、S 消息）都要有周期性冗余备份
4. **PlayerPrefs 持久化**：触屏按钮布局通过 PlayerPrefs 保存
5. **DontDestroyOnLoad**：NetworkLobbySync、NetworkBattleSync、MultiplayerManager
6. **Scene 视图可视化**：Ball/Character 的 Gizmo 在 Editor 下可见，改动后记得保存场景
