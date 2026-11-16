namespace PaletteMaker.Core;

/// <summary>Ham RGBA (alfası düz, premultiply edilmemiş) piksel tamponundan palet çıkarır.</summary>
public static class PaletteExtractor
{
    private const int InitialClusters = 48;
    private const int MaxIterations = 30;
    private const int AlphaCutoff = 128;
    private const double NoiseShare = 0.01;
    private const double LightnessWeight = 0.5;
    private const double MinThreshold = 0.02;
    private const double MaxThreshold = 0.20;

    private sealed class Cluster
    {
        public Lab Center;
        public double Weight;
    }

    public static IReadOnlyList<PaletteColor> Extract(ReadOnlySpan<byte> rgba, PaletteOptions? options = null)
    {
        options ??= new PaletteOptions();

        // 3) 5 bitlik RGB histogramı; her kutunun gerçek ortalama rengi korunur.
        var bins = new Dictionary<int, (long Count, long R, long G, long B)>();
        long total = 0;
        for (int i = 0; i + 3 < rgba.Length; i += 4)
        {
            if (rgba[i + 3] < AlphaCutoff) continue;
            byte r = rgba[i], g = rgba[i + 1], b = rgba[i + 2];
            int key = ((r >> 3) << 10) | ((g >> 3) << 5) | (b >> 3);
            bins.TryGetValue(key, out var e);
            bins[key] = (e.Count + 1, e.R + r, e.G + g, e.B + b);
            total++;
        }
        if (total == 0) return Array.Empty<PaletteColor>();

        var points = new List<(Lab Lab, double W)>(bins.Count);
        foreach (var (_, e) in bins.OrderBy(kv => kv.Key))
            points.Add((ColorMath.RgbToOklab((double)e.R / e.Count, (double)e.G / e.Count, (double)e.B / e.Count), e.Count));

        // 4) Ağırlıklı k-means (k-means++ başlangıcı, sabit tohum).
        var clusters = KMeans(points, Math.Min(InitialClusters, points.Count));

        // 5) Gürültü ayıklama.
        var kept = clusters.Where(c => c.Weight / total >= NoiseShare).ToList();
        if (kept.Count == 0) kept.Add(clusters.OrderByDescending(c => c.Weight).First());

        // 6-7) Ton birleştirme ve sınır.
        double threshold = MinThreshold + (MaxThreshold - MinThreshold) * options.MergeStrength;
        while (kept.Count > 1)
        {
            (int i, int j, double d) = ClosestPair(kept);
            if (d >= threshold && kept.Count <= options.MaxColors) break;
            Merge(kept, i, j);
        }

        // 8) Çıktı: paya göre sıralı, paylar toplamı 1.
        double sum = kept.Sum(c => c.Weight);
        return kept.OrderByDescending(c => c.Weight)
            .Select(c =>
            {
                var (r, g, b) = ColorMath.OklabToRgb(c.Center);
                return new PaletteColor(r, g, b, c.Weight / sum);
            })
            .ToList();
    }

    private static double Distance(Lab x, Lab y)
    {
        double dl = (x.L - y.L) * LightnessWeight, da = x.A - y.A, db = x.B - y.B;
        return Math.Sqrt(dl * dl + da * da + db * db);
    }

    private static double Dist2Plain(Lab x, Lab y)
    {
        double dl = x.L - y.L, da = x.A - y.A, db = x.B - y.B;
        return dl * dl + da * da + db * db;
    }

    private static (int, int, double) ClosestPair(List<Cluster> cs)
    {
        int bi = 0, bj = 1; double best = double.MaxValue;
        for (int i = 0; i < cs.Count; i++)
            for (int j = i + 1; j < cs.Count; j++)
            {
                double d = Distance(cs[i].Center, cs[j].Center);
                if (d < best) { best = d; bi = i; bj = j; }
            }
        return (bi, bj, best);
    }

    private static void Merge(List<Cluster> cs, int i, int j)
    {
        var a = cs[i]; var b = cs[j];
        double w = a.Weight + b.Weight;
        a.Center = new Lab(
            (a.Center.L * a.Weight + b.Center.L * b.Weight) / w,
            (a.Center.A * a.Weight + b.Center.A * b.Weight) / w,
            (a.Center.B * a.Weight + b.Center.B * b.Weight) / w);
        a.Weight = w;
        cs.RemoveAt(j);
    }

    private static List<Cluster> KMeans(List<(Lab Lab, double W)> pts, int k)
    {
        var rng = new Random(12345);
        var centers = new List<Lab>(k);

        // k-means++: ilk merkez ağırlığa göre, sonrakiler uzaklık² × ağırlık ile.
        centers.Add(pts[PickWeighted(rng, pts.Select(p => p.W).ToArray())].Lab);
        var minD = pts.Select(p => Dist2Plain(p.Lab, centers[0])).ToArray();
        while (centers.Count < k)
        {
            var wts = new double[pts.Count];
            for (int i = 0; i < pts.Count; i++) wts[i] = minD[i] * pts[i].W;
            if (wts.Sum() <= 0) break; // kalan noktalar mevcut merkezlerle çakışıyor
            var c = pts[PickWeighted(rng, wts)].Lab;
            centers.Add(c);
            for (int i = 0; i < pts.Count; i++) minD[i] = Math.Min(minD[i], Dist2Plain(pts[i].Lab, c));
        }

        var assign = new int[pts.Count];
        Array.Fill(assign, -1);
        for (int iter = 0; iter < MaxIterations; iter++)
        {
            bool changed = false;
            for (int i = 0; i < pts.Count; i++)
            {
                int best = 0; double bd = double.MaxValue;
                for (int c = 0; c < centers.Count; c++)
                {
                    double d = Dist2Plain(pts[i].Lab, centers[c]);
                    if (d < bd) { bd = d; best = c; }
                }
                if (assign[i] != best) { assign[i] = best; changed = true; }
            }
            if (!changed) break;

            var sums = new (double L, double A, double B, double W)[centers.Count];
            for (int i = 0; i < pts.Count; i++)
            {
                ref var s = ref sums[assign[i]];
                s.L += pts[i].Lab.L * pts[i].W; s.A += pts[i].Lab.A * pts[i].W; s.B += pts[i].Lab.B * pts[i].W; s.W += pts[i].W;
            }
            for (int c = 0; c < centers.Count; c++)
                if (sums[c].W > 0) centers[c] = new Lab(sums[c].L / sums[c].W, sums[c].A / sums[c].W, sums[c].B / sums[c].W);
        }

        var result = new List<Cluster>();
        for (int c = 0; c < centers.Count; c++)
        {
            double w = 0, l = 0, a = 0, b = 0;
            for (int i = 0; i < pts.Count; i++)
                if (assign[i] == c) { w += pts[i].W; l += pts[i].Lab.L * pts[i].W; a += pts[i].Lab.A * pts[i].W; b += pts[i].Lab.B * pts[i].W; }
            if (w > 0) result.Add(new Cluster { Center = new Lab(l / w, a / w, b / w), Weight = w });
        }
        return result;
    }

    private static int PickWeighted(Random rng, double[] weights)
    {
        double sum = 0; foreach (var w in weights) sum += w;
        double target = rng.NextDouble() * sum, acc = 0;
        for (int i = 0; i < weights.Length; i++)
        {
            acc += weights[i];
            if (acc >= target && weights[i] > 0) return i;
        }
        return weights.Length - 1;
    }
}
