using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SharkiDesktopGuardian.Models;

namespace SharkiDesktopGuardian.Controls;

public sealed class SpriteAnimator : Image
{
    private const int CellWidth = 192;
    private const int CellHeight = 208;
    private readonly DispatcherTimer _timer;
    private BitmapSource? _atlas;
    private PetState _state = PetState.Idle;
    private int _frame;

    public SpriteAnimator()
    {
        Stretch = System.Windows.Media.Stretch.Uniform;
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;
        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(125)
        };
        _timer.Tick += (_, _) => Advance();
        Loaded += OnLoaded;
        Unloaded += (_, _) => _timer.Stop();
    }

    public bool AnimationsEnabled { get; set; } = true;

    public PetState State
    {
        get => _state;
        set
        {
            if (_state == value)
            {
                return;
            }

            _state = value;
            _frame = 0;
            _timer.Interval = ResolveInterval(value);
            RenderFrame();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs eventArgs)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri("pack://application:,,,/Assets/spritesheet.png", UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            _atlas = bitmap;
            RenderFrame();
            _timer.Start();
        }
        catch
        {
            ToolTip = "El atlas de Sharki no está disponible.";
        }
    }

    private void Advance()
    {
        if (_atlas is null)
        {
            return;
        }

        if (!AnimationsEnabled)
        {
            _frame = 0;
        }
        else
        {
            var (_, frameCount) = ResolveRow(_state);
            _frame = (_frame + 1) % frameCount;
        }

        RenderFrame();
    }

    private void RenderFrame()
    {
        if (_atlas is null)
        {
            return;
        }

        var (row, frameCount) = ResolveRow(_state);
        _frame %= frameCount;
        var crop = new CroppedBitmap(_atlas, new Int32Rect(_frame * CellWidth, row * CellHeight, CellWidth, CellHeight));
        crop.Freeze();
        Source = crop;
    }

    private static (int Row, int Frames) ResolveRow(PetState state) => state switch
    {
        PetState.MovingRight => (1, 8),
        PetState.MovingLeft => (2, 8),
        PetState.Greeting => (3, 4),
        PetState.Waiting => (6, 6),
        PetState.HighLoad => (11, 8),      // carga una caja pesada
        PetState.LowDisk => (12, 6),       // empuja una caja llena
        PetState.HighMemory => (13, 6),    // se limpia el sudor
        PetState.ThermalAlert => (14, 5),  // rodeado de llamas
        _ => (0, 6)
    };

    private static TimeSpan ResolveInterval(PetState state) => state switch
    {
        PetState.Waiting => TimeSpan.FromMilliseconds(160),
        _ => TimeSpan.FromMilliseconds(125)
    };
}
