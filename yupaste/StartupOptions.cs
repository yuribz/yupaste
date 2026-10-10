namespace Yupaste;

public sealed record StartupOptions(string Host, int Port)
{
    public const int DefaultPort = 5005;
}
