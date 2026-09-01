using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SharkiDesktopGuardian.Models;

namespace SharkiDesktopGuardian.Controls;

public sealed class SpriteAnimator : Image
{
    private const int AtlasColumns = 8;
    private const int AtlasRows = 15;
    private readonly DispatcherTimer _timer;
    private BitmapSource? _atlas;
    private int _cellWidth;
    private int _cellHeight;
    private string _petId = PetCatalog.DefaultId;
    private PetState _state = PetState.Idle;
    private int _frame;

    public SpriteAnimator()
    {
        Stretch = System.Windows.Media.Stretch.Uniform;
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(125)
        };
        _timer.Tick += (_, _) => Advance();
        Loaded += OnLoaded;
        Unloaded += (_, _) => _timer.Stop();
    }

    public bool AnimationsEnabled { get; set; } = true;

    public string PetId
    {
        get => _petId;
        set
        {
            var normalized = PetCatalog.NormalizeId(value);
            if (string.Equals(_petId, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _petId = normalized;
            _frame = 0;
            if (IsLoaded)
            {
                LoadAtlas();
                if (_atlas is not null)
                {
                    _timer.Start();
                }
            }
        }
    }

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
        LoadAtlas();
        if (_atlas is not null)
        {
            _timer.Start();
        }
    }

    private void LoadAtlas()
    {
        var definition = PetCatalog.Resolve(_petId);
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri($"pack://application:,,,/{definition.AtlasResourcePath}", UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            if (bitmap.PixelWidth % AtlasColumns != 0 || bitmap.PixelHeight % AtlasRows != 0)
            {
                throw new InvalidDataException(
                    $"El atlas de {definition.DisplayName} debe tener {AtlasColumns} columnas y {AtlasRows} filas completas.");
            }

            _cellWidth = bitmap.PixelWidth / AtlasColumns;
            _cellHeight = bitmap.PixelHeight / AtlasRows;
            _atlas = bitmap;
            ToolTip = null;
            RenderFrame();
        }
        catch
        {
            _atlas = null;
            Source = null;
            _timer.Stop();
            ToolTip = $"El atlas de {definition.DisplayName} no está disponible.";
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
        var crop = new CroppedBitmap(_atlas, new Int32Rect(_frame * _cellWidth, row * _cellHeight, _cellWidth, _cellHeight));
        crop.Freeze();
        Source = crop;
    }

    private (int Row, int Frames) ResolveRow(PetState state) => state switch
    {
        PetState.MovingRight => (1, 8),
        PetState.MovingLeft => (2, 8),
        PetState.Greeting => (3, 4),
        PetState.CommandNotRecognized => (5, 6),
        PetState.Waiting => (6, 6),
        PetState.HighLoad => (11, 8),      // carga una caja pesada
        PetState.LowDisk => (12, 6),       // empuja una caja llena
        PetState.HighMemory => (13, 6),    // se limpia el sudor
        PetState.ThermalAlert => (14, 5),  // rodeado de llamas
        // El reposo es deliberadamente estático para todas las mascotas. Las demás
        // poses solo se reproducen cuando existe una interacción o una alerta.
        _ => (0, 1)
    };

    private static TimeSpan ResolveInterval(PetState state) => state switch
    {
        PetState.Waiting => TimeSpan.FromMilliseconds(160),
        PetState.CommandNotRecognized => TimeSpan.FromMilliseconds(180),
        _ => TimeSpan.FromMilliseconds(125)
    };
}
