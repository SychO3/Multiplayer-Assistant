namespace MultiplayerAssistant.Services
{
    /// <summary>
    /// 简易服务汇聚器：用于在少量场景下跨模块访问共享服务实例。
    /// </summary>
    internal static class ServiceHub
    {
        public static ConfirmationTimeoutService ConfirmationTimeout { get; set; }
    }
}


