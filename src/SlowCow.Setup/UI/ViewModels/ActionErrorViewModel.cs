namespace SlowCow.Setup.UI.ViewModels;

internal class ActionErrorViewModel : ViewModelBase
{
    public required string Message { get; set; }
    public string Details { get; set; } = string.Empty;
    public string? StackTrace { get; set; } = string.Empty;

    public static ActionErrorViewModel? DesignInstance { get; } = new () {
        Message = "This is a design mode error example",
        Details = "Some details will be here (with error code)",
        StackTrace = null,
    };
}
