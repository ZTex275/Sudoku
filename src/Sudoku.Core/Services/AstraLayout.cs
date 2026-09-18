namespace Sudoku.Core.Services;

public static class AstraLayout
{
    public const int Sectors = 14;
    public const int Rings = 7;
    public const int Cells = Sectors * Rings;
    public const double ViewOrigin = -1.14;
    public const double ViewSize = 2.28;

    private const double TwoPi = Math.PI * 2;
    private const double Delta = TwoPi / Sectors;
    private const double RadialSpan = 0.155;
    private const double Ring0Radius = 0.9225;

    public static int Index(int ring, int sector)
    {
        sector %= Sectors;
        if (sector < 0)
            sector += Sectors;
        return ring * Sectors + sector;
    }

    public static (int Ring, int Sector) Coords(int cell) => (cell / Sectors, cell % Sectors);

    public static List<int[]> AllGroups()
    {
        var groups = new List<int[]>(Sectors * 2);
        for (var s = 0; s < Sectors; s++)
        {
            var cw = new int[Rings];
            var ccw = new int[Rings];
            for (var r = 0; r < Rings; r++)
            {
                cw[r] = Index(r, s + r / 2);
                ccw[r] = Index(r, s - (r + 1) / 2);
            }

            groups.Add(cw);
            groups.Add(ccw);
        }

        return groups;
    }

    public static int[] PatternSolution(Random rng)
    {
        var grid = new int[Cells];
        var rot = rng.Next(Sectors);
        var map = Enumerable.Range(1, Rings).OrderBy(_ => rng.Next()).ToArray();
        for (var r = 0; r < Rings; r++)
        {
            for (var s = 0; s < Sectors; s++)
            {
                var value = Mod(r + s + rot + 4 * (r % 2), Rings);
                grid[Index(r, s)] = map[value];
            }
        }

        return grid;
    }

    public static string PolygonPoints(int ring, int sector) =>
        ShapeGeom.SvgPoints(Vertices(ring, sector));

    public static string CssClip(int ring, int sector, double inset = 0.07) =>
        ShapeGeom.CssPolygon(ShapeGeom.Inset(Vertices(ring, sector), inset), ViewOrigin, ViewSize);

    public static (double X, double Y) CssCenter(int ring, int sector)
    {
        var (x, y) = Center(ring, sector);
        return ShapeGeom.ToPct(x, y, ViewOrigin, ViewSize);
    }

    public static int HitTest(double nx, double ny)
    {
        var x = ViewOrigin + nx * ViewSize;
        var y = ViewOrigin + ny * ViewSize;
        for (var i = Cells - 1; i >= 0; i--)
        {
            var (ring, sector) = Coords(i);
            if (ShapeGeom.PointInConvex(x, y, Vertices(ring, sector)))
                return i;
        }

        return -1;
    }

    public static (double X, double Y) Center(int ring, int sector)
    {
        var (ang, rad) = PolarCenter(ring, sector);
        return ToXy(ang, rad);
    }

    public static IReadOnlyList<(int Ring, int Sector)> Neighbors(int ring, int sector)
    {
        var list = new List<(int, int)>(4);
        if (ring % 2 == 0)
        {
            if (ring + 1 < Rings)
            {
                list.Add((ring + 1, sector));
                list.Add((ring + 1, sector - 1));
            }

            if (ring - 1 >= 0)
            {
                list.Add((ring - 1, sector));
                list.Add((ring - 1, sector - 1));
            }
        }
        else
        {
            if (ring + 1 < Rings)
            {
                list.Add((ring + 1, sector));
                list.Add((ring + 1, sector + 1));
            }

            if (ring - 1 >= 0)
            {
                list.Add((ring - 1, sector));
                list.Add((ring - 1, sector + 1));
            }
        }

        return list;
    }

    private static (double X, double Y)[] Vertices(int ring, int sector)
    {
        var (ang, rad) = PolarCenter(ring, sector);
        var half = RadialSpan / 2;
        return
        [
            ToXy(ang, rad + half),
            ToXy(ang + Delta / 2, rad),
            ToXy(ang, rad - half),
            ToXy(ang - Delta / 2, rad)
        ];
    }

    private static (double Ang, double Rad) PolarCenter(int ring, int sector)
    {
        var ang = ring % 2 == 0 ? sector * Delta : (sector + 0.5) * Delta;
        var rad = Ring0Radius - ring * (RadialSpan / 2);
        return (ang, rad);
    }

    private static (double X, double Y) ToXy(double ang, double rad) =>
        (rad * Math.Sin(ang), -rad * Math.Cos(ang));

    private static int Mod(int value, int n)
    {
        var m = value % n;
        return m < 0 ? m + n : m;
    }
}
