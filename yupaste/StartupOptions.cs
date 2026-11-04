namespace Yupaste;

public sealed record StartupOptions(string Host, int Port, int IdleTimeoutMinutes)
{
    public const int DefaultPort = 5005;
    public const int DefaultIdleTimeoutMinutes = 15;
}
