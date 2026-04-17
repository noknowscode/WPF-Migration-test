using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ImageViewerUno.Models;

public enum VisualStates
{
    ShowAnim,
    ShowImage,
    ShowMedia
}

public class MediaViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private string? _filenameAnim;
    private string? _filenameImage;
    private string? _filenameMedia;
    private VisualStates _currentVisualState;

    public string? FilenameAnim
    {
        get => _filenameAnim;
        set { _filenameAnim = value; OnPropertyChanged(); }
    }

    public string? FilenameImage
    {
        get => _filenameImage;
        set { _filenameImage = value; OnPropertyChanged(); }
    }

    public string? FilenameMedia
    {
        get => _filenameMedia;
        set { _filenameMedia = value; OnPropertyChanged(); }
    }

    public VisualStates CurrentVisualState
    {
        get => _currentVisualState;
        set { _currentVisualState = value; OnPropertyChanged(); }
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
