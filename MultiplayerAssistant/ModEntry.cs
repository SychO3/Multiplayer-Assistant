using HarmonyLib;
using StardewModdingAPI;

namespace MultiplayerAssistant;

public class ModEntry : Mod
{   
    private Harmony? _harmony;
    private ModConfig _config = new();
    private AutoHostService? _autoHost;
    private AutoMailService? _autoMail;
    private TimeControlService? _timeControl;
    private AutoPositionService? _autoPosition;
    private AutoSkipService? _autoSkip;
    private HostKeepAliveService? _keepAlive;

    public override void Entry(IModHelper helper)
    {
        // 初始化 Harmony 并应用补丁（如果有）
        this._harmony = new Harmony(this.ModManifest.UniqueID);
        this._harmony.PatchAll();

        // 读取配置（不存在会自动创建）并控制日志输出
        this._config = helper.ReadConfig<ModConfig>();
        MonitorExtensions.SetDefaultPrefix(this.ModManifest.Name);
        MonitorExtensions.SetLoggingEnabled(this._config.EnableDebugLogs);

        if (MonitorExtensions.IsLoggingEnabled)
            this.Monitor.Info("调试日志已开启。", "Config");

        // 自动建房服务（受配置控制）
        if (this._config.EnableAutoHost)
        {
            this._autoHost = new AutoHostService(helper, this.Monitor, this._config);
            this._autoHost.Initialize();
            this.Monitor.Info("自动建房功能：已启用", "Config");
        }
        else
        {
            this.Monitor.Info("自动建房功能：已禁用", "Config");
        }

        // 自动打开并阅读未读邮件（受配置控制）
        if (this._config.EnableAutoOpenUnreadMail)
        {
            this._autoMail = new AutoMailService(helper, this.Monitor, this._config);
            this._autoMail.Initialize();
            this.Monitor.Info("自动打开并阅读未读邮件：已启用", "Config");
        }
        else
        {
            this.Monitor.Info("自动打开并阅读未读邮件：已禁用", "Config");
        }

        // 注册时间控制命令（始终可用）
        this._timeControl = new TimeControlService(helper, this.Monitor);
        this._timeControl.Initialize();
        this.Monitor.Info("时间控制命令：已注册 (ma:time)", "Config");

        // 每天开始时自动移动到农舍后方（始终启用）
        this._autoPosition = new AutoPositionService(helper, this.Monitor);
        this._autoPosition.Initialize();
        this.Monitor.Info("每日自动定位：已启用 (Farm: 64,10)", "Config");

        // 自动跳过节日/动画/剧情（始终启用）
        this._autoSkip = new AutoSkipService(helper, this.Monitor);
        this._autoSkip.Initialize();
        this.Monitor.Info("自动跳过节日/剧情：已启用", "Config");

        // 主机保活：无限体力/生命（始终启用，仅主机执行）
        this._keepAlive = new HostKeepAliveService(helper, this.Monitor);
        this._keepAlive.Initialize();
        this.Monitor.Info("主机保活：已启用（HP/SP 保持最大）", "Config");

        this.Monitor.Debug("入口初始化完成。", "Lifecycle");
    }
}

