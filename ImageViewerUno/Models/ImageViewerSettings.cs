namespace ImageViewerUno.Models;

public record ImageViewerSettings
{
    public string ApplicationTitle { get; init; } = "Uno Image Viewer";
    public string BackgroundColor { get; init; } = "Black";
    public bool CloseOnLostFocus { get; init; } = false;
    public int ImageDurationSeconds { get; init; } = 2;
    public string IncludedFileExtensions { get; init; } = ".bmp,.gif,.jpeg,.jpg,.png,.tif,.tiff,.webp";
    public string MsgColor { get; init; } = "LimeGreen";
    public double MsgFadeoutSeconds { get; init; } = 2.0;
    public bool RunAnimatedGifs { get; init; } = true;
    public bool ShowHelpOnLoad { get; init; } = false;
    public double ZoomMax { get; init; } = 5.0;
    public double ZoomMin { get; init; } = 0.1;
    public double ZoomStep { get; init; } = 1.25;
}
