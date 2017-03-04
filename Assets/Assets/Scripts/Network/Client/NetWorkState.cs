using System.ComponentModel;

/// <summary>
/// 网络状态
/// </summary>
public enum enNetWorkState
{
    [Description("Connecting server")]
    Connecting,

    [Description("Server connected")]
    Connected,

    [Description("Disconnected with server")]
    Disconnected,
}