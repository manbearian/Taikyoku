
using ShogiEngine;
using System.Runtime.CompilerServices;

namespace MauiUI;

public enum BoardPanelOrientation
{
    Horizontal, Vertical, Corner
}

internal class BoardPanelDrawer : IDrawable
{
    public bool IsRotated { get; set; }

    private BoardPanelView View { get; set; }

    public BoardPanelDrawer(BoardPanelView view) =>
        View = view;

    public void Draw(ICanvas canvas, RectF rect)
    {
        canvas.FillColor = View.BackgroundColor;
        canvas.FontColor = View.TextColor;

        canvas.FillRectangle(0, 0, rect.Width, rect.Height);

        switch (View.Orientation)
        {
            case BoardPanelOrientation.Horizontal:
                DrawHorizontal(canvas, rect);
                break;
            case BoardPanelOrientation.Vertical:
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
            var index = View.IsRotated ? TaikyokuShogi.BoardWidth - i - 1 : i;
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
            var index = View.IsRotated ? TaikyokuShogi.BoardHeight - i - 1 : i;
            canvas.DrawString(RowName(index), rect.Width / 2, i * spacing + (spacing / 2), HorizontalAlignment.Center);
        }
    }
}

public class BoardPanelView : GraphicsView
{
    //
    // Bindabe Proprerties
    //

    public static readonly BindableProperty OrientationProperty = BindableProperty.Create(nameof(Orientation), typeof(BoardPanelOrientation), typeof(BoardPanelView));

    public BoardPanelOrientation Orientation
    {
        get => (BoardPanelOrientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(BoardPanelView));

    public Color TextColor
    {
        get => (Color)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    public static readonly BindableProperty IsRotatedProperty = BindableProperty.Create(nameof(IsRotated), typeof(bool), typeof(BoardPanelView));

    public bool IsRotated
    {
        get => (bool)GetValue(IsRotatedProperty);
        set => SetValue(IsRotatedProperty, value);
    }

    public BoardPanelView()
    {
        // Enable custom draw
        Drawable = new BoardPanelDrawer(this);
    }

    protected override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        if (propertyName == nameof(IsRotated))
            Invalidate();

        base.OnPropertyChanged(propertyName);
    }
}
