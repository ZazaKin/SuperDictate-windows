using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SuperDictate.Ui;

/// <summary>
/// A round badge for each language: its flag, drawn as simple vector shapes on a
/// 100 × 100 square and cropped to a circle (Windows shows no flag emoji). A
/// language without a badge shows its code.
/// </summary>
internal static class LanguageIcon
{
    private static readonly Brush RimBrush = Frozen(new SolidColorBrush(Color.FromArgb(46, 255, 255, 255)));
    private static readonly Dictionary<string, Brush> Cache = new();

    private static readonly Dictionary<string, Action<DrawingGroup>> Flags = new()
    {
        ["en"] = g =>
        {
            // The Union Flag, centre crop: diagonals under the cross.
            Fill(g, "#012169", 0, 0, 100, 100);
            Line(g, "#FFFFFF", 0, 0, 100, 100, 20);
            Line(g, "#FFFFFF", 0, 100, 100, 0, 20);
            Line(g, "#C8102E", 0, 0, 100, 100, 7);
            Line(g, "#C8102E", 0, 100, 100, 0, 7);
            Fill(g, "#FFFFFF", 37, 0, 26, 100);
            Fill(g, "#FFFFFF", 0, 37, 100, 26);
            Fill(g, "#C8102E", 43, 0, 14, 100);
            Fill(g, "#C8102E", 0, 43, 100, 14);
        },
        ["es"] = g =>
        {
            Fill(g, "#AA151B", 0, 0, 100, 100);
            Fill(g, "#F1BF00", 0, 25, 100, 50);
        },
        ["fr"] = g => Vertical(g, "#0055A4", "#FFFFFF", "#EF4135"),
        ["de"] = g => Horizontal(g, "#000000", "#DD0000", "#FFCE00"),
        ["it"] = g => Vertical(g, "#009246", "#FFFFFF", "#CE2B37"),
        ["pt"] = g =>
        {
            Fill(g, "#046A38", 0, 0, 40, 100);
            Fill(g, "#DA291C", 40, 0, 60, 100);
            Ring(g, "#FFE900", 40, 50, 16, 5);
            Fill(g, "#FFFFFF", 34, 42, 12, 16);
            Fill(g, "#DA291C", 36, 44, 8, 12);
        },
        ["nl"] = g => Horizontal(g, "#AE1C28", "#FFFFFF", "#21468B"),
        ["pl"] = g => Horizontal(g, "#FFFFFF", "#DC143C"),
        ["ru"] = g => Horizontal(g, "#FFFFFF", "#0039A6", "#D52B1E"),
        ["uk"] = g => Horizontal(g, "#0057B7", "#FFD700"),
        ["cs"] = g =>
        {
            Horizontal(g, "#FFFFFF", "#D7141A");
            Polygon(g, "#11457E", new Point(0, 0), new Point(52, 50), new Point(0, 100));
        },
        ["sv"] = g =>
        {
            Fill(g, "#006AA7", 0, 0, 100, 100);
            Fill(g, "#FECC00", 28, 0, 16, 100);
            Fill(g, "#FECC00", 0, 42, 100, 16);
        },
        ["el"] = g =>
        {
            for (var stripe = 0; stripe < 9; stripe++) Fill(g, stripe % 2 == 0 ? "#0D5EAF" : "#FFFFFF", 0, stripe * 100 / 9.0, 100, 100 / 9.0 + 0.5);
            Fill(g, "#0D5EAF", 0, 0, 55.6, 55.6);
            Fill(g, "#FFFFFF", 22.2, 0, 11.1, 55.6);
            Fill(g, "#FFFFFF", 0, 22.2, 55.6, 11.1);
        },
        ["ro"] = g => Vertical(g, "#002B7F", "#FCD116", "#CE1126"),
        ["hu"] = g => Horizontal(g, "#CE2939", "#FFFFFF", "#477050"),
        ["bg"] = g => Horizontal(g, "#FFFFFF", "#00966E", "#D62612"),
        ["da"] = g =>
        {
            Fill(g, "#C8102E", 0, 0, 100, 100);
            Fill(g, "#FFFFFF", 28, 0, 14, 100);
            Fill(g, "#FFFFFF", 0, 43, 100, 14);
        },
        ["fi"] = g =>
        {
            Fill(g, "#FFFFFF", 0, 0, 100, 100);
            Fill(g, "#002F6C", 26, 0, 18, 100);
            Fill(g, "#002F6C", 0, 41, 100, 18);
        },
        ["sk"] = g =>
        {
            Horizontal(g, "#FFFFFF", "#0B4EA2", "#EE1C25");
            // The shield: a white double cross on red, over blue hills.
            Polygon(g, "#FFFFFF", new Point(20, 22), new Point(56, 22), new Point(56, 58), new Point(38, 80), new Point(20, 58));
            Polygon(g, "#EE1C25", new Point(23, 25), new Point(53, 25), new Point(53, 57), new Point(38, 76), new Point(23, 57));
            Fill(g, "#FFFFFF", 36, 30, 4, 30);
            Fill(g, "#FFFFFF", 29, 36, 18, 3.5);
            Fill(g, "#FFFFFF", 31, 44, 14, 3.5);
            Polygon(g, "#0B4EA2", new Point(26, 60), new Point(50, 60), new Point(38, 74));
        },
        ["hr"] = g =>
        {
            Horizontal(g, "#FF0000", "#FFFFFF", "#171796");
            // The chequy shield in the middle.
            for (var row = 0; row < 5; row++)
            {
                for (var column = 0; column < 5; column++)
                {
                    Fill(g, (row + column) % 2 == 0 ? "#FF0000" : "#FFFFFF", 37.5 + (column * 5), 28 + (row * 6), 5, 6);
                }
            }
        },
        ["lt"] = g => Horizontal(g, "#FDB913", "#006A44", "#C1272D"),
        ["sl"] = g =>
        {
            Horizontal(g, "#FFFFFF", "#005DA4", "#ED1C24");
            // The shield: Triglav in white on blue, edged in red.
            Polygon(g, "#ED1C24", new Point(18, 20), new Point(42, 20), new Point(42, 48), new Point(30, 58), new Point(18, 48));
            Polygon(g, "#005DA4", new Point(20, 22), new Point(40, 22), new Point(40, 47), new Point(30, 55), new Point(20, 47));
            Polygon(g, "#FFFFFF", new Point(21, 46), new Point(26, 37), new Point(30, 42), new Point(34, 37), new Point(39, 46), new Point(30, 53));
        },
        ["lv"] = g =>
        {
            Fill(g, "#9E3039", 0, 0, 100, 100);
            Fill(g, "#FFFFFF", 0, 40, 100, 20);
        },
        ["et"] = g => Horizontal(g, "#0072CE", "#000000", "#FFFFFF"),
        ["mt"] = g =>
        {
            Vertical(g, "#FFFFFF", "#CF142B");
            // The George Cross, in its corner.
            Fill(g, "#A0A0A0", 14, 18, 18, 18);
            Fill(g, "#FFFFFF", 21, 18, 4, 18);
            Fill(g, "#FFFFFF", 14, 25, 18, 4);
        },
    };

    /// <summary>A round badge of the given size for a language code.</summary>
    public static FrameworkElement Create(string code, double size)
    {
        var badge = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size / 2),
            BorderBrush = RimBrush,
            BorderThickness = new Thickness(1),
        };

        if (Flags.ContainsKey(code))
        {
            badge.Background = FlagBrush(code);
        }
        else
        {
            badge.Background = Solid("#242F3D");
            badge.Child = Letter(code.ToUpperInvariant(), size * 0.65, Solid("#6AB2F2"));
        }

        return badge;
    }

    /// <summary>Whether the language has a flag rather than its code.</summary>
    public static bool HasPicture(string code) => Flags.ContainsKey(code);

    private static Brush FlagBrush(string code)
    {
        if (Cache.TryGetValue(code, out var cached)) return cached;
        var group = new DrawingGroup();
        Flags[code](group);
        var brush = new DrawingBrush(group)
        {
            Viewbox = new Rect(0, 0, 100, 100),
            ViewboxUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.Fill,
        };
        brush.Freeze();
        Cache[code] = brush;
        return brush;
    }

    private static TextBlock Letter(string text, double size, Brush color) => new()
    {
        Text = text,
        FontSize = size * 0.42,
        FontWeight = FontWeights.SemiBold,
        Foreground = color,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static void Horizontal(DrawingGroup g, params string[] colors)
    {
        var height = 100.0 / colors.Length;
        for (var index = 0; index < colors.Length; index++) Fill(g, colors[index], 0, index * height, 100, height + 0.5);
    }

    private static void Vertical(DrawingGroup g, params string[] colors)
    {
        var width = 100.0 / colors.Length;
        for (var index = 0; index < colors.Length; index++) Fill(g, colors[index], index * width, 0, width + 0.5, 100);
    }

    private static void Fill(DrawingGroup g, string color, double x, double y, double width, double height) =>
        g.Children.Add(new GeometryDrawing(Solid(color), null, new RectangleGeometry(new Rect(x, y, width, height))));

    private static void Ring(DrawingGroup g, string color, double x, double y, double radius, double thickness) =>
        g.Children.Add(new GeometryDrawing(null, new Pen(Solid(color), thickness), new EllipseGeometry(new Point(x, y), radius, radius)));

    private static void Line(DrawingGroup g, string color, double x1, double y1, double x2, double y2, double thickness) =>
        g.Children.Add(new GeometryDrawing(null, new Pen(Solid(color), thickness), new LineGeometry(new Point(x1, y1), new Point(x2, y2))));

    private static void Polygon(DrawingGroup g, string color, params Point[] points)
    {
        var figure = new PathFigure { StartPoint = points[0], IsClosed = true, IsFilled = true };
        for (var index = 1; index < points.Length; index++) figure.Segments.Add(new LineSegment(points[index], false));
        g.Children.Add(new GeometryDrawing(Solid(color), null, new PathGeometry { Figures = { figure } }));
    }

    private static SolidColorBrush Solid(string hex) => Frozen(new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)));

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
