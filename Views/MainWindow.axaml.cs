using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia;
using System.Linq;
using JobTracker.ViewModels;

namespace JobTracker.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void StatusSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            try
            {
                await vm.StatusChangedAsync();
            }
            catch (System.Exception ex)
            {
                vm.GmailStatusMessage = ex.Message;
            }
        }
    }

    private async void ImportFromExcel(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var dialog = new OpenFileDialog
        {
            Title = "Import Excel file",
            AllowMultiple = false,
            Filters =
            {
                new FileDialogFilter { Name = "Excel", Extensions = { "xlsx", "xls" } },
                new FileDialogFilter { Name = "All Files", Extensions = { "*" } }
            }
        };

        var result = await dialog.ShowAsync(this);
        var filePath = result?.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(filePath))
            return;

        await vm.ImportFromExcelAsync(
            filePath,
            vm.ImportCompanyHeader,
            vm.ImportStatusHeader,
            vm.ImportProceedLabel,
            vm.ImportPendingLabel,
            vm.ImportRejectedLabel);
    }

    private async void ConfirmClearAll(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var dialog = new Window
        {
            Title = "Confirm clear",
            Width = 360,
            Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var message = new TextBlock
        {
            Text = "Are you sure you want to clear all applications?",
            Margin = new Thickness(16)
        };

        var confirmButton = new Button { Content = "Clear", Width = 100, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
        var cancelButton = new Button { Content = "Cancel", Width = 100, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };

        var buttons = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(16, 0, 16, 16),
            Children = { cancelButton, confirmButton }
        };

        var layout = new StackPanel
        {
            Children = { message, buttons }
        };

        dialog.Content = layout;

        confirmButton.Click += async (_, __) =>
        {
            dialog.Close();
            await vm.ClearAllAsync();
        };

        cancelButton.Click += (_, __) => dialog.Close();

        await dialog.ShowDialog(this);
    }
}
