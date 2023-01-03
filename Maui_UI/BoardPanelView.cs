
using ShogiEngine;

namespace MauiUI;

public enum BoardPanelOrientation
{
    Horizontal, Vertical, Corner
}

internal class BoardPanelDrawer : IDrawable
{
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
            canvas.DrawString(ColumnName(i), i * spacing + (spacing / 2), rect.Height * 0.75f, HorizontalAlignment.Center);
        }
    }

    private void DrawVertical(ICanvas canvas, RectF rect)
    {
        static string RowName(int i) => new((char)('A' + (i % 26)), i / 26 + 1);

        var spacing = rect.Height / TaikyokuShogi.BoardHeight;

        canvas.FontSize = rect.Width * 0.8f;

        for (int i = 0; i < TaikyokuShogi.BoardHeight; ++i)
        {
            canvas.DrawString(RowName(i), rect.Width / 2, i * spacing + (spacing / 2), HorizontalAlignment.Center);
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

    public BoardPanelView()
    {
        // Enable custom draw
        Drawable = new BoardPanelDrawer(this);
    }
}
