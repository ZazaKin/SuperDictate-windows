using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SuperDictate.Ui;

/// <summary>
/// The moving picture inside an art skin, under the words: light pools under grain,
/// a horizon, chrome ribbons, a honeycomb, a pixel wave, tiles, a sky. It fills the
/// capsule and is cut to its shape. <see cref="Render"/> moves it once a frame; the
/// voice lifts or brightens it. Every color under the words keeps the skin's text
/// at 4.5:1, which the self-test checks through the skin's fill colors.
/// </summary>
internal abstract class CapsuleArt : Grid
{
    private double _radius;
    private Size _built;

    protected CapsuleArt()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
        SizeChanged += (_, _) => Cut();
    }

    public static CapsuleArt? For(string? art) => art switch
    {
        "bloom" => new Bloom(),
        "eclipse" => new Eclipse(),
        "chrome" => new Chrome(),
        "hive" => new Hive(),
        "halftone" => new Halftone(),
        "mosaic" => new Mosaic(),
        "mesa" => new Mesa(),
        _ => null,
    };

    /// <summary>The capsule's corner radius, to cut the picture to.</summary>
    public double Radius
    {
        set
        {
            _radius = value;
            Cut();
        }
    }

    /// <param name="seconds">Time, for the motion; held at 0 with Windows animations off.</param>
    /// <param name="level">The voice, 0 to 1.</param>
    public void Render(double seconds, double level)
    {
        var size = new Size(ActualWidth, ActualHeight);
        if (size.Width <= 0 || size.Height <= 0) return;
        if (size != _built)
        {
            _built = size;
            Build(size.Width, size.Height);
        }

        Draw(size.Width, size.Height, seconds, level);
    }

    /// <summary>Lays the picture out for this size; called again when the capsule grows.</summary>
    protected virtual void Build(double width, double height)
    {
    }

    protected abstract void Draw(double width, double height, double seconds, double level);

    private void Cut() => Clip = new RectangleGeometry(new Rect(RenderSize), _radius, _radius);

    /// <summary>
    /// A calm lane behind the line of words: the skin's base color over the art, from
    /// just under the meter to the bottom margin, so the words always read; the art
    /// stays vivid around the meter, at the ends and along the bottom edge.
    /// </summary>
    protected void Lane(uint rgb, double strength)
    {
        var alpha = (byte)Math.Round(255 * strength);
        Children.Add(new Rectangle
        {
            Fill = Frozen(new LinearGradientBrush(new GradientStopCollection
            {
                new(Rgb(rgb, 0), 0.4),
                new(Rgb(rgb, alpha), 0.54),
                new(Rgb(rgb, alpha), 0.9),
                new(Rgb(rgb, (byte)(alpha * 0.4)), 1),
            }, 90)),
        });
    }

    protected static Color Rgb(uint rgb, byte alpha = 255) => Color.FromArgb(alpha, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

    protected static Brush Solid(uint rgb, byte alpha = 255) => Frozen(new SolidColorBrush(Rgb(rgb, alpha)));

    /// <summary>A soft round light: the color in the middle, nothing at the edge.</summary>
    protected static Brush Glow(uint rgb, byte alpha) => Frozen(new RadialGradientBrush(
        new GradientStopCollection { new(Rgb(rgb, alpha), 0), new(Rgb(rgb, (byte)(alpha * 0.45)), 0.45), new(Rgb(rgb, 0), 1) }));

    protected static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }

    private static Brush? _grain;

    /// <summary>Film grain: a fixed speckle, tiled, the same every run.</summary>
    protected static Brush Grain()
    {
        if (_grain is not null) return _grain;
        const int Side = 96;
        var random = new Random(7);
        var pixels = new int[Side * Side];
        for (var index = 0; index < pixels.Length; index++)
        {
            var light = random.Next(2) == 0;
            var alpha = random.Next(0, 70);
            var value = light ? alpha : 0; // Premultiplied: white at this alpha, or black.
            pixels[index] = (alpha << 24) | (value << 16) | (value << 8) | value;
        }

        var bitmap = new WriteableBitmap(Side, Side, 96, 96, PixelFormats.Pbgra32, null);
        bitmap.WritePixels(new Int32Rect(0, 0, Side, Side), pixels, Side * 4, 0);
        bitmap.Freeze();
        _grain = Frozen(new ImageBrush(bitmap)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, Side / 2.0, Side / 2.0),
            ViewportUnits = BrushMappingMode.Absolute,
        });
        return _grain;
    }

    /// <summary>Grainy pastel light pools, after a mesh-gradient poster: they drift, and swell as you speak.</summary>
    private sealed class Bloom : CapsuleArt
    {
        // Periwinkle, coral, mint, peach, lilac: light enough that dark words over any of them hold 4.5:1.
        private static readonly uint[] Pools = { 0x7C97FF, 0xFF7A6B, 0x6FE0C4, 0xFFB37E, 0xB98AFF };
        private readonly Ellipse[] _pools;
        private readonly Canvas _canvas = new();

        public Bloom()
        {
            Background = Solid(0xF6EEF0);
            _pools = Pools.Select(color => new Ellipse { Fill = Glow(color, 255) }).ToArray();
            foreach (var pool in _pools) _canvas.Children.Add(pool);
            Children.Add(_canvas);
            Children.Add(new Rectangle { Fill = Grain(), Opacity = 0.3 });
            Lane(0xF6EEF0, 0.45);
        }

        protected override void Draw(double width, double height, double seconds, double level)
        {
            for (var index = 0; index < _pools.Length; index++)
            {
                var size = height * (2.3 + (0.5 * level) + (0.25 * Math.Sin((seconds * 0.7) + index)));
                var x = width * (0.08 + (0.22 * index) + (0.07 * Math.Sin((seconds * 0.45) + (index * 1.7))));
                var y = height * (0.5 + (0.45 * Math.Cos((seconds * 0.55) + (index * 2.1))));
                var pool = _pools[index];
                pool.Width = size * 1.7;
                pool.Height = size;
                Canvas.SetLeft(pool, x - (pool.Width / 2));
                Canvas.SetTop(pool, y - (size / 2));
            }
        }
    }

    /// <summary>A planet's lit edge low across the capsule; the light rises with the voice and a glint travels along it.</summary>
    private sealed class Eclipse : CapsuleArt
    {
        private readonly Ellipse _glow = new() { Fill = Glow(0x2560D8, 190) };
        private readonly Ellipse _planet = new() { Fill = Solid(0x010207) };
        private readonly Ellipse _rim = new();
        private readonly Canvas _canvas = new();
        private readonly LinearGradientBrush _edge = new() { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };

        public Eclipse()
        {
            Background = Frozen(new LinearGradientBrush(Rgb(0x02040A), Rgb(0x061430), 90));
            foreach (var color in new uint[] { 0x2F7BFF, 0x9CCBFF, 0xFFFFFF, 0x9CCBFF, 0x2F7BFF })
            {
                _edge.GradientStops.Add(new GradientStop(Rgb(color, 0), 0));
            }

            _rim.Stroke = _edge;
            _canvas.Children.Add(_glow);
            _canvas.Children.Add(_planet);
            _canvas.Children.Add(_rim);
            Children.Add(_canvas);
            Lane(0x02040A, 0.3);
        }

        protected override void Build(double width, double height)
        {
            // The horizon sits just under the words, in the capsule's bottom margin.
            var radius = width * 1.4;
            foreach (var disc in new[] { _planet, _rim })
            {
                disc.Width = disc.Height = radius * 2;
                Canvas.SetLeft(disc, (width / 2) - radius);
                Canvas.SetTop(disc, (height * 0.9) - (disc == _rim ? 0 : -0.6));
            }

            _rim.StrokeThickness = Math.Max(1, height * 0.03);
        }

        protected override void Draw(double width, double height, double seconds, double level)
        {
            // The glow rises behind the horizon, brighter and taller as the voice comes.
            _glow.Width = width * 0.95;
            _glow.Height = height * (1.3 + (0.7 * level));
            Canvas.SetLeft(_glow, (width * (0.5 + (0.18 * Math.Sin(seconds * 0.35)))) - (_glow.Width / 2));
            Canvas.SetTop(_glow, (height * 0.9) - (_glow.Height / 2));
            _glow.Opacity = 0.55 + (0.45 * level);

            // A glint runs along the rim and back.
            var centre = 0.5 + (0.35 * Math.Sin(seconds * 0.5));
            byte[] alphas = { 0, 170, 255, 170, 0 };
            double[] offsets = { centre - 0.35, centre - 0.12, centre, centre + 0.12, centre + 0.35 };
            for (var index = 0; index < alphas.Length; index++)
            {
                var stop = _edge.GradientStops[index];
                stop.Color = Color.FromArgb(alphas[index], stop.Color.R, stop.Color.G, stop.Color.B);
                stop.Offset = offsets[index];
            }
        }
    }

    /// <summary>Curved black ribbons with iridescent edges, after dark chrome: they undulate, a glint slides across.</summary>
    private sealed class Chrome : CapsuleArt
    {
        private const int Count = 5;
        private readonly Ellipse[] _ribbons = new Ellipse[Count];
        private readonly Canvas _canvas = new();
        private readonly LinearGradientBrush _glint = new() { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0.4) };
        private double _distance;

        public Chrome()
        {
            Background = Solid(0x07080B);
            for (var index = 0; index < Count; index++)
            {
                _ribbons[index] = new Ellipse();
                _canvas.Children.Add(_ribbons[index]);
            }

            // Everywhere a little light, and a band of full light that slides through.
            foreach (var (alpha, offset) in new[] { (110, 0.0), (110, 0.3), (255, 0.4), (110, 0.5), (110, 1.0) })
            {
                _glint.GradientStops.Add(new GradientStop(Color.FromArgb((byte)alpha, 0, 0, 0), offset));
            }

            _canvas.OpacityMask = _glint;
            Children.Add(_canvas);
            Lane(0x07080B, 0.7);
        }

        protected override void Build(double width, double height) =>
            _distance = Math.Sqrt(Math.Pow(width * 0.6, 2) + Math.Pow(height * 2.6, 2));

        protected override void Draw(double width, double height, double seconds, double level)
        {
            // Rings around a point above and right of the capsule, so their arcs sweep across it.
            var centre = new Point(width * 1.1, -height * 2.1);
            var thickness = height * 0.55;
            for (var index = 0; index < Count; index++)
            {
                var radius = _distance + ((index - 2) * height * 0.62) + (height * 0.08 * Math.Sin((seconds * 0.8) + index));
                var ribbon = _ribbons[index];
                ribbon.Width = ribbon.Height = radius * 2;
                Canvas.SetLeft(ribbon, centre.X - radius);
                Canvas.SetTop(ribbon, centre.Y - radius);
                ribbon.StrokeThickness = thickness;
                ribbon.Stroke = Ribbon(radius, thickness, level);
            }

            var sweep = ((seconds * 0.22) % 1.4) - 0.2;
            double[] offsets = { 0, sweep - 0.12, sweep, sweep + 0.12, 1 };
            for (var index = 0; index < offsets.Length; index++) _glint.GradientStops[index].Offset = Math.Clamp(offsets[index], 0, 1);
        }

        /// <summary>One ribbon, shaded across its width: dark, then an orange-white-blue edge, as chrome catches light.</summary>
        private static Brush Ribbon(double radius, double thickness, double level)
        {
            double At(double fraction) => Math.Clamp((radius - (thickness * fraction)) / radius, 0, 1);
            var edge = (byte)(150 + (105 * level));
            return Frozen(new RadialGradientBrush(new GradientStopCollection
            {
                new(Rgb(0x0B0C0F), At(1)),
                new(Rgb(0x23262C), At(0.55)),
                new(Rgb(0xE8A46A, edge), At(0.22)),
                new(Rgb(0xFFFFFF, edge), At(0.15)),
                new(Rgb(0x78AEFF, edge), At(0.08)),
                new(Rgb(0x0B0C0F), 1),
            }));
        }
    }

    /// <summary>A honeycomb on black, lit by a violet-blue light that wanders and brightens with the voice.</summary>
    private sealed class Hive : CapsuleArt
    {
        private readonly Rectangle _cells = new() { Fill = Solid(0xFFFFFF, 22) };
        private readonly Rectangle _lit = new();
        private readonly RadialGradientBrush _light = new()
        {
            RadiusX = 0.32,
            RadiusY = 1.4,
            GradientStops = { new(Rgb(0x6A2FD6), 0), new(Rgb(0x3F38D0, 200), 0.4), new(Rgb(0x2450D8, 90), 0.7), new(Rgb(0x2450D8, 0), 1) },
        };

        public Hive()
        {
            Background = Solid(0x050509);
            _lit.Fill = _light;
            Children.Add(_cells);
            Children.Add(_lit);
            Lane(0x050509, 0.65);
        }

        protected override void Build(double width, double height)
        {
            var mask = Honeycomb(height / 8.5);
            _cells.OpacityMask = mask;
            _lit.OpacityMask = mask;
        }

        protected override void Draw(double width, double height, double seconds, double level)
        {
            var x = 0.5 + (0.4 * Math.Sin(seconds * 0.55));
            _light.Center = _light.GradientOrigin = new Point(x, 0.55 + (0.2 * Math.Sin(seconds * 0.9)));
            _light.RadiusX = 0.28 + (0.12 * level);
            _lit.Opacity = 0.7 + (0.3 * level);
        }

        /// <summary>Hexagons with a hairline gap, tiled: pointy-top, each row shifted half a cell.</summary>
        private static Brush Honeycomb(double radius)
        {
            var across = Math.Sqrt(3) * radius;
            var tall = 3 * radius;
            var shapes = new GeometryGroup();
            foreach (var (x, y) in new[] { (0.0, 0.0), (across, 0.0), (across / 2, 1.5 * radius), (0.0, tall), (across, tall) })
            {
                var figure = new PathFigure { IsClosed = true, IsFilled = true };
                for (var corner = 0; corner < 6; corner++)
                {
                    var angle = Math.PI / 180 * ((60 * corner) - 90);
                    var point = new Point(x + (radius * 0.84 * Math.Cos(angle)), y + (radius * 0.84 * Math.Sin(angle)));
                    if (corner == 0) figure.StartPoint = point;
                    else figure.Segments.Add(new LineSegment(point, false));
                }

                shapes.Children.Add(new PathGeometry(new[] { figure }));
            }

            var tile = new Rect(0, 0, across, tall);
            return Frozen(new DrawingBrush(new GeometryDrawing(Brushes.White, null, shapes))
            {
                TileMode = TileMode.Tile,
                Viewbox = tile,
                ViewboxUnits = BrushMappingMode.Absolute,
                Viewport = tile,
                ViewportUnits = BrushMappingMode.Absolute,
                Stretch = Stretch.None,
            });
        }
    }

    /// <summary>
    /// A field of square cells drawn into a small bitmap and scaled up without smoothing,
    /// for the pixel skins: each cell is a few pixels with a one-pixel gap.
    /// </summary>
    private abstract class Cells : CapsuleArt
    {
        private readonly Image _image = new() { Stretch = Stretch.Fill, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        private WriteableBitmap? _bitmap;
        private int[] _pixels = Array.Empty<int>();
        private int _columns;
        private int _rows;

        protected Cells()
        {
            RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.NearestNeighbor);
            Children.Add(_image);
        }

        /// <summary>Bitmap pixels per cell, the last of them the gap.</summary>
        protected abstract int Pitch { get; }

        /// <summary>Rows of cells from top to bottom.</summary>
        protected abstract int RowCount { get; }

        /// <summary>Round the cells' corners by leaving the corner pixels out.</summary>
        protected virtual bool Rounded => false;

        protected override void Build(double width, double height)
        {
            var size = height / RowCount;
            _rows = RowCount;
            _columns = (int)Math.Ceiling(width / size);
            var pitch = Pitch;
            _bitmap = new WriteableBitmap(_columns * pitch, _rows * pitch, 96, 96, PixelFormats.Pbgra32, null);
            _pixels = new int[_columns * pitch * _rows * pitch];
            _image.Source = _bitmap;
            _image.Width = _columns * size;
            _image.Height = height;
        }

        protected override void Draw(double width, double height, double seconds, double level)
        {
            if (_bitmap is null) return;
            var pitch = Pitch;
            var stride = _columns * pitch;
            Array.Clear(_pixels);
            for (var row = 0; row < _rows; row++)
            {
                for (var column = 0; column < _columns; column++)
                {
                    var color = Cell(column, row, _columns, _rows, seconds, level);
                    if (color.A == 0) continue;
                    var pixel = Premultiplied(color);
                    for (var y = 0; y < pitch - 1; y++)
                    {
                        for (var x = 0; x < pitch - 1; x++)
                        {
                            var corner = (x == 0 || x == pitch - 2) && (y == 0 || y == pitch - 2);
                            if (Rounded && corner) continue;
                            _pixels[(((row * pitch) + y) * stride) + (column * pitch) + x] = pixel;
                        }
                    }
                }
            }

            _bitmap.WritePixels(new Int32Rect(0, 0, stride, _rows * pitch), _pixels, stride * 4, 0);
        }

        /// <summary>The cell's color; transparent leaves it empty.</summary>
        protected abstract Color Cell(int column, int row, int columns, int rows, double seconds, double level);

        private static int Premultiplied(Color color)
        {
            int Channel(byte value) => value * color.A / 255;
            return (color.A << 24) | (Channel(color.R) << 16) | (Channel(color.G) << 8) | Channel(color.B);
        }

        protected static Color Mix(Color from, Color to, double amount)
        {
            amount = Math.Clamp(amount, 0, 1);
            byte Channel(byte a, byte b) => (byte)Math.Round(a + ((b - a) * amount));
            return Color.FromArgb(Channel(from.A, to.A), Channel(from.R, to.R), Channel(from.G, to.G), Channel(from.B, to.B));
        }
    }

    /// <summary>A pink wave in ordered dither on navy, after a halftone poster: it rolls, and rises as you speak.</summary>
    private sealed class Halftone : Cells
    {
        // The 4 × 4 ordered-dither thresholds.
        private static readonly int[,] Bayer = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } };
        private static readonly Color Pink = Rgb(0xC21E63);

        public Halftone()
        {
            Background = Solid(0x0A1022);
            Lane(0x0A1022, 0.75);
        }

        protected override int Pitch => 4;

        protected override int RowCount => 14;

        protected override Color Cell(int column, int row, int columns, int rows, double seconds, double level)
        {
            var height = 1 - ((row + 0.5) / rows); // 0 at the bottom, 1 at the top.
            // A pink mass under the calm lane, its dithered fringe climbing around the meter as the voice comes.
            var crest = 0.42 + (0.15 * Math.Sin((column * 0.11) + (seconds * 1.1))) + (0.08 * Math.Sin((column * 0.29) - (seconds * 1.7)))
                        + (0.25 * level);
            var density = Math.Clamp(0.5 + ((crest - height) * 2.6), 0, 1);
            return (Bayer[row & 3, column & 3] + 0.5) / 16 < density ? Pink : Colors.Transparent;
        }
    }

    /// <summary>Lavender-to-pink tiles with a shimmer running across them diagonally, after a tile-grid poster.</summary>
    private sealed class Mosaic : Cells
    {
        private static readonly Color Lavender = Rgb(0xF2EEF9);
        private static readonly Color Rose = Rgb(0xF6B3CC);
        private static readonly Color Pink = Rgb(0xF07AA5);

        public Mosaic()
        {
            Background = Solid(0xFBF7FB);
            Lane(0xFBF7FB, 0.5);
        }

        protected override int Pitch => 8;

        protected override int RowCount => 6;

        protected override bool Rounded => true;

        protected override Color Cell(int column, int row, int columns, int rows, double seconds, double level)
        {
            var across = (column + (row * 0.6)) / Math.Max(1, columns + (rows * 0.6) - 1);
            var color = across < 0.5 ? Mix(Lavender, Rose, across * 2) : Mix(Rose, Pink, (across - 0.5) * 2);
            var shimmer = Math.Pow(0.5 + (0.5 * Math.Sin((column * 0.45) + (row * 0.9) - (seconds * 2.4))), 6);
            return Mix(color, Colors.White, (0.45 * shimmer) + (0.1 * level));
        }
    }

    /// <summary>A pale sky over a terracotta horizon, after a landscape poster: soft clouds drift by.</summary>
    private sealed class Mesa : CapsuleArt
    {
        private readonly Ellipse[] _clouds = Enumerable.Range(0, 4).Select(_ => new Ellipse { Fill = Glow(0xFFFFFF, 255) }).ToArray();
        // A far hill, sunlit, and the near land in shadow.
        private readonly Path _far = new() { Fill = Frozen(new LinearGradientBrush(Rgb(0xE6A07E), Rgb(0xCF7A5C), 90)) };
        private readonly Path _land = new()
        {
            Fill = Frozen(new LinearGradientBrush(Rgb(0xB8473A), Rgb(0x7E2820), 90)),
            Stroke = Solid(0xE08A63),
        };
        private readonly Canvas _canvas = new();

        public Mesa()
        {
            Background = Frozen(new LinearGradientBrush(Rgb(0xC5D6E6), Rgb(0xF2E9E2), 90));
            foreach (var cloud in _clouds) _canvas.Children.Add(cloud);
            _canvas.Children.Add(_far);
            _canvas.Children.Add(_land);
            Children.Add(_canvas);
            Children.Add(new Rectangle { Fill = Grain(), Opacity = 0.25 });
        }

        protected override void Build(double width, double height)
        {
            // Rolling land in the capsule's bottom margin, under the words.
            _far.Data = Hills(width, height, 0.8, 0.06, 1.7, 2.1);
            _land.Data = Hills(width, height, 0.88, 0.04, 2.6, 0.8);
            _land.StrokeThickness = Math.Max(0.8, height * 0.02);
        }

        private static Geometry Hills(double width, double height, double level, double rise, double waves, double shift)
        {
            var figure = new PathFigure { StartPoint = new Point(0, height), IsClosed = true, IsFilled = true };
            for (var step = 0; step <= 48; step++)
            {
                var x = width * step / 48;
                var y = height * (level + (rise * Math.Sin((x / width * Math.PI * waves) + shift)) + (0.015 * Math.Sin(x / width * Math.PI * 7.0)));
                figure.Segments.Add(new LineSegment(new Point(x, y), true));
            }

            figure.Segments.Add(new LineSegment(new Point(width, height), false));
            return new PathGeometry(new[] { figure });
        }

        protected override void Draw(double width, double height, double seconds, double level)
        {
            for (var index = 0; index < _clouds.Length; index++)
            {
                var cloud = _clouds[index];
                cloud.Width = width * (0.38 + (0.1 * index));
                cloud.Height = height * (0.95 + (0.2 * (index % 2)));
                var travel = (width * 1.5) + cloud.Width;
                var x = (((index * width * 0.42) - (seconds * width * 0.035)) % travel + travel) % travel - cloud.Width;
                Canvas.SetLeft(cloud, x);
                Canvas.SetTop(cloud, (height * (0.05 + (0.18 * (index % 3)))) - (cloud.Height * 0.2));
            }
        }
    }
}
