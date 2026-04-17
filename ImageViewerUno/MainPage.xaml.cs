using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ImageViewerUno.Services;
using Microsoft.Extensions.Options;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.System;

namespace ImageViewerUno;

public sealed partial class MainPage : Page
{
    [Flags]
    private enum Modi
    {
        Normal = 1,
        Zoom = 2,
        Slideshow = 4,
        HelpShown = 8
    }

    // settings
    private string _applicationTitle = "Uno Image Viewer";
    private string _backgroundColor = "Black";
    private string _msgColor = "LimeGreen";
    private string _includedFileExtensions = ".bmp,.gif,.jpeg,.jpg,.png,.tif,.tiff,.webp";
    private string[] _fileExtensions = Array.Empty<string>();
    private double _msgFadeoutSeconds = 2.0;
    private double _zoomMin = 0.1;
    private double _zoomMax = 5.0;
    private double _zoomStep = 1.25;
    private int _imageDurationSeconds = 2;
    private bool _showHelpOnLoad;
    private bool _runAnimatedGifs = true;
    private bool _closeOnLostFocus;

    // file navigation
    private string? _directory;
    private List<string> _fileList = new();
    private int _currentFileIndex;

    // states
    private bool _isFileDialogOpen;
    private Modi _mode = Modi.Normal;
    private bool _isSlideshowRunning;
    private bool _isImageLoaded;

    // image dragging / zoom
    private Point _kv;
    private Point _origin;
    private Point _start;
    private double _maxX;
    private double _maxY;
    private double _mediaWidth;
    private double _mediaHeight;
    private bool _isDragging;

    private readonly DispatcherTimer _timerMessage = new();
    private readonly DispatcherTimer _timerSlideshow = new();

    private readonly Models.MediaViewModel _mediaViewModel = new();

    public MainPage()
    {
        ReadSettings();
        this.InitializeComponent();
        DataContext = _mediaViewModel;
        _mediaViewModel.PropertyChanged += OnMediaViewModelPropertyChanged;
        InitializeTransforms();
        _timerMessage.Tick += TimerMessage_Tick;
        _timerSlideshow.Tick += TimerSlideshow_Tick;
        ApplySettingsToView();
    }

    /// <summary>
    /// Constructor with settings read from appsettings.json for showing the page in another app.
    /// </summary>
    public MainPage(string folder) : this()
    {
        _directory = folder;
    }

    /// <summary>
    /// Constructor with explicit settings for showing the page in another app.
    /// Mirrors the WPF MainWindow overload.
    /// </summary>
    public MainPage(string folder, bool showHelpOnLoad, bool runAnimatedGifs, bool closeOnLostFocus,
        string backgroundColor, string msgColor, string includedFileExtensions, int imageDurationSeconds,
        double fadeoutSeconds, double zoomMin, double zoomMax, double zoomStep)
    {
        _directory = folder;
        _showHelpOnLoad = showHelpOnLoad;
        _runAnimatedGifs = runAnimatedGifs;
        _closeOnLostFocus = closeOnLostFocus;
        _backgroundColor = backgroundColor;
        _msgColor = msgColor;
        _includedFileExtensions = includedFileExtensions;
        _imageDurationSeconds = imageDurationSeconds;
        _msgFadeoutSeconds = fadeoutSeconds;
        _zoomMin = zoomMin;
        _zoomMax = zoomMax;
        _zoomStep = zoomStep;

        this.InitializeComponent();
        DataContext = _mediaViewModel;
        _mediaViewModel.PropertyChanged += OnMediaViewModelPropertyChanged;
        InitializeTransforms();
        _timerMessage.Tick += TimerMessage_Tick;
        _timerSlideshow.Tick += TimerSlideshow_Tick;
        ApplySettingsToView();
    }

    private void OnMediaViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Models.MediaViewModel.CurrentVisualState))
        {
            // Source used Blend behaviors (DataTrigger + GoToStateAction). The WinUI equivalent package
            // (Microsoft.Xaml.Behaviors.WinUI.Managed) is WinAppSDK-only, not Uno cross-platform. Handle
            // the VSM transition in code-behind for parity across Desktop / WASM / WinUI.
            VisualStateManager.GoToState(this, _mediaViewModel.CurrentVisualState.ToString(), true);
        }
    }

    private void ReadSettings()
    {
        try
        {
            var host = (Application.Current as App)?.Host;
            var options = host?.Services.GetService(typeof(IOptions<Models.ImageViewerSettings>)) as IOptions<Models.ImageViewerSettings>;
            var s = options?.Value;
            if (s is null) return;

            _applicationTitle = s.ApplicationTitle;
            _backgroundColor = s.BackgroundColor;
            _closeOnLostFocus = s.CloseOnLostFocus;
            _imageDurationSeconds = s.ImageDurationSeconds;
            _includedFileExtensions = s.IncludedFileExtensions;
            _msgColor = s.MsgColor;
            _msgFadeoutSeconds = s.MsgFadeoutSeconds;
            _runAnimatedGifs = s.RunAnimatedGifs;
            _showHelpOnLoad = s.ShowHelpOnLoad;
            _zoomMax = s.ZoomMax;
            _zoomMin = s.ZoomMin;
            _zoomStep = s.ZoomStep;
        }
        catch
        {
            // Fall back to defaults when DI isn't wired (e.g. design-time).
        }
    }

    private void ApplySettingsToView()
    {
        try { _fileExtensions = _includedFileExtensions.Split(','); }
        catch { _fileExtensions = new[] { ".bmp", ".gif", ".jpeg", ".jpg", ".png", ".tif", ".tiff", ".webp" }; }

        var bg = ResolveBrush(_backgroundColor) ?? new SolidColorBrush(Colors.Black);
        Background = bg;
        RootGrid.Background = bg;
        ImageBorder.Background = bg;

        var fg = ResolveBrush(_msgColor) ?? new SolidColorBrush(Colors.LimeGreen);
        MessageLabel.Foreground = fg;
    }

    private static Brush? ResolveBrush(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var prop = typeof(Colors).GetProperty(name, BindingFlags.Public | BindingFlags.Static);
        if (prop?.GetValue(null) is Windows.UI.Color c)
        {
            return new SolidColorBrush(c);
        }
        return null;
    }

    private void InitializeTransforms()
    {
        // MediaHost RenderTransform must exist before zoom/pan math runs.
        MediaHost.RenderTransformOrigin = new Point(0.5, 0.5);
        var tg = new TransformGroup();
        tg.Children.Add(new ScaleTransform());
        tg.Children.Add(new TranslateTransform());
        MediaHost.RenderTransform = tg;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is string arg && !string.IsNullOrWhiteSpace(arg))
        {
            // Launch arg may be an image or a directory path.
            _directory ??= Directory.Exists(arg) ? arg : Path.GetDirectoryName(arg);
        }

        // Page receives focus so KeyDown fires.
        this.Focus(FocusState.Programmatic);
    }

    private async void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        this.KeyDown += MainPage_KeyDown;

        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]) && (File.Exists(args[1]) || Directory.Exists(args[1])))
        {
            await SetImageAsync(Path.GetFullPath(args[1]));
        }
        else if (!string.IsNullOrEmpty(_directory))
        {
            await SetImageAsync(_directory);
        }
        else
        {
            _showHelpOnLoad = true;
        }

        if (_showHelpOnLoad)
        {
            ShowHelpText();
        }
    }

    /// <summary>
    /// Sets the current image. If imagePath is null, uses the file list's current index.
    /// If imagePath is a file, loads its directory and sets it as current.
    /// If imagePath is a directory, loads the directory and sets the first file.
    /// </summary>
    private async Task SetImageAsync(string? imagePath = null)
    {
        try
        {
            if (string.IsNullOrEmpty(imagePath))
            {
                if (_fileList.Count == 0) return;
                imagePath = _fileList[_currentFileIndex];

                _mediaWidth = _mediaHeight = 0;

                bool isWebp = imagePath.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);
                bool isGif = imagePath.EndsWith(".gif", StringComparison.OrdinalIgnoreCase)
                          || imagePath.EndsWith(".gifv", StringComparison.OrdinalIgnoreCase);

                if (isWebp)
                {
                    // TODO(Phase 7): decode WebP frames with SixLabors.ImageSharp into WriteableBitmap loop.
                    // v1: attempt BitmapImage (works for static WebP on some platforms, shows blank otherwise).
                    await LoadStillImageAsync(imagePath, SwitchableAnim);
                    _mediaViewModel.FilenameAnim = imagePath;
                    _mediaViewModel.FilenameImage = _mediaViewModel.FilenameMedia = null;
                    _mediaViewModel.CurrentVisualState = Models.VisualStates.ShowAnim;
                }
                else if (isGif && _runAnimatedGifs)
                {
                    // Animated GIFs render via WinUI Image natively; MediaPlayerElement is overkill here.
                    // Using SwitchableImage for GIFs aligns with WinUI's auto-animation.
                    await LoadStillImageAsync(imagePath, SwitchableImage);
                    _mediaViewModel.FilenameImage = imagePath;
                    _mediaViewModel.FilenameAnim = _mediaViewModel.FilenameMedia = null;
                    _mediaViewModel.CurrentVisualState = Models.VisualStates.ShowImage;
                }
                else
                {
                    await LoadStillImageAsync(imagePath, SwitchableImage);
                    _mediaViewModel.FilenameImage = imagePath;
                    _mediaViewModel.FilenameAnim = _mediaViewModel.FilenameMedia = null;
                    _mediaViewModel.CurrentVisualState = Models.VisualStates.ShowImage;
                }

                _isImageLoaded = true;
                ResetZoomLevel();
                ShowMessage(Path.GetFileName(imagePath));
            }
            else
            {
                if (File.Exists(imagePath))
                {
                    LoadFileList(Path.GetDirectoryName(imagePath) ?? string.Empty);
                    _currentFileIndex = Math.Max(0, _fileList.IndexOf(imagePath));
                    await SetImageAsync();
                }
                else if (Directory.Exists(imagePath))
                {
                    LoadFileList(imagePath);
                    _currentFileIndex = 0;
                    if (_fileList.Count > 0) await SetImageAsync();
                    else ShowMessage("No supported files in: " + imagePath);
                }
                else
                {
                    ShowMessage("Invalid path: " + imagePath);
                    _isImageLoaded = false;
                }
            }
        }
        catch (Exception ex)
        {
            ShowMessage($"Failed to load: {Path.GetFileName(imagePath)} — {ex.Message}");
            _isImageLoaded = false;
        }
    }

    private async Task LoadStillImageAsync(string path, Image target)
    {
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(path);
            using IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read);
            var bmp = new BitmapImage();
            await bmp.SetSourceAsync(stream);
            target.Source = bmp;
            _mediaWidth = bmp.PixelWidth;
            _mediaHeight = bmp.PixelHeight;
        }
        catch
        {
            // Fall back to URI-based load for platforms where StorageFile path access is limited.
            var uri = new Uri($"file:///{path.Replace('\\', '/')}");
            var bmp = new BitmapImage(uri);
            target.Source = bmp;
        }
    }

    private void LoadFileList(string directoryName)
    {
        if (string.IsNullOrWhiteSpace(directoryName) || !Directory.Exists(directoryName))
        {
            _fileList = new List<string>();
            return;
        }

        IEnumerable<string> source;
        if (_fileExtensions.Length == 0 || _fileExtensions[0] == "" || _fileExtensions[0] == "*")
        {
            source = Directory.EnumerateFiles(directoryName);
        }
        else
        {
            source = Directory.EnumerateFiles(directoryName)
                .Where(f => _fileExtensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)));
        }

        _fileList = source.OrderBy(x => x, NaturalStringComparer.Instance).ToList();
    }

    private async void MainPage_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        bool alt = e.KeyStatus.IsMenuKeyDown;

        if ((_mode & Modi.HelpShown) != 0 && !(alt == false && (e.Key == VirtualKey.H || e.Key == VirtualKey.I || e.Key == VirtualKey.F1)))
        {
            HelpPanel.Visibility = Visibility.Collapsed;
            _mode &= ~Modi.HelpShown;
            e.Handled = true;
            return;
        }

        if (!alt)
        {
            switch (e.Key)
            {
                case VirtualKey.H:
                case VirtualKey.I:
                case VirtualKey.F1:
                    ShowHelpText();
                    e.Handled = true;
                    break;

                case VirtualKey.Escape:
                    if ((_mode & Modi.Slideshow) != 0) StopSlideshow(2);
                    else TryCloseApp();
                    e.Handled = true;
                    break;

                case VirtualKey.Left:
                    if (_currentFileIndex > 0)
                    {
                        _currentFileIndex--;
                        ResetZoomLevel();
                        await SetImageAsync();
                    }
                    e.Handled = true;
                    break;

                case VirtualKey.Right:
                    if (_currentFileIndex < _fileList.Count - 1)
                    {
                        _currentFileIndex++;
                        ResetZoomLevel();
                        await SetImageAsync();
                    }
                    e.Handled = true;
                    break;

                case VirtualKey.Home:
                    if (_fileList.Count > 0 && _currentFileIndex > 0)
                    {
                        _currentFileIndex = 0;
                        ResetZoomLevel();
                        await SetImageAsync();
                    }
                    e.Handled = true;
                    break;

                case VirtualKey.End:
                    if (_fileList.Count > 0 && _currentFileIndex < _fileList.Count - 1)
                    {
                        _currentFileIndex = _fileList.Count - 1;
                        ResetZoomLevel();
                        await SetImageAsync();
                    }
                    e.Handled = true;
                    break;

                case VirtualKey.Space:
                    if ((_mode & Modi.Zoom) != 0) ResetZoomLevel();
                    else if (_isSlideshowRunning) StopSlideshow(1);
                    else StartSlideshow(true);
                    e.Handled = true;
                    break;

                case VirtualKey.Add:
                case (VirtualKey)187: // OemPlus
                    Zoom(1);
                    e.Handled = true;
                    break;

                case VirtualKey.Subtract:
                case (VirtualKey)189: // OemMinus
                    Zoom(-1);
                    e.Handled = true;
                    break;

                case VirtualKey.PageDown:
                    if (_imageDurationSeconds > 1) _imageDurationSeconds--;
                    ShowMessage($"Image duration: {_imageDurationSeconds} sec");
                    if (_isSlideshowRunning) StartSlideshow(false);
                    e.Handled = true;
                    break;

                case VirtualKey.PageUp:
                    if (_imageDurationSeconds < 30) _imageDurationSeconds++;
                    ShowMessage($"Image duration: {_imageDurationSeconds} sec");
                    if (_isSlideshowRunning) StartSlideshow(false);
                    e.Handled = true;
                    break;
            }
        }
        else
        {
            // Alt + arrow → keyboard pan in zoom mode
            switch (e.Key)
            {
                case VirtualKey.Left: AdjustKeyboardVector(-1, 0); MoveToNewPosition(_kv); e.Handled = true; break;
                case VirtualKey.Right: AdjustKeyboardVector(1, 0); MoveToNewPosition(_kv); e.Handled = true; break;
                case VirtualKey.Up: AdjustKeyboardVector(0, -1); MoveToNewPosition(_kv); e.Handled = true; break;
                case VirtualKey.Down: AdjustKeyboardVector(0, 1); MoveToNewPosition(_kv); e.Handled = true; break;
            }
        }
    }

    private void TryCloseApp()
    {
        try { Application.Current.Exit(); } catch { /* ignore on platforms that don't support explicit exit */ }
    }

    private void SetMaxPanValues()
    {
        try
        {
            var scale = (ScaleTransform)((TransformGroup)MediaHost.RenderTransform).Children.First(t => t is ScaleTransform);
            double scaledW = _mediaWidth * scale.ScaleX;
            double scaledH = _mediaHeight * scale.ScaleY;
            _maxX = (scaledW > ImageBorder.ActualWidth) ? (scaledW - ImageBorder.ActualWidth) / 2.0 : 0;
            _maxY = (scaledH > ImageBorder.ActualHeight) ? (scaledH - ImageBorder.ActualHeight) / 2.0 : 0;
        }
        catch
        {
            _maxX = _maxY = 0;
        }
    }

    private void ResetZoomLevel()
    {
        _mode &= ~Modi.Zoom;

        MediaHost.RenderTransformOrigin = new Point(0.5, 0.5);
        var tg = new TransformGroup();
        tg.Children.Add(new ScaleTransform());
        tg.Children.Add(new TranslateTransform());
        MediaHost.RenderTransform = tg;

        SetMaxPanValues();
    }

    private async void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var props = e.GetCurrentPoint(RootGrid).Properties;
        int clickCount = (int)e.GetCurrentPoint(RootGrid).Timestamp; // timestamp is not click count; Uno uses PointerPointProperties. Fall back to double-click via DoubleTapped event? Handled below via IsLeftButtonPressed + a timer-free approximation.

        // Fallback: any non-drag left-click opens the file picker only when double-tapped (see DoubleTapped handler in future polish).
        // v1: middle/right button handled here; double-click file picker via a separate DoubleTapped hook (TODO polish).
        if (props.IsMiddleButtonPressed && _isImageLoaded && OperatingSystem.IsWindows())
        {
            string currentPath = _fileList.ElementAtOrDefault(_currentFileIndex) ?? string.Empty;
            if (!string.IsNullOrEmpty(currentPath))
            {
                try { System.Diagnostics.Process.Start("explorer.exe", $"/select, \"{currentPath}\""); } catch { /* ignore */ }
            }
            e.Handled = true;
            return;
        }

        if (props.IsRightButtonPressed && _isImageLoaded)
        {
            string currentPath = _fileList.ElementAtOrDefault(_currentFileIndex) ?? string.Empty;
            if (string.IsNullOrEmpty(currentPath)) return;
            string data = Path.GetDirectoryName(currentPath) ?? currentPath;
            try
            {
                var dp = new DataPackage();
                dp.SetText(data);
                Clipboard.SetContent(dp);
                ShowMessage("Directory path copied to clipboard");
            }
            catch
            {
                ShowMessage("Failed to set clipboard content!");
            }
            e.Handled = true;
        }

        await Task.CompletedTask;
    }

    private async void Page_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (_isFileDialogOpen) return;
        _isFileDialogOpen = true;
        try
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add("*");
#if WINDOWS
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(((App)Application.Current).MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
#endif
            var file = await picker.PickSingleFileAsync();
            if (file is not null)
            {
                await SetImageAsync(file.Path);
            }
        }
        catch (Exception ex)
        {
            ShowMessage("File picker failed: " + ex.Message);
        }
        finally
        {
            _isFileDialogOpen = false;
        }
    }

    private void RootGrid_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (!_isImageLoaded) return;
        int delta = e.GetCurrentPoint(RootGrid).Properties.MouseWheelDelta;
        Zoom(delta);
        e.Handled = true;
    }

    private void MediaHost_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var props = e.GetCurrentPoint(ImageBorder).Properties;
        if (!_isImageLoaded || !props.IsLeftButtonPressed) return;
        MediaHost.CapturePointer(e.Pointer);
        var tt = (TranslateTransform)((TransformGroup)MediaHost.RenderTransform).Children.First(t => t is TranslateTransform);
        _start = e.GetCurrentPoint(ImageBorder).Position;
        _origin = new Point(tt.X, tt.Y);
        _isDragging = true;
    }

    private void MediaHost_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging) return;
        var p = e.GetCurrentPoint(ImageBorder).Position;
        var v = new Point(_start.X - p.X, _start.Y - p.Y);
        MoveToNewPosition(v);
    }

    private void MediaHost_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _isDragging = false;
        MediaHost.ReleasePointerCapture(e.Pointer);
    }

    private void AdjustKeyboardVector(double x, double y)
    {
        if (x != 0)
        {
            if (Math.Abs(_origin.X - _kv.X - x) < _maxX)
                _kv = new Point(_kv.X + Math.Sign(x) * 10, _kv.Y);
            else if (Math.Abs(_origin.X - _kv.X - x) >= _maxX)
                _kv = new Point(_kv.X + Math.Sign(x) * (_maxX - Math.Abs(_origin.X - _kv.X)), _kv.Y);
        }
        else if (y != 0)
        {
            if (Math.Abs(_origin.Y - _kv.Y - y) < _maxY)
                _kv = new Point(_kv.X, _kv.Y + Math.Sign(y) * 10);
            else if (Math.Abs(_origin.Y - _kv.Y - y) >= _maxY)
                _kv = new Point(_kv.X, _kv.Y + Math.Sign(y) * (_maxY - Math.Abs(_origin.Y - _kv.Y)));
        }
    }

    private void MoveToNewPosition(Point v)
    {
        try
        {
            var tt = (TranslateTransform)((TransformGroup)MediaHost.RenderTransform).Children.First(t => t is TranslateTransform);
            tt.X = _maxX == 0 ? tt.X : (Math.Abs(_origin.X - v.X) >= _maxX) ? Math.Sign(_origin.X - v.X) * _maxX : _origin.X - v.X;
            tt.Y = _maxY == 0 ? tt.Y : (Math.Abs(_origin.Y - v.Y) >= _maxY) ? Math.Sign(_origin.Y - v.Y) * _maxY : _origin.Y - v.Y;
        }
        catch
        {
            // Transform not initialized yet.
        }
    }

    private void Zoom(double delta)
    {
        try
        {
            var st = (ScaleTransform)((TransformGroup)MediaHost.RenderTransform).Children.First(t => t is ScaleTransform);
            if (delta < 0 && st.ScaleX < _zoomMin || delta > 0 && st.ScaleX > _zoomMax) return;

            var tt = (TranslateTransform)((TransformGroup)MediaHost.RenderTransform).Children.First(t => t is TranslateTransform);

            double zoom = delta > 0 ? _zoomStep : 1.0 / _zoomStep;
            st.ScaleX = st.ScaleY *= zoom;

            if (Math.Abs(st.ScaleX - 1.0) < 0.0001) _mode &= ~Modi.Zoom;
            else _mode |= Modi.Zoom;

            SetMaxPanValues();

            tt.X = _maxX == 0 ? 0 : (Math.Abs(tt.X) > _maxX) ? Math.Sign(tt.X) * _maxX : tt.X;
            tt.Y = _maxY == 0 ? 0 : (Math.Abs(tt.Y) > _maxY) ? Math.Sign(tt.Y) * _maxY : tt.Y;
        }
        catch
        {
            // No transform yet.
        }
    }

    private void ShowHelpText()
    {
        HelpText.Text = "General\n" +
            "---------\n" +
            "H / I / F1    Show this help screen\n" +
            "ESC           Close help / End slideshow / Close application\n" +
            "Space         End zoom mode / Start/Stop slideshow\n" +
            "\n" +
            "Mouse Navigation\n" +
            "---------------------\n" +
            "Wheel          Change zoom level\n" +
            "Middle click   Open Explorer and select file (Windows only)\n" +
            "Right click    Copy directory path to clipboard\n" +
            "Drag           Pan image when zoomed in\n" +
            "\n" +
            "Keyboard Navigation\n" +
            "------------------------\n" +
            "Left / Right   Show previous / next image\n" +
            "Home / End     Show first / last image\n" +
            "+ / -          Zoom in / out\n" +
            "\n" +
            "In zoom mode:\n" +
            "Alt + arrows   Move the image\n" +
            "\n" +
            "In slideshow mode:\n" +
            "PageUp/Down    Change image duration (1..30 sec)";
        HelpPanel.Visibility = Visibility.Visible;
        _mode |= Modi.HelpShown;
    }

    private void ShowMessage(string text)
    {
        MessageLabel.Text = text;
        StartMessageFadeoutTimer();
    }

    private void StartMessageFadeoutTimer()
    {
        if (_msgFadeoutSeconds == 0) return;

        MessageLabel.Visibility = Visibility.Visible;

        if (_msgFadeoutSeconds < 0) return;

        _timerMessage.Stop();
        _timerMessage.Interval = TimeSpan.FromSeconds(_msgFadeoutSeconds);
        _timerMessage.Start();
    }

    private void TimerMessage_Tick(object? sender, object e)
    {
        _timerMessage.Stop();
        MessageLabel.Visibility = Visibility.Collapsed;
    }

    private void StartSlideshow(bool showMessage)
    {
        if (_isSlideshowRunning) StopSlideshow(0);
        if (showMessage) ShowMessage("Slideshow started");
        _mode |= Modi.Slideshow;
        _isSlideshowRunning = true;
        _timerSlideshow.Interval = TimeSpan.FromSeconds(_imageDurationSeconds);
        _timerSlideshow.Start();
    }

    private void StopSlideshow(int mode)
    {
        _isSlideshowRunning = false;
        if (mode == 1) ShowMessage("Slideshow paused");
        else if (mode == 2) { _mode &= ~Modi.Slideshow; ShowMessage("Slideshow stopped"); }
        _timerSlideshow.Stop();
    }

    private async void TimerSlideshow_Tick(object? sender, object e)
    {
        if (_currentFileIndex < _fileList.Count - 1)
        {
            _currentFileIndex++;
            await SetImageAsync();
        }
        else
        {
            StopSlideshow(2);
        }
    }
}
