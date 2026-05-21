using System;
using CommunityToolkit.WinUI.UI.Controls;
using EventLookUno.Models;
using EventLookUno.Presentation;
using EventLookUno.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace EventLookUno;

public sealed partial class MainPage : Page
{
    private MainViewModel? _vm;

    public MainPage()
    {
        this.InitializeComponent();
        Loaded += OnPageLoaded;
    }

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        if (_vm is not null)
            return;

        // Service-locator pull (Slot 1 §17 pattern). Keeps ViewModel wiring out of XAML and
        // avoids requiring a DI-aware Page factory. Promoted App.Host is `public`.
        var app = Application.Current as App;
        var services = app?.Host?.Services;
        if (services is null)
            return;

        var source = services.GetRequiredService<IEventSource>();
        var options = services.GetRequiredService<IOptions<EventLookSettings>>();

        _vm = new MainViewModel(source, options);
        DataContext = _vm;

        await _vm.OnLoadedAsync();
    }

    private void OnRefreshAccelerator(object sender, object e)
    {
        if (_vm is { } vm && vm.RefreshCommand.CanExecute(null))
            vm.RefreshCommand.Execute(null);
    }

    private void OnExitClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            Microsoft.UI.Xaml.Application.Current.Exit();
        }
        catch
        {
            // Application.Exit() is NotImplemented on WASM — browser tabs can't self-close.
            // (Slot 1 §3 known gotcha.)
        }
    }

    /// <summary>
    /// LoadingRow is how WinUI DataGrid exposes per-row styling at render time.
    /// Replaces the WPF <c>&lt;Style TargetType="DataGridRow"&gt;&lt;Style.Triggers&gt;&lt;DataTrigger&gt;</c>
    /// pattern, which doesn't exist in WinUI — styles are setter-only.
    /// </summary>
    private void OnLoadingRow(object? sender, DataGridRowEventArgs e)
    {
        if (e.Row.DataContext is EventEntry entry)
        {
            e.Row.Background = entry.IsNewLoaded
                ? new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0x40, 0xFF, 0xEB, 0x3B))
                : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }
    }
}
