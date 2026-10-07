using System;
using System.Windows;

namespace SuperDictate.Ui;

/// <summary>Which side of the screen the capsule keeps to, on one axis.</summary>
public enum CapsuleEdge
{
    /// <summary>Left, or top.</summary>
    Start,
    Center,
    /// <summary>Right, or bottom.</summary>
    End,
}

/// <summary>
/// Where the capsule sits, independent of any screen's size, so one choice works
/// on every monitor. On each axis it keeps to an edge (or the centre), and X and
/// Y say how far across the screen (0 to 1) that edge of the capsule is.
///
/// That edge is what holds still while the capsule grows with the words: against
/// the left side it grows to the right, against the right side to the left, in
/// the middle both ways. Whatever its size, it stays on screen, a margin away
/// from the edges.
/// </summary>
public readonly record struct CapsulePlacement(CapsuleEdge Horizontal, double X, CapsuleEdge Vertical, double Y)
{
    /// <summary>Gap kept between the capsule and the edges of the work area (DIPs).</summary>
    public const double Margin = 12;

    /// <summary>How close to an edge or centre line a dragged capsule snaps to it (DIPs).</summary>
    public const double SnapDistance = 14;

    public static readonly CapsulePlacement TopCenter = new(CapsuleEdge.Center, 0.5, CapsuleEdge.Start, 0);

    public static readonly (string Id, string Name, CapsulePlacement Placement)[] Presets =
    {
        ("top", "Top center", TopCenter),
        ("top-left", "Top left", new(CapsuleEdge.Start, 0, CapsuleEdge.Start, 0)),
        ("top-right", "Top right", new(CapsuleEdge.End, 1, CapsuleEdge.Start, 0)),
        ("bottom", "Bottom center", new(CapsuleEdge.Center, 0.5, CapsuleEdge.End, 1)),
        ("bottom-left", "Bottom left", new(CapsuleEdge.Start, 0, CapsuleEdge.End, 1)),
        ("bottom-right", "Bottom right", new(CapsuleEdge.End, 1, CapsuleEdge.End, 1)),
    };

    /// <summary>The capsule is against the top (or bottom) of the screen, so it slides in from there.</summary>
    public bool AtTop => Vertical == CapsuleEdge.Start && Y <= 0.001;

    public bool AtBottom => Vertical == CapsuleEdge.End && Y >= 0.999;

    /// <summary>
    /// The capsule's rectangle in this work area. Its anchored edge goes where X and
    /// Y say, pulled back so that even at <paramref name="widest"/> the capsule fits.
    /// </summary>
    public Rect Place(Size size, double widest, Rect area)
    {
        var usable = Usable(area);
        widest = Math.Min(Math.Max(widest, size.Width), usable.Width);

        var at = usable.Left + (Math.Clamp(X, 0, 1) * usable.Width);
        // Clamp the edge that holds still, so the capsule at its widest stays inside.
        var left = Horizontal switch
        {
            CapsuleEdge.Start => Math.Clamp(at, usable.Left, usable.Right - widest),
            CapsuleEdge.End => Math.Clamp(at, usable.Left + widest, usable.Right) - size.Width,
            _ => Math.Clamp(at, usable.Left + (widest / 2), usable.Right - (widest / 2)) - (size.Width / 2),
        };

        var y = usable.Top + (Math.Clamp(Y, 0, 1) * usable.Height);
        var top = Vertical switch
        {
            CapsuleEdge.Start => y,
            CapsuleEdge.End => y - size.Height,
            _ => y - (size.Height / 2),
        };
        top = Math.Clamp(top, usable.Top, Math.Max(usable.Top, usable.Bottom - size.Height));

        return new Rect(left, top, size.Width, size.Height);
    }

    /// <summary>
    /// The placement for a capsule left at this rectangle. Its third of the screen
    /// decides the edge it keeps to: left third the left edge, right third the
    /// right edge, between them its centre. The same goes for top and bottom.
    /// </summary>
    public static CapsulePlacement FromRect(Rect capsule, Rect area)
    {
        var usable = Usable(area);
        var (horizontal, x) = Along(capsule.Left, capsule.Right, usable.Left, usable.Width);
        var (vertical, y) = Along(capsule.Top, capsule.Bottom, usable.Top, usable.Height);
        return new CapsulePlacement(horizontal, x, vertical, y);
    }

    /// <summary>Which guides a dragged capsule is resting on.</summary>
    [Flags]
    public enum Guides
    {
        None = 0,
        Left = 1,
        CenterX = 2,
        Right = 4,
        Top = 8,
        CenterY = 16,
        Bottom = 32,
    }

    /// <summary>
    /// A dragged capsule, kept inside the work area and pulled onto an edge or a
    /// centre line when it comes within <see cref="SnapDistance"/> of one.
    /// </summary>
    public static Rect Snap(Rect capsule, Rect area, out Guides guides)
    {
        var usable = Usable(area);
        guides = Guides.None;
        var left = Math.Clamp(capsule.Left, usable.Left, Math.Max(usable.Left, usable.Right - capsule.Width));
        var top = Math.Clamp(capsule.Top, usable.Top, Math.Max(usable.Top, usable.Bottom - capsule.Height));

        if (Math.Abs(left - usable.Left) <= SnapDistance) (left, guides) = (usable.Left, guides | Guides.Left);
        else if (Math.Abs(left + capsule.Width - usable.Right) <= SnapDistance) (left, guides) = (usable.Right - capsule.Width, guides | Guides.Right);
        else if (Math.Abs(left + (capsule.Width / 2) - CenterOf(area).X) <= SnapDistance) (left, guides) = (CenterOf(area).X - (capsule.Width / 2), guides | Guides.CenterX);

        if (Math.Abs(top - usable.Top) <= SnapDistance) (top, guides) = (usable.Top, guides | Guides.Top);
        else if (Math.Abs(top + capsule.Height - usable.Bottom) <= SnapDistance) (top, guides) = (usable.Bottom - capsule.Height, guides | Guides.Bottom);
        else if (Math.Abs(top + (capsule.Height / 2) - CenterOf(area).Y) <= SnapDistance) (top, guides) = (CenterOf(area).Y - (capsule.Height / 2), guides | Guides.CenterY);

        return new Rect(left, top, capsule.Width, capsule.Height);
    }

    public static Rect Usable(Rect area)
    {
        var usable = area;
        usable.Inflate(-Margin, -Margin);
        return usable.IsEmpty ? area : usable;
    }

    private static Point CenterOf(Rect area) => new(area.Left + (area.Width / 2), area.Top + (area.Height / 2));

    private static (CapsuleEdge Edge, double At) Along(double start, double end, double origin, double length)
    {
        if (length <= 0) return (CapsuleEdge.Center, 0.5);
        var middle = ((start + end) / 2 - origin) / length;
        return middle switch
        {
            < 1.0 / 3 => (CapsuleEdge.Start, Math.Clamp((start - origin) / length, 0, 1)),
            > 2.0 / 3 => (CapsuleEdge.End, Math.Clamp((end - origin) / length, 0, 1)),
            _ => (CapsuleEdge.Center, Math.Clamp(middle, 0, 1)),
        };
    }
}
