using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using MsBox.Avalonia;
using SlowCow.Setup.UI.ViewModels;
namespace SlowCow.Setup.UI.Views;

public partial class ActionErrorView : UserControl
{
    public ActionErrorView()
    {
        InitializeComponent();
    }

    private ActionErrorViewModel ViewModel => (ActionErrorViewModel) DataContext!;

    protected override void OnDataContextChanged(EventArgs e)
    {
        #if DEBUG
        if (!string.IsNullOrWhiteSpace(ViewModel.StackTrace))
            DetailsButton.IsVisible = true;
        #endif

        base.OnDataContextChanged(e);
    }

    private void ShowDetails_OnClick(object? sender, RoutedEventArgs e)
    {
        // show message box
        MessageBoxManager
            .GetMessageBoxStandard("Error Details", ViewModel.StackTrace)
            .ShowWindowDialogAsync(this.FindAncestorOfType<Window>());
    }
}
