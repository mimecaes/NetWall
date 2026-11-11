using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using NetWall.IDE.Models;

namespace NetWall.IDE.Controls;

public sealed class KittenAvatar : Control
{
    public const int AccessoryCount = 5;

    public static readonly StyledProperty<PetColor> ColorProperty =
        AvaloniaProperty.Register<KittenAvatar, PetColor>(nameof(Color));

    public static readonly StyledProperty<int> AccessoryProperty =
        AvaloniaProperty.Register<KittenAvatar, int>(nameof(Accessory));

    private const int Cell = 32;
    private const int IdleRow = 52;
    private const int CropX = 6;
    private const int CropY = 2;
    private const int CropSize = 23;

    private static readonly IBrush Bow = new ImmutableSolidColorBrush(Avalonia.Media.Color.Parse("#E8899E"));
    private static readonly IBrush BowKnot = new ImmutableSolidColorBrush(Avalonia.Media.Color.Parse("#C2577F"));
    private static readonly IBrush Light = new ImmutableSolidColorBrush(Avalonia.Media.Color.Parse("#F4F1FA"));
    private static readonly IBrush Dark = new ImmutableSolidColorBrush(Avalonia.Media.Color.Parse("#1B1A25"));
    private static readonly IBrush Scarf = new ImmutableSolidColorBrush(Avalonia.Media.Color.Parse("#EF9F27"));
    private static readonly IBrush ScarfAlt = new ImmutableSolidColorBrush(Avalonia.Media.Color.Parse("#5DCAA5"));
    private static readonly IBrush Hat = new ImmutableSolidColorBrush(Avalonia.Media.Color.Parse("#7F77DD"));
    private static readonly IBrush HatBand = new ImmutableSolidColorBrush(Avalonia.Media.Color.Parse("#AFA9EC"));

    static KittenAvatar()
    {
        AffectsRender<KittenAvatar>(ColorProperty, AccessoryProperty);
    }

    public KittenAvatar()
    {
        RenderOptions.SetBitmapInterpolationMode(this, Avalonia.Media.Imaging.BitmapInterpolationMode.None);
    }

    public PetColor Color
    {
        get => GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    public int Accessory
    {
        get => GetValue(AccessoryProperty);
        set => SetValue(AccessoryProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var size = System.Math.Min(Bounds.Width, Bounds.Height);
        var origin = new Point((Bounds.Width - size) / 2, (Bounds.Height - size) / 2);
        var pixel = size / CropSize;
        var sheet = PetWidget.LoadSheet(Color);
        context.DrawImage(sheet, new Rect(CropX, IdleRow * Cell + CropY, CropSize, CropSize), new Rect(origin, new Size(size, size)));

        void Dot(IBrush brush, double x, double y, double width, double height) =>
            context.FillRectangle(brush, new Rect(origin.X + x * pixel, origin.Y + y * pixel, width * pixel, height * pixel));

        switch (Accessory)
        {
            case 1:
                Dot(Bow, 13, 5, 2, 3);
                Dot(Bow, 16, 5, 2, 3);
                Dot(BowKnot, 15, 6, 1, 1);
                break;
            case 2:
                var frame = Color == PetColor.White ? Dark : Light;
                Dot(frame, 5, 9, 4, 1);
                Dot(frame, 5, 11, 4, 1);
                Dot(frame, 5, 9, 1, 3);
                Dot(frame, 8, 9, 1, 3);
                Dot(frame, 10, 9, 4, 1);
                Dot(frame, 10, 11, 4, 1);
                Dot(frame, 10, 9, 1, 3);
                Dot(frame, 13, 9, 1, 3);
                Dot(frame, 9, 10, 1, 1);
                break;
            case 3:
                var scarf = Color == PetColor.Orange ? ScarfAlt : Scarf;
                Dot(scarf, 4, 13, 12, 2);
                Dot(scarf, 12, 15, 2, 3);
                break;
            case 4:
                Dot(Hat, 6, 3, 9, 4);
                Dot(HatBand, 5, 6, 11, 1);
                Dot(Light, 9, 1, 2, 2);
                break;
        }
    }
}
