// SPDX-License-Identifier: Apache-2.0
using System.Numerics;

namespace PicoGK;

/// <summary>
/// Mutable axis-aligned three-dimensional bounding box in millimetres.
/// </summary>
public sealed class BBox3
{
    public Vector3 vecMin;
    public Vector3 vecMax;

    /// <summary>Creates an empty bounding box.</summary>
    public BBox3()
    {
        Clear();
    }

    /// <summary>Creates a bounding box containing one point.</summary>
    public BBox3(Vector3 vecPoint)
    {
        ValidatePoint(vecPoint);
        vecMin = vecPoint;
        vecMax = vecPoint;
    }

    /// <summary>Creates a bounding box from minimum and maximum corners.</summary>
    public BBox3(Vector3 vecMin, Vector3 vecMax)
    {
        ValidatePoint(vecMin);
        ValidatePoint(vecMax);

        this.vecMin = Vector3.Min(vecMin, vecMax);
        this.vecMax = Vector3.Max(vecMin, vecMax);
    }

    /// <summary>Creates a copy of another bounding box.</summary>
    public BBox3(BBox3 oOther)
    {
        ArgumentNullException.ThrowIfNull(oOther);
        vecMin = oOther.vecMin;
        vecMax = oOther.vecMax;
    }

    /// <summary>Returns true if the box does not contain any point.</summary>
    public bool bIsEmpty()
    {
        return vecMin.X > vecMax.X ||
               vecMin.Y > vecMax.Y ||
               vecMin.Z > vecMax.Z;
    }

    /// <summary>Resets this bounding box to the empty state.</summary>
    public void Clear()
    {
        vecMin = new Vector3(float.PositiveInfinity);
        vecMax = new Vector3(float.NegativeInfinity);
    }

    /// <summary>Expands this box to include a point.</summary>
    public void Include(Vector3 vecPoint)
    {
        ValidatePoint(vecPoint);

        if (bIsEmpty())
        {
            vecMin = vecPoint;
            vecMax = vecPoint;
            return;
        }

        vecMin = Vector3.Min(vecMin, vecPoint);
        vecMax = Vector3.Max(vecMax, vecPoint);
    }

    /// <summary>Expands this box to include another box. Empty boxes are ignored.</summary>
    public void Include(BBox3 oOther)
    {
        ArgumentNullException.ThrowIfNull(oOther);

        if (oOther.bIsEmpty())
            return;

        Include(oOther.vecMin);
        Include(oOther.vecMax);
    }

    /// <summary>Returns true if this box contains the supplied point, including its boundary.</summary>
    public bool bContains(Vector3 vecPoint)
    {
        ValidatePoint(vecPoint);

        return !bIsEmpty() &&
               vecPoint.X >= vecMin.X && vecPoint.X <= vecMax.X &&
               vecPoint.Y >= vecMin.Y && vecPoint.Y <= vecMax.Y &&
               vecPoint.Z >= vecMin.Z && vecPoint.Z <= vecMax.Z;
    }

    /// <summary>Returns true if this box completely contains another box.</summary>
    public bool bContains(BBox3 oOther)
    {
        ArgumentNullException.ThrowIfNull(oOther);

        if (oOther.bIsEmpty())
            return true;

        return !bIsEmpty() &&
               bContains(oOther.vecMin) &&
               bContains(oOther.vecMax);
    }

    /// <summary>Returns true if this box overlaps or touches another box.</summary>
    public bool bIntersects(BBox3 oOther)
    {
        ArgumentNullException.ThrowIfNull(oOther);

        if (bIsEmpty() || oOther.bIsEmpty())
            return false;

        return vecMin.X <= oOther.vecMax.X && vecMax.X >= oOther.vecMin.X &&
               vecMin.Y <= oOther.vecMax.Y && vecMax.Y >= oOther.vecMin.Y &&
               vecMin.Z <= oOther.vecMax.Z && vecMax.Z >= oOther.vecMin.Z;
    }

    /// <summary>Returns the intersection of this box and another box.</summary>
    public BBox3 oIntersection(BBox3 oOther)
    {
        ArgumentNullException.ThrowIfNull(oOther);

        if (!bIntersects(oOther))
            return new BBox3();

        return new BBox3(
            Vector3.Max(vecMin, oOther.vecMin),
            Vector3.Min(vecMax, oOther.vecMax));
    }

    /// <summary>Box size along X, Y and Z. Empty boxes return Vector3.Zero.</summary>
    public Vector3 vecSize => bIsEmpty() ? Vector3.Zero : vecMax - vecMin;

    /// <summary>Center of the box. Empty boxes return Vector3.Zero.</summary>
    public Vector3 vecCenter => bIsEmpty() ? Vector3.Zero : 0.5f * (vecMin + vecMax);

    public float fWidth  => vecSize.X;
    public float fHeight => vecSize.Y;
    public float fDepth  => vecSize.Z;

    /// <summary>Expands this box uniformly in all directions by the supplied distance.</summary>
    public void Grow(float fDistanceMM)
    {
        if (!float.IsFinite(fDistanceMM))
            throw new ArgumentOutOfRangeException(nameof(fDistanceMM));

        if (bIsEmpty())
            return;

        Vector3 vecGrow = new(fDistanceMM);
        vecMin -= vecGrow;
        vecMax += vecGrow;

        if (bIsEmpty())
            Clear();
    }

    /// <summary>Returns a grown copy of this box.</summary>
    public BBox3 oGrown(float fDistanceMM)
    {
        BBox3 oResult = new(this);
        oResult.Grow(fDistanceMM);
        return oResult;
    }

    /// <summary>Clamps a point to the closest point inside this box.</summary>
    public Vector3 vecClamp(Vector3 vecPoint)
    {
        ValidatePoint(vecPoint);
        ThrowIfEmpty();
        return Vector3.Clamp(vecPoint, vecMin, vecMax);
    }

    /// <summary>Returns a uniformly distributed random point inside this box.</summary>
    public Vector3 vecRandomVectorInside(Random oRandom)
    {
        ArgumentNullException.ThrowIfNull(oRandom);
        ThrowIfEmpty();

        Vector3 vecRange = vecMax - vecMin;

        return vecMin + new Vector3(
            oRandom.NextSingle() * vecRange.X,
            oRandom.NextSingle() * vecRange.Y,
            oRandom.NextSingle() * vecRange.Z);
    }

    /// <summary>Alias for vecRandomVectorInside.</summary>
    public Vector3 vecRandomPoint(Random oRandom) => vecRandomVectorInside(oRandom);

    private void ThrowIfEmpty()
    {
        if (bIsEmpty())
            throw new InvalidOperationException("Bounding box is empty.");
    }

    private static void ValidatePoint(Vector3 vecPoint)
    {
        if (!float.IsFinite(vecPoint.X) ||
            !float.IsFinite(vecPoint.Y) ||
            !float.IsFinite(vecPoint.Z))
        {
            throw new ArgumentOutOfRangeException(nameof(vecPoint),
                "Bounding box coordinates must be finite.");
        }
    }
}
