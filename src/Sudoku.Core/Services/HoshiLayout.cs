namespace Sudoku.Core.Services;

public static class HoshiLayout
{
    public const int Cells = 54;
    public const int Sectors = 6;
    public const int Digits = 9;

    private const double Side = 1.0;
    private const double Height = 0.8660254037844386;

    private static readonly CellGeom[] Geom;
    private static readonly int[][] GroupsInternal;
    private static readonly int[][] NeighborInternal;
    private static readonly string[] Outlines;
    private static readonly string HexHole;

    public static IReadOnlyList<int[]> AllGroups() => GroupsInternal;
    public static int Sector(int cell) => Geom[cell].Sector;
    public static string PolygonPoints(int cell) => Geom[cell].Points;
    public static (double X, double Y) Center(int cell) => (Geom[cell].Cx, Geom[cell].Cy);
    public static IReadOnlyList<int> Neighbors(int cell) => NeighborInternal[cell];
    public static string SectorOutline(int sector) => Outlines[sector];
    public static string HolePoints => HexHole;
    public static string ViewBox => "-5.55 -5.55 11.1 11.1";

    static HoshiLayout()
    {
        var hex = new (double X, double Y)[6];
        for (var k = 0; k < 6; k++)
        {
            var ang = k * Math.PI / 3;
            hex[k] = (3 * Side * Math.Cos(ang), 3 * Side * Math.Sin(ang));
        }

        var cells = new List<CellGeom>(Cells);
        var outlines = new string[6];
        for (var k = 0; k < 6; k++)
        {
            var a = hex[k];
            var b = hex[(k + 1) % 6];
            var c = Third(a, b);
            outlines[k] = Join(Flip(c), Flip(a), Flip(b));

            (double X, double Y) P(int i, int j)
            {
                var t = 3 - i - j;
                return ((t * a.X + i * b.X + j * c.X) / 3, (t * a.Y + i * b.Y + j * c.Y) / 3);
            }

            for (var i = 0; i < 3; i++)
            {
                for (var j = 0; j < 3 - i; j++)
                    cells.Add(Make(k, P(i, j), P(i + 1, j), P(i, j + 1)));
            }

            for (var i = 0; i < 2; i++)
            {
                for (var j = 0; j < 2 - i; j++)
                    cells.Add(Make(k, P(i + 1, j), P(i, j + 1), P(i + 1, j + 1)));
            }
        }

        Geom = cells.ToArray();
        Outlines = outlines;
        HexHole = string.Join(" ", Enumerable.Range(0, 6).Select(i => Fmt(Flip(hex[i]))));

        GroupsInternal = BuildGroups(Geom);
        NeighborInternal = BuildNeighbors(Geom);
    }

    public static bool HasCenterGap(int[] group)
    {
        if (group.Length != 8)
            return false;
        var xs = group.Select(i => Geom[i].Cx).OrderBy(x => x).ToArray();
        if (xs[0] >= -1 || xs[^1] <= 1)
            return false;
        for (var i = 1; i < xs.Length; i++)
        {
            if (xs[i] - xs[i - 1] > 2)
                return true;
        }

        return false;
    }

    private static CellGeom Make(int sector, (double X, double Y) a, (double X, double Y) b, (double X, double Y) c)
    {
        var cx = (a.X + b.X + c.X) / 3;
        var cy = (a.Y + b.Y + c.Y) / 3;
        return new CellGeom
        {
            Sector = sector,
            Cx = cx,
            Cy = cy,
            Ax = a.X,
            Ay = a.Y,
            Bx = b.X,
            By = b.Y,
            Dx = c.X,
            Dy = c.Y,
            Points = Join(Flip(a), Flip(b), Flip(c))
        };
    }

    private static int[][] BuildGroups(CellGeom[] geom)
    {
        var groups = new List<int[]>(36);
        for (var s = 0; s < 6; s++)
            groups.Add(Enumerable.Range(0, geom.Length).Where(i => geom[i].Sector == s).ToArray());

        foreach (var dir in new[] { 0.0, 60.0, 120.0 })
        {
            var buckets = new Dictionary<int, List<int>>();
            var ang = dir * Math.PI / 180;
            var ca = Math.Cos(ang);
            var sa = Math.Sin(ang);
            var proj = new double[geom.Length];
            var pmin = double.PositiveInfinity;
            for (var i = 0; i < geom.Length; i++)
            {
                proj[i] = -geom[i].Cx * sa + geom[i].Cy * ca;
                if (proj[i] < pmin)
                    pmin = proj[i];
            }

            for (var i = 0; i < geom.Length; i++)
            {
                var key = (int)Math.Floor((proj[i] - pmin + 1e-9) / Height);
                if (!buckets.TryGetValue(key, out var list))
                {
                    list = [];
                    buckets[key] = list;
                }

                list.Add(i);
            }

            foreach (var list in buckets.Values)
            {
                if (list.Count >= 2)
                    groups.Add(list.ToArray());
            }
        }

        return groups.ToArray();
    }

    private static int[][] BuildNeighbors(CellGeom[] geom)
    {
        var result = new int[geom.Length][];
        for (var i = 0; i < geom.Length; i++)
        {
            var nbs = new List<int>(3);
            for (var j = 0; j < geom.Length; j++)
            {
                if (i != j && ShareEdge(geom[i], geom[j]))
                    nbs.Add(j);
            }

            result[i] = nbs.ToArray();
        }

        return result;
    }

    private static bool ShareEdge(CellGeom a, CellGeom b)
    {
        var n = 0;
        foreach (var (x, y) in a.Verts())
        {
            foreach (var (u, v) in b.Verts())
            {
                if (Math.Abs(x - u) < 1e-6 && Math.Abs(y - v) < 1e-6)
                    n++;
            }
        }

        return n == 2;
    }

    private static (double X, double Y) Third((double X, double Y) a, (double X, double Y) b)
    {
        var mx = (a.X + b.X) / 2;
        var my = (a.Y + b.Y) / 2;
        var rx = -(b.Y - a.Y);
        var ry = b.X - a.X;
        var scale = Math.Sqrt(3) / 2;
        var cx = mx + rx * scale;
        var cy = my + ry * scale;
        if (cx * cx + cy * cy < mx * mx + my * my)
            return (mx - rx * scale, my - ry * scale);
        return (cx, cy);
    }

    private static (double X, double Y) Flip((double X, double Y) p) => (p.X, -p.Y);

    private static string Join(params (double X, double Y)[] pts) =>
        string.Join(" ", pts.Select(Fmt));

    private static string Fmt((double X, double Y) p) => $"{p.X:0.###},{p.Y:0.###}";

    private sealed class CellGeom
    {
        public int Sector;
        public double Cx, Cy, Ax, Ay, Bx, By, Dx, Dy;
        public string Points = "";

        public IEnumerable<(double X, double Y)> Verts()
        {
            yield return (Ax, Ay);
            yield return (Bx, By);
            yield return (Dx, Dy);
        }
    }
}
