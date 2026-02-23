using Avalonia;
using Hive.Common.Models;

namespace Hive.Common.Services;

/// <summary>
/// Provides mathematical utilities for hexagonal grid calculations.
/// Uses flat-top hexagon orientation.
/// </summary>
public static class HexMath
{
    private static readonly double Sqrt3 = Math.Sqrt(3);

    /// <summary>
    /// Converts a hex coordinate to pixel position (center of the hexagon).
    /// Uses flat-top orientation.
    /// </summary>
    public static Point HexToPixel(HexCoordinate hex, double size, Point origin)
    {
        double x = size * (3.0 / 2 * hex.Q);
        double y = size * (Sqrt3 / 2 * hex.Q + Sqrt3 * hex.R);
        return new Point(x + origin.X, y + origin.Y);
    }

    /// <summary>
    /// Converts a pixel position to the nearest hex coordinate.
    /// Uses flat-top orientation.
    /// </summary>
    public static HexCoordinate PixelToHex(Point pixel, double size, Point origin)
    {
        double px = pixel.X - origin.X;
        double py = pixel.Y - origin.Y;

        double q = (2.0 / 3 * px) / size;
        double r = (-1.0 / 3 * px + Sqrt3 / 3 * py) / size;

        return HexRound(q, r);
    }

    /// <summary>
    /// Rounds fractional hex coordinates to the nearest integer hex coordinate.
    /// </summary>
    public static HexCoordinate HexRound(double q, double r)
    {
        double s = -q - r;

        int qi = (int)Math.Round(q);
        int ri = (int)Math.Round(r);
        int si = (int)Math.Round(s);

        double qDiff = Math.Abs(qi - q);
        double rDiff = Math.Abs(ri - r);
        double sDiff = Math.Abs(si - s);

        if (qDiff > rDiff && qDiff > sDiff)
        {
            qi = -ri - si;
        }
        else if (rDiff > sDiff)
        {
            ri = -qi - si;
        }

        return new HexCoordinate(qi, ri);
    }

    /// <summary>
    /// Gets the 6 corner points of a hexagon (flat-top orientation).
    /// </summary>
    public static Point[] GetHexCorners(Point center, double size)
    {
        var corners = new Point[6];
        for (int i = 0; i < 6; i++)
        {
            double angle = Math.PI / 3 * i; // Start at 0 degrees for flat-top
            corners[i] = new Point(
                center.X + size * Math.Cos(angle),
                center.Y + size * Math.Sin(angle));
        }
        return corners;
    }

    /// <summary>
    /// Gets the center point of a triplet (the gap between 3 adjacent hexagons).
    /// Triplets are the triangular spaces between hexagons where players tap.
    /// </summary>
    public static Point GetTripletCenter(HexCoordinate[] triplet, double size, Point origin)
    {
        double sumX = 0, sumY = 0;
        foreach (var hex in triplet)
        {
            var point = HexToPixel(hex, size, origin);
            sumX += point.X;
            sumY += point.Y;
        }
        return new Point(sumX / triplet.Length, sumY / triplet.Length);
    }

    /// <summary>
    /// Finds the triplet (3 adjacent hexagons) that contains the given pixel point.
    /// Uses triangle gap centers for more intuitive hit detection.
    /// Returns null if the point is not within a valid triplet area.
    /// </summary>
    public static HexCoordinate[]? GetTripletAtPoint(Point pixel, double size, Point origin, Func<HexCoordinate, bool> isValidHex)
    {
        // Get all valid triplets and find the one whose center is closest to the tap point
        var allTriplets = GetAllTripletCenters(size, origin, isValidHex);
        
        if (allTriplets.Count == 0)
        {
            return null;
        }

        // Find the nearest triplet center
        HexCoordinate[]? nearestTriplet = null;
        double nearestDistance = double.MaxValue;
        
        foreach (var (triplet, center) in allTriplets)
        {
            double dx = pixel.X - center.X;
            double dy = pixel.Y - center.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestTriplet = triplet;
            }
        }

        // Use a generous touch radius - approximately the size of the triangular gap
        // The gap radius is roughly size * 0.6 (distance from triplet center to edge)
        double touchRadius = size * 0.8;
        
        if (nearestDistance <= touchRadius && nearestTriplet != null)
        {
            return nearestTriplet;
        }

        return null;
    }

    /// <summary>
    /// Gets all valid triplet centers for hit detection.
    /// Each triplet is the triangular gap between 3 adjacent hexagons.
    /// </summary>
    public static List<(HexCoordinate[] Triplet, Point Center)> GetAllTripletCenters(
        double size, Point origin, Func<HexCoordinate, bool> isValidHex)
    {
        var triplets = new List<(HexCoordinate[] Triplet, Point Center)>();
        var seen = new HashSet<string>();

        // Enumerate triplets by checking each hex and its adjacent pairs
        // For each hex, check the 6 triangular gaps around it (each formed by 2 consecutive neighbors)
        // We use a canonical key to avoid duplicates
        
        // Get all valid hex coordinates by testing a reasonable range
        var validHexes = new List<HexCoordinate>();
        for (int r = -20; r <= 20; r++)
        {
            for (int q = -20; q <= 20; q++)
            {
                var coord = new HexCoordinate(q, r);
                if (isValidHex(coord))
                {
                    validHexes.Add(coord);
                }
            }
        }

        foreach (var hex in validHexes)
        {
            // Check each pair of consecutive neighbors (6 pairs form 6 triangular gaps)
            for (int dir = 0; dir < 6; dir++)
            {
                var neighbor1 = hex.GetNeighbor(dir);
                var neighbor2 = hex.GetNeighbor((dir + 1) % 6);

                // Skip if any hex is invalid
                if (!isValidHex(neighbor1) || !isValidHex(neighbor2))
                {
                    continue;
                }

                // Create canonical key to avoid duplicates (sort by Q, then R)
                var sorted = new[] { hex, neighbor1, neighbor2 }
                    .OrderBy(c => c.Q)
                    .ThenBy(c => c.R)
                    .ToArray();
                var key = $"{sorted[0].Q},{sorted[0].R}|{sorted[1].Q},{sorted[1].R}|{sorted[2].Q},{sorted[2].R}";

                if (!seen.Add(key))
                {
                    continue; // Already added this triplet
                }

                // Calculate the center of this triplet (average of 3 hex centers)
                var triplet = new[] { hex, neighbor1, neighbor2 };
                var center = GetTripletCenter(triplet, size, origin);

                triplets.Add((triplet, center));
            }
        }

        return triplets;
    }

    /// <summary>
    /// Calculates a point along a circular arc for rotation animation.
    /// </summary>
    public static Point GetPointOnArc(Point center, Point start, double angleRadians)
    {
        double dx = start.X - center.X;
        double dy = start.Y - center.Y;
        double cos = Math.Cos(angleRadians);
        double sin = Math.Sin(angleRadians);
        
        return new Point(
            center.X + dx * cos - dy * sin,
            center.Y + dx * sin + dy * cos);
    }

    /// <summary>
    /// Linear interpolation between two points.
    /// </summary>
    public static Point Lerp(Point a, Point b, double t)
    {
        return new Point(
            a.X + (b.X - a.X) * t,
            a.Y + (b.Y - a.Y) * t);
    }

    /// <summary>
    /// Gets the width of a flat-top hexagon given its size (center to corner distance).
    /// </summary>
    public static double GetHexWidth(double size) => 2 * size;

    /// <summary>
    /// Gets the height of a flat-top hexagon given its size.
    /// </summary>
    public static double GetHexHeight(double size) => Sqrt3 * size;

    /// <summary>
    /// Gets the vertical spacing between hex rows (center to center).
    /// </summary>
    public static double GetVerticalSpacing(double size) => Sqrt3 * size;

    /// <summary>
    /// Gets the horizontal spacing between hex columns (center to center).
    /// </summary>
    public static double GetHorizontalSpacing(double size) => size * 1.5;
}
