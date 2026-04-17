using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace ImageViewerUno.Services;

/// <summary>
/// Drives frame-by-frame playback of a decoded animated image (GIF / WebP) onto a WinUI Image control.
/// Replaces the source WPF app's AnimatedWebpElement (Canvas + System.Drawing.Bitmap + GDI interop).
/// </summary>
public sealed class AnimatedImagePlayer : IDisposable
{
    private readonly Image _target;
    private readonly DispatcherTimer _timer = new();
    private IReadOnlyList<AnimatedImageFrame> _frames = Array.Empty<AnimatedImageFrame>();
    private int _currentIndex;
    private WriteableBitmap? _bitmap;

    public event EventHandler? FirstFrameDisplayed;

    public int NaturalImageWidth => _frames.Count > 0 ? _frames[0].Width : 0;
    public int NaturalImageHeight => _frames.Count > 0 ? _frames[0].Height : 0;

    public AnimatedImagePlayer(Image target)
    {
        _target = target;
        _timer.Tick += OnTick;
    }

    public void Load(Stream stream)
    {
        Stop();
        _frames = AnimatedImageDecoder.Decode(stream);
        _currentIndex = 0;
        if (_frames.Count == 0) return;

        _bitmap = new WriteableBitmap(_frames[0].Width, _frames[0].Height);
        RenderCurrentFrame();
        _target.Source = _bitmap;
        FirstFrameDisplayed?.Invoke(this, EventArgs.Empty);

        if (_frames.Count > 1) Start();
    }

    public void Start()
    {
        if (_frames.Count <= 1) return;
        _timer.Interval = TimeSpan.FromMilliseconds(Math.Max(20, _frames[_currentIndex].DurationMs));
        _timer.Start();
    }

    public void Stop() => _timer.Stop();

    private void OnTick(object? sender, object e)
    {
        _timer.Stop();
        _currentIndex = (_currentIndex + 1) % _frames.Count;
        RenderCurrentFrame();

        _timer.Interval = TimeSpan.FromMilliseconds(Math.Max(20, _frames[_currentIndex].DurationMs));
        _timer.Start();
    }

    private void RenderCurrentFrame()
    {
        if (_bitmap is null) return;

        var frame = _frames[_currentIndex];
        using var writer = _bitmap.PixelBuffer.AsStream();
        writer.Position = 0;
        writer.Write(frame.Bgra, 0, frame.Bgra.Length);
        _bitmap.Invalidate();
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
        _frames = Array.Empty<AnimatedImageFrame>();
        _bitmap = null;
    }
}
