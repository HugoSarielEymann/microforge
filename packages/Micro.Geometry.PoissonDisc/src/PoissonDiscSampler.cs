namespace Micro.Geometry.PoissonDisc;

/// <summary>Un point du plan.</summary>
/// <param name="X">Abscisse.</param>
/// <param name="Y">Ordonnée.</param>
public readonly record struct SamplePoint(double X, double Y);

/// <summary>Le résultat d'un tirage : les points, et la distance minimale qui les sépare.</summary>
/// <param name="Points">Exactement le nombre de points demandé, dans l'ordre du tirage.</param>
/// <param name="Distance">
/// Distance minimale garantie entre deux points : celle de départ, ou moins si elle a dû être
/// resserrée pour que tous les points tiennent.
/// </param>
public sealed record SampleResult(IReadOnlyList<SamplePoint> Points, double Distance);

/// <summary>
/// Tire un nombre <b>exact</b> de points dans une ellipse ou un rectangle, deux à deux espacés
/// d'une distance minimale (disques de Poisson) : un semis régulier sans être une grille, qui ne
/// laisse ni amas ni trous — ce que rend impossible un tirage uniforme.
/// </summary>
/// <remarks>
/// <para>
/// La distance de départ découle de la surface et du nombre de points
/// (<see cref="PoissonDiscOptions.AreaPerPoint"/>). Si les points ne tiennent pas, elle se
/// resserre (<see cref="PoissonDiscOptions.ShrinkFactor"/>) et le tirage reprend : le nombre de
/// points est garanti, la distance est la plus grande trouvée.
/// </para>
/// <para>
/// Déterministe : l'aléa vient du <see cref="Random"/> fourni. Un <c>new Random(graine)</c>
/// donne le même semis à chaque lancement — des étoiles qui restent à leur place.
/// </para>
/// </remarks>
public static class PoissonDiscSampler
{
    /// <summary>Tire des points dans une ellipse.</summary>
    /// <param name="count">Nombre de points voulus (0 rend une liste vide).</param>
    /// <param name="centerX">Centre de l'ellipse.</param>
    /// <param name="centerY">Centre de l'ellipse.</param>
    /// <param name="radiusX">Demi-axe horizontal, positif ou nul.</param>
    /// <param name="radiusY">Demi-axe vertical, positif ou nul.</param>
    /// <param name="random">La source d'aléa (une graine fixe pour un résultat stable).</param>
    /// <param name="options">Réglages ; <see cref="PoissonDiscOptions.Default"/> si omis.</param>
    /// <returns>Exactement <paramref name="count"/> points, tous dans l'ellipse.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="random"/> est nul.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Si <paramref name="count"/> est négatif ou dépasse <see cref="PoissonDiscOptions.MaximumCount"/>,
    /// si une coordonnée n'est pas finie, si un demi-axe est négatif, ou si les réglages sont
    /// incohérents.
    /// </exception>
    public static SampleResult SampleEllipse(int count, double centerX, double centerY, double radiusX, double radiusY, Random random, PoissonDiscOptions? options = null)
    {
        options = Check(count, random, options);
        Finite(centerX, nameof(centerX));
        Finite(centerY, nameof(centerY));
        Size(radiusX, nameof(radiusX));
        Size(radiusY, nameof(radiusY));
        double area = Area(Math.PI * radiusX * radiusY);
        return Sample(count, area, options, () =>
        {
            double angle = random.NextDouble() * Math.PI * 2;
            double radius = Math.Sqrt(random.NextDouble());
            return new SamplePoint(centerX + (Math.Cos(angle) * radius * radiusX), centerY + (Math.Sin(angle) * radius * radiusY));
        });
    }

    /// <summary>Tire des points dans un rectangle.</summary>
    /// <param name="count">Nombre de points voulus (0 rend une liste vide).</param>
    /// <param name="left">Bord gauche.</param>
    /// <param name="top">Bord haut.</param>
    /// <param name="width">Largeur, positive ou nulle.</param>
    /// <param name="height">Hauteur, positive ou nulle.</param>
    /// <param name="random">La source d'aléa (une graine fixe pour un résultat stable).</param>
    /// <param name="options">Réglages ; <see cref="PoissonDiscOptions.Default"/> si omis.</param>
    /// <returns>Exactement <paramref name="count"/> points, tous dans le rectangle.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="random"/> est nul.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Si <paramref name="count"/> est négatif ou dépasse <see cref="PoissonDiscOptions.MaximumCount"/>,
    /// si une coordonnée n'est pas finie, si une dimension est négative, ou si les réglages sont
    /// incohérents.
    /// </exception>
    public static SampleResult SampleRectangle(int count, double left, double top, double width, double height, Random random, PoissonDiscOptions? options = null)
    {
        options = Check(count, random, options);
        Finite(left, nameof(left));
        Finite(top, nameof(top));
        Size(width, nameof(width));
        Size(height, nameof(height));
        return Sample(count, Area(width * height), options, () =>
            new SamplePoint(left + (random.NextDouble() * width), top + (random.NextDouble() * height)));
    }

    private static PoissonDiscOptions Check(int count, Random random, PoissonDiscOptions? options)
    {
        ArgumentNullException.ThrowIfNull(random);
        options ??= PoissonDiscOptions.Default;
        options.Validate();
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, options.MaximumCount);
        return options;
    }

    private static void Finite(double value, string name)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(name, value, "La coordonnée doit être un nombre fini.");
        }
    }

    private static void Size(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "La dimension doit être un nombre fini positif ou nul.");
        }
    }

    /// <summary>Une surface qui déborde (dimensions démesurées) ne permettrait aucun tirage : elle est refusée.</summary>
    private static double Area(double area)
    {
        if (!double.IsFinite(area))
        {
            throw new ArgumentOutOfRangeException(nameof(area), area, "Les dimensions sont trop grandes : leur surface déborde.");
        }

        return area;
    }

    private static SampleResult Sample(int count, double area, PoissonDiscOptions options, Func<SamplePoint> draw)
    {
        if (count == 0)
        {
            return new SampleResult([], 0);
        }

        double distance = area > 0 ? Math.Sqrt(area / (count * options.AreaPerPoint)) : 0;
        while (true)
        {
            List<SamplePoint> points = Attempt(count, distance, options.AttemptsPerPoint, draw);
            if (points.Count == count)
            {
                return new SampleResult(points, distance);
            }

            // Une surface nulle (ou une distance devenue infime) accepte tout point : le tirage
            // aboutit toujours au tour suivant.
            distance = distance < 1e-12 ? 0 : distance * options.ShrinkFactor;
        }
    }

    /// <summary>Un tour de tirage à distance fixe, accéléré par une grille (cellule = distance / √2).</summary>
    private static List<SamplePoint> Attempt(int count, double distance, int attemptsPerPoint, Func<SamplePoint> draw)
    {
        List<SamplePoint> points = new(count);
        if (distance <= 0)
        {
            while (points.Count < count)
            {
                points.Add(draw());
            }

            return points;
        }

        double cell = distance / Math.Sqrt(2);
        Dictionary<(long, long), SamplePoint> grid = [];
        double squared = distance * distance;
        long attempts = (long)attemptsPerPoint * count;
        for (long attempt = 0; attempt < attempts && points.Count < count; attempt++)
        {
            SamplePoint candidate = draw();
            long gx = (long)Math.Floor(candidate.X / cell);
            long gy = (long)Math.Floor(candidate.Y / cell);
            if (Fits(grid, gx, gy, candidate, squared))
            {
                grid[(gx, gy)] = candidate;
                points.Add(candidate);
            }
        }

        return points;
    }

    private static bool Fits(Dictionary<(long, long), SamplePoint> grid, long gx, long gy, SamplePoint candidate, double squared)
    {
        for (long x = gx - 2; x <= gx + 2; x++)
        {
            for (long y = gy - 2; y <= gy + 2; y++)
            {
                if (grid.TryGetValue((x, y), out SamplePoint other))
                {
                    double dx = other.X - candidate.X;
                    double dy = other.Y - candidate.Y;
                    if ((dx * dx) + (dy * dy) < squared)
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }
}
