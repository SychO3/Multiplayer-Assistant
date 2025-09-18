# Multiplayer-Assistant
Multiplayer Assistant 是一个用于《星露谷物语》的 SMAPI Mod，为多人联机提供辅助功能。

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

