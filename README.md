# Multiplayer-Assistant
Multiplayer Assistant 是一个用于《星露谷物语》的 SMAPI Mod，为多人联机提供辅助功能。

## 最新更新

### v1.1.0 - 兼容Stardew Valley 1.6更新 (2024)
- **修复API兼容性问题**: 
  - 修复 `Game1.player.maxStamina` NetInt 类型转换错误
  - 修复 `buildStructure` 方法调用参数错误
  - 修复 `Game1.log` 访问权限问题，改用 Console.WriteLine
- **更新作物系统API调用**:
  - `crop.RegrowAfterHarvest` 改为从 `cropData.RegrowDays` 获取
  - `crop.netSeedIndex` 改为 `crop.indexOfHarvest` (返回string类型)
  - 添加 GetCropRegrowDays 辅助方法
  - 修复 WhichForageCrop 赋值时的类型转换 (使用 int.Parse)
- **修复日志记录方法调用**:
  - 移除 monitor.Debug 的 LogLevel 参数
  - 修复 CurrentPlayerLimit 属性访问
- **修复反射调用兼容性**:
  - LoadGameMenu.FindSaveGames 方法签名已更改
  - 添加参数数量检测和自适应调用逻辑
- **使用 ModData 系统替代 ReadyCheck**:
  - 原有的 ReadyCheck 类在 1.6 中已被移除
  - 使用 Farmer.modData 存储玩家准备状态
  - 修复主机睡觉无限循环问题
  - 主机现在正确跟踪自己的准备状态
  - 保持原有接口不变，确保兼容性
- **优化主机睡觉机制**:
  - 主机（ServerBot）不再显示睡觉对话框
  - 主机自动响应睡觉请求
  - 添加详细的调试日志以便诊断问题
  - 主机持续检查所有玩家的准备状态
  - 修改主机自动睡觉时间从 2:30 AM 改为 1:50 AM
- **修复客户端睡觉状态检测**:
  - 主机现在能正确检测到客户端玩家的原生睡觉状态
  - 兼容未安装 MOD 的客户端玩家
  - 使用游戏原生的 isInBed 和 timeWentToBed 状态
  - 不再依赖客户端安装相同的 MOD
- **修复主机睡觉响应机制**:
  - 主机现在会创建 ReadyCheckDialog 并在2秒后自动确认
  - 确保客户端的"等待其他玩家"对话框能正确接收主机的响应
  - 解决主机黑屏但客户端仍在等待的问题
- **增强睡觉流程调试**:
  - 添加多种睡觉触发方式的尝试（doSleep, NewDay, 时间设置）
  - 详细的错误日志和失败回退机制
  - 自动检测并报告 doSleep 方法的可用性
  - 兼容 Stardew Valley 1.6 的 API 变化
- **修复睡觉无限循环问题**:
  - 添加睡觉执行状态锁定，防止重复调用
  - 限制每天最多尝试 3 次睡觉
  - 每日开始自动重置尝试计数器
  - 异步检测睡觉是否成功，失败时尝试备用方案
- **注意**: 在1.6中无法直接修改作物的 RegrowDays 属性

### v1.0.0 - MonitorExtensions 优化
- **适配最新SMAPI API**: 完全兼容最新版本的SMAPI日志记录API
- **新增Alert日志级别**: 支持Alert级别日志记录，用于需要玩家关注的重要信息
- **详细日志功能增强**: 
  - 新增`IsVerboseEnabled()`方法检查详细日志状态
  - 新增`VerboseLogConditional()`方法进行条件性详细日志记录，避免不必要的性能开销
- **服务器机器人专用日志方法**: 
  - `ServerBotInfo()`: 记录服务器机器人信息
  - `ServerBotError()`: 记录服务器机器人错误
  - `ServerBotException()`: 记录服务器机器人异常
  - `MultiplayerDebug()`: 记录多人游戏调试信息
- **性能优化**: 详细日志记录使用延迟执行，仅在启用时才进行字符串格式化

## 特性

- **日志记录系统**: 完整的日志扩展方法，支持上下文标签和统一格式
- **服务器机器人支持**: 专为多人游戏房主(主机)设计的服务器机器人功能
- **详细日志控制**: 智能的详细日志记录，避免性能损耗

## 安装要求

- SMAPI 3.9.0 或更高版本
- .NET 6.0 运行时
- 《星露谷物语》1.5.2 或更高版本
