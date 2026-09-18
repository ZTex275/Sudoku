using System.Globalization;

namespace Sudoku.Core.Services;

public static class ShapeGeom
{
    public static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string CssPolygon(IEnumerable<(double X, double Y)> pts, double origin, double size)
    {
        var parts = pts.Select(p =>
        {
            var x = (p.X - origin) / size * 100;
            var y = (p.Y - origin) / size * 100;
            return $"{Fmt(x)}% {Fmt(y)}%";
        });
        return "polygon(" + string.Join(", ", parts) + ")";
    }

    public static string SvgPoints(IEnumerable<(double X, double Y)> pts) =>
        string.Join(" ", pts.Select(p => $"{Fmt(p.X)},{Fmt(p.Y)}"));

    public static (double X, double Y) ToPct(double x, double y, double origin, double size) =>
        ((x - origin) / size * 100, (y - origin) / size * 100);

    public static (double X, double Y)[] Inset((double X, double Y)[] pts, double t)
    {
        if (pts.Length == 0 || t == 0)
            return pts;

        var cx = 0.0;
        var cy = 0.0;
        foreach (var p in pts)
        {
            cx += p.X;
            cy += p.Y;
        }

        cx /= pts.Length;
        cy /= pts.Length;
        return pts.Select(p => (p.X + (cx - p.X) * t, p.Y + (cy - p.Y) * t)).ToArray();
    }

    public static bool PointInConvex(double x, double y, IReadOnlyList<(double X, double Y)> pts)
    {
        if (pts.Count < 3)
            return false;

        var sign = 0;
        for (var i = 0; i < pts.Count; i++)
        {
            var (ax, ay) = pts[i];
            var (bx, by) = pts[(i + 1) % pts.Count];
            var cross = (bx - ax) * (y - ay) - (by - ay) * (x - ax);
            if (Math.Abs(cross) < 1e-9)
                continue;
            var s = cross > 0 ? 1 : -1;
            if (sign == 0)
                sign = s;
            else if (s != sign)
                return false;
        }

        return true;
    }

    public static string Fmt(double value) => value.ToString("0.###", Invariant);

    public static string Pct(double value) => Fmt(value) + "%";
}
