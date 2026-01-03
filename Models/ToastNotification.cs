namespace KopioRapido.Models;

public class ToastNotification
{
    public string Message { get; set; } = string.Empty;
    public ToastType Type { get; set; } = ToastType.Info;
    public int DurationSeconds { get; set; } = 4;
}

public enum ToastType
{
    Info,
    Success,
    Warning,
    Error
}
