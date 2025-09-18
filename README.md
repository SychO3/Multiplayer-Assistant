# Multiplayer-Assistant
Multiplayer Assistant 是一个用于《星露谷物语》的 SMAPI Mod，为多人联机提供辅助功能。

## 最新更新 (v1.1.0)

### 代码优化和日志增强
- **全面日志系统**：所有功能模块都已添加详细的 DEBUG 日志记录，便于调试和问题排查
- **中文注释**：所有代码关键部分都添加了中文注释，提高代码可读性
- **统一日志框架**：使用 `MonitorExtensions` 模块提供统一的日志输出格式和上下文标签
- **最新 API 兼容**：完全基于 apis_en 文档中的最新 API，确保与 Stardew Valley 1.6 的兼容性

### 修复的模块
1. **ModEntry**：主入口模块已优化，添加了完整的日志记录
2. **Chat 模块**：EventDrivenChatBox 现在包含详细的事件日志
3. **MessageCommands 模块**：
   - BuildCommandListener：建造命令监听器
   - DemolishCommandListener：拆除命令监听器  
   - PauseCommandListener：暂停命令监听器
   - ServerCommandListener：服务器命令监听器
4. **HostAutomatorStages 模块**：
   - AutomatedHost：自动化主机功能
   - ProcessDialogueBehaviorLink：对话处理行为链
5. **Crops 模块**：CropSaver 作物保护器功能
6. **Utils 模块**：各种实用工具类

### 日志使用说明
- 默认启用 DEBUG 级别日志，可通过 SMAPI 控制台查看
- 日志格式：`[MultiplayerAssistant][模块名] 日志内容`
- 可通过 `MonitorExtensions.SetLoggingEnabled(false)` 关闭日志

## 兼容 Stardew Valley 1.6 的重要说明

- 本 MOD 已完成对 SDV 1.6 的初步兼容并成功通过 Release 构建。
- 以下为本次适配的关键变更与注意事项。

### 主要变更

- 作物保护器（`MultiplayerAssistant/Crops/CropSaver.cs`）
  - 移除对旧字段的直接访问：`Crop.seasonsToGrowIn`、`regrowAfterHarvest`、`GameLocation.GetSeasonForLocation()`。
  - 兼容 1.6 的数据类型变化：
    - `RowInSpriteSheet` 改为 string 存储。
    - `WhichForageCrop` 改为 string 存储。
    - `PhaseDays` 改为 `List<string>` 存储。
  - 移除 `Game1.savePathOverride`，统一采用标准存档路径。
  - 启用“位置上下文季节”判定：优先使用 `Game1.GetSeasonForLocation(location)`（反射），姜岛兜底为 `summer`，否则回退 `Game1.currentSeason`。

- 开荒与菜单流程
  - `StartFarmStage.cs`：移除对只读 `catPerson` 的赋值；`whichPetBreed` 改为写入 string；`CurrentPlayerLimit` 去掉 `.Value`；用 `gainExperience(3, 20000)` 设置采矿等级到 10。

- 节日/睡觉就绪流程
  - `TransitionFestivalAttendanceBehaviorLink.cs`、`TransitionFestivalEndBehaviorLink.cs`、`TransitionSleepBehaviorLink.cs`：去除 `FarmerTeam.SetLocalReady/GetNumberReady` 调用，改由 `ReadyCheckDialog` 驱动就绪状态。
  - `Utils/Festivals.cs`、`Utils/Sleeping.cs`：删除 `GetNumberReady(...)` 依赖并采用保守判定；`Utility.isFestivalDay` 第二参数改为 `Game1.season`（枚举）。

- 事件/任务 ID 统一字符串化
  - 涉及：`PurchaseJojaMembershipBehaviorLink.cs`、`UnlockCommunityCenterBehaviorLink.cs`、`EndCommunityCenterBehaviorLink.cs`、`GetFishingRodBehaviorLink.cs`、`ReadyCheckHelper.cs`。

- 建筑相关命令
  - `DemolishCommandListener.cs`：使用像素坐标换算瓦片坐标；移除对 `Cabin.farmhand`、`Chest.items`、`BluePrint` 的直接依赖；保留 Shipping Bin 保护与 Cabin 二次确认。
  - `BuildCommandListener.cs`：已按 apis_en 的“官方木匠流程（CarpenterMenu）”恢复建造功能，会为玩家自动打开木匠铺界面，你可选择目标蓝图并在面前一格处放置。

- 对话自动应答
  - `ProcessDialogueBehaviorLink.cs`：通过反射读取 `DialogueBox.responses`，恢复“蘑菇/蝙蝠”和宠物“是/否”的自动应答。

### 建造指令使用说明（1.6）

- 支持的命令（私聊机器人）：
  - `build stone_cabin`
  - `build plank_cabin`
  - `build log_cabin`
- 机器人会自动打开 `CarpenterMenu("Robin", ScienceHouse)`，并通过聊天提示你选择相应蓝图并放置。
- 建议在白天、非节日、非剧情/对话时使用；确保有建造权限与足够资源。

### 已知事项与后续计划

- `BuildCommandListener` 已恢复（CarpenterMenu 官方流程）。若在个别场景无法自动打开菜单，聊天中会提示你手动前往 Robin 选择目标蓝图。
- `CropSaver` 的季节判断将升级为“位置上下文季节”，以替代当前的 `Game1.currentSeason`。
- 关键路径已补充中文注释；如需更详细的 DEBUG 日志，可开启并查看控制台/日志文件。

## 更新日志

### 1.1.0 新增
- 自动查看并阅读未读邮件（AutoMailService）
  - 功能：在 `SaveLoaded` 与 `DayStarted` 自动检查未读与预定邮件，并逐封打开 `LetterViewerMenu` 阅读，短暂停留后自动关闭，直到处理完成。
  - 配置：`EnableAutoOpenUnreadMail`（默认 `true`）。
  - 实现：仅使用公开 API（`Farmer.mailbox` / `mailReceived` / `mailForTomorrow`），未使用 Harmony；通过 `Display.MenuChanged` 排队依次阅读；日志统一用 `MonitorExtensions`。
- 每天开始自动移动到农舍后方（AutoPositionService）
  - 功能：在 `DayStarted` 将玩家移动到 `Farm (64,10)`，减少干扰并便于自动化交互。
  - 配置：无（始终启用）。
  - 实现：公开 API（`Game1.getLocationFromName("Farm")`、`Warp`、`Game1.player.warpFarmer(warp)`），日志统一用 `MonitorExtensions`。
 - 自动跳过节日/动画/剧情（AutoSkipService）
   - 功能：在检测到会冻结时间的事件（`Game1.eventUp` 且存在 `currentEvent`）时，自动调用 `skipEvent()` 跳过；若出现阻塞性的对话菜单（`DialogueBox`），尝试优雅关闭。
   - 配置：无（始终启用）。
   - 实现：仅使用公开 API（`Event.skipEvent()`、`IClickableMenu.exitThisMenu()`），未使用 Harmony；日志统一用 `MonitorExtensions`。
 - 主机保活（HostKeepAliveService）
   - 功能：持续将主机生命值与体力保持为最大，避免无人值守时倒下或卡死。
   - 配置：无（始终启用，仅主机执行）。
   - 实现：公开 API（`Game1.player.health/maxHealth`、`Game1.player.stamina/maxStamina`），日志统一用 `MonitorExtensions`。
 - 事件驱动聊天封装（Chat/EventDrivenChatBox, Chat/ChatEventArgs）
   - 功能：将 `ChatBox` 封装为事件与一次性响应组（关键词 -> 回调），更易于在主机侧实现基于聊天的交互与命令确认。
   - API：基于游戏公开 API（重写 `ChatBox.receiveChatMessage(...)`），不使用 Harmony；全程使用 `MonitorExtensions` 输出 DEBUG 日志。
   - 使用：
     - 订阅事件：`EventDrivenChatBox.ChatReceived += (s, e) => { /* 处理 e.Message 等 */ };`
     - 注册响应组：`RegisterFarmerResponseActionGroup(farmerId, new Dictionary<string, Action?> { ["yes"]=OnYes, ["no"]=OnNo });` 任意一个被触发后整组会被移除避免重复触发。
- 时间控制命令（TimeControlService）
  - 功能：提供 `ma:time` 控制台命令，支持设置/前进游戏时间：`ma:time set <HHmm>`、`ma:time add <minutes>`（按 10 分钟步进优先调用 `Game1.performTenMinuteClockUpdate()`）。
  - 配置：无（始终注册）。
  - 实现：公开 API（`Game1.timeOfDay`、`Game1.performTenMinuteClockUpdate()`），日志统一用 `MonitorExtensions`。

### 1.0.0 安全性与性能优化（AutoHostService）
- 优化事件订阅生命周期：
  - 在 `Initialize()` 中订阅 `GameLoop.UpdateTicked`，确保首次进入标题界面可触发自动流程。
  - 在自动流程于 `OnUpdateTickedTitleStage()` 触发后，立即取消订阅 `UpdateTicked`，减少每帧调用开销。
  - 在 `OnReturnedToTitle()` 重新订阅 `UpdateTicked`，确保返回标题界面后可再次触发自动流程。
- 更安全、更高效的存档匹配：
  - `TryFindSaveSlotByFarmName()` 改为使用流式 `XmlReader` 解析，避免整文件读取，降低内存占用并提高健壮性。
  - 打开 `SaveGameInfo` 文件时采用 `FileShare.ReadWrite`，减少与游戏/其他进程的 IO 争用。
  - 农场名比较改为不区分大小写（`OrdinalIgnoreCase`），避免大小写差异导致匹配失败。
- 清理：移除未使用的 `using System.Linq;`。

#### 新增功能
- 存档加载并确认主机进入后，自动将玩家移动到农舍后方（`Farm` 地图坐标 `64,10`），减少对玩家/场景的干扰，同时保留交互能力。

#### 修复
- 为避免编译错误（`StardewValley.Locations.Farm` 类型在目标引用下不可用），传送实现改为使用 `GameLocation`（`NameOrUniqueName`）而非直接依赖 `Farm` 类型。

如需进一步减少日志或调整加载后兜底检查逻辑，请参考 `AutoHostService` 内部注释。

