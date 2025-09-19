# Multiplayer-Assistant
Multiplayer Assistant 是一个用于《星露谷物语》的 SMAPI Mod，为多人联机提供辅助功能。

## 最新更新

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
