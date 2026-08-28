// SPDX-License-Identifier: Apache-2.0
#ifndef PICOGK_BOUNDS3D_H_
#define PICOGK_BOUNDS3D_H_

#include "PicoGKApiTypes.h"

#include <algorithm>
#include <cmath>
#include <stdexcept>

namespace PicoGK
{
/// Axis-aligned bounds with an explicit empty state.
///
/// Default construction creates empty bounds. A zero-size bounds containing one
/// point, including the origin, is non-empty.
class Bounds3d
{
public:
    bool bIsEmpty() const noexcept { return !m_bHasValue; }

    const PKVector3& vecMin() const
    {
        if (!m_bHasValue)
            throw std::logic_error("Empty bounds have no minimum");
        return m_vecMin;
    }

    const PKVector3& vecMax() const
    {
        if (!m_bHasValue)
            throw std::logic_error("Empty bounds have no maximum");
        return m_vecMax;
    }

    void Include(const PKVector3& vecPoint)
    {
        if (!std::isfinite(vecPoint.X) ||
            !std::isfinite(vecPoint.Y) ||
            !std::isfinite(vecPoint.Z))
        {
            throw std::invalid_argument("Bounds point components must be finite");
        }

        if (!m_bHasValue)
        {
            m_vecMin = vecPoint;
            m_vecMax = vecPoint;
            m_bHasValue = true;
            return;
        }

        m_vecMin.X = std::min(m_vecMin.X, vecPoint.X);
        m_vecMin.Y = std::min(m_vecMin.Y, vecPoint.Y);
        m_vecMin.Z = std::min(m_vecMin.Z, vecPoint.Z);
        m_vecMax.X = std::max(m_vecMax.X, vecPoint.X);
        m_vecMax.Y = std::max(m_vecMax.Y, vecPoint.Y);
        m_vecMax.Z = std::max(m_vecMax.Z, vecPoint.Z);
    }

private:
    PKVector3 m_vecMin{};
    PKVector3 m_vecMax{};
    bool      m_bHasValue{false};
};
}

#endif // PICOGK_BOUNDS3D_H_
