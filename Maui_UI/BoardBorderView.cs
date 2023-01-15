using System.Runtime.CompilerServices;

using ShogiEngine;

namespace MauiUI;

public enum BorderOrientation
{
    Horizontal, Vertical, Corner
}


public class BoardBorderView : GraphicsView, IDrawable
{
    //
    // Bindabe Proprerties
    //

    public static readonly BindableProperty OrientationProperty = BindableProperty.Create(nameof(Orientation), typeof(BorderOrientation), typeof(BoardBorderView));

    public BorderOrientation Orientation
    {
        get => (BorderOrientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(BoardBorderView));

    public Color TextColor
    {
        get => (Color)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    public static readonly BindableProperty IsRotatedProperty = BindableProperty.Create(nameof(IsRotated), typeof(bool), typeof(BoardBorderView));

    public bool IsRotated
    {
        get => (bool)GetValue(IsRotatedProperty);
        set => SetValue(IsRotatedProperty, value);
    }

    public BoardBorderView()
    {
        Drawable = this;
    }

    protected override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        if (propertyName == nameof(IsRotated))
            Invalidate();

        base.OnPropertyChanged(propertyName);
    }

    public void Draw(ICanvas canvas, RectF rect)
    {
        canvas.FillColor = BackgroundColor;
        canvas.FontColor = TextColor;

        canvas.FillRectangle(0, 0, rect.Width, rect.Height);

        switch (Orientation)
        {
            case BorderOrientation.Horizontal:
                DrawHorizontal(canvas, rect);
                break;
            case BorderOrientation.Vertical:
                DrawVertical(canvas, rect);
                break;
        }
    }

    private void DrawHorizontal(ICanvas canvas, RectF rect)
    {
        static string ColumnName(int i) => $"{TaikyokuShogi.BoardWidth - i}";

        var spacing = rect.Width / TaikyokuShogi.BoardWidth;

        canvas.FontSize = rect.Height;

        for (int i = 0; i < TaikyokuShogi.BoardWidth; ++i)
        {
            var index = IsRotated ? TaikyokuShogi.BoardWidth - i - 1 : i;
            canvas.DrawString(ColumnName(index), i * spacing + (spacing / 2), rect.Height * 0.75f, HorizontalAlignment.Center);
        }
    }

    private void DrawVertical(ICanvas canvas, RectF rect)
    {
        static string RowName(int i) => new((char)('A' + (i % 26)), i / 26 + 1);

        var spacing = rect.Height / TaikyokuShogi.BoardHeight;

        canvas.FontSize = rect.Width * 0.8f;

        for (int i = 0; i < TaikyokuShogi.BoardHeight; ++i)
        {
            var index = IsRotated ? TaikyokuShogi.BoardHeight - i - 1 : i;
            canvas.DrawString(RowName(index), rect.Width / 2, i * spacing + (spacing / 2), HorizontalAlignment.Center);
        }
    }
}
