// SPDX-License-Identifier: Apache-2.0
#include "PicoGKMesh.h"

#include <algorithm>
#include <limits>
#include <stdexcept>

namespace PicoGK
{
namespace
{
    template<typename T>
    std::vector<T> aCopy(const T* pData, uint32_t nCount)
    {
        if (nCount == 0)
            return {};
        if (!pData)
            throw std::invalid_argument("Mesh buffer pointer is null for non-empty buffer");
        return std::vector<T>(pData, pData + nCount);
    }

    bool bValidIndex(uint32_t nIndex, size_t nVertexCount)
    {
        return static_cast<size_t>(nIndex) < nVertexCount;
    }
}

Mesh::Mesh(const PKVector3* pVertices,
           uint32_t nVertices,
           const PKTriangle* pTriangles,
           uint32_t nTriangles,
           const PKQuad* pQuads,
           uint32_t nQuads)
    : m_aVertices(aCopy(pVertices, nVertices)),
      m_aTriangles(aCopy(pTriangles, nTriangles)),
      m_aQuads(aCopy(pQuads, nQuads))
{
    Validate();
    BuildBoundingBox();
}

Mesh::Mesh(std::vector<PKVector3>&& aVertices,
           std::vector<PKTriangle>&& aTriangles,
           std::vector<PKQuad>&& aQuads)
    : m_aVertices(std::move(aVertices)),
      m_aTriangles(std::move(aTriangles)),
      m_aQuads(std::move(aQuads))
{
    Validate();
    BuildBoundingBox();
}

void Mesh::Validate() const
{
    const size_t nMaxCount = static_cast<size_t>(std::numeric_limits<uint32_t>::max());
    if (m_aVertices.size() > nMaxCount ||
        m_aTriangles.size() > nMaxCount ||
        m_aQuads.size() > nMaxCount ||
        m_aTriangles.size() + 2 * m_aQuads.size() > nMaxCount)
    {
        throw std::length_error("Mesh exceeds the 32-bit ABI element-count limit");
    }

    const size_t nVertices = m_aVertices.size();

    for (const PKTriangle& tri : m_aTriangles)
    {
        if (!bValidIndex(tri.A, nVertices) ||
            !bValidIndex(tri.B, nVertices) ||
            !bValidIndex(tri.C, nVertices))
        {
            throw std::invalid_argument("Mesh triangle references an invalid vertex index");
        }
    }

    for (const PKQuad& quad : m_aQuads)
    {
        if (!bValidIndex(quad.A, nVertices) ||
            !bValidIndex(quad.B, nVertices) ||
            !bValidIndex(quad.C, nVertices) ||
            !bValidIndex(quad.D, nVertices))
        {
            throw std::invalid_argument("Mesh quad references an invalid vertex index");
        }
    }
}

void Mesh::BuildBoundingBox()
{
    if (m_aVertices.empty())
    {
        m_oBBox = {};
        return;
    }

    m_oBBox.vecMin = m_aVertices.front();
    m_oBBox.vecMax = m_aVertices.front();

    for (const PKVector3& vec : m_aVertices)
    {
        m_oBBox.vecMin.X = std::min(m_oBBox.vecMin.X, vec.X);
        m_oBBox.vecMin.Y = std::min(m_oBBox.vecMin.Y, vec.Y);
        m_oBBox.vecMin.Z = std::min(m_oBBox.vecMin.Z, vec.Z);
        m_oBBox.vecMax.X = std::max(m_oBBox.vecMax.X, vec.X);
        m_oBBox.vecMax.Y = std::max(m_oBBox.vecMax.Y, vec.Y);
        m_oBBox.vecMax.Z = std::max(m_oBBox.vecMax.Z, vec.Z);
    }
}

void Mesh::BuildTriangulatedCache() const
{
    if (m_aQuads.empty())
        return;

    const size_t nTriangulated = m_aTriangles.size() + 2 * m_aQuads.size();
    m_aTriangulatedTriangles.reserve(nTriangulated);
    m_aTriangulatedTriangles.insert(m_aTriangulatedTriangles.end(),
                                     m_aTriangles.begin(),
                                     m_aTriangles.end());

    for (const PKQuad& q : m_aQuads)
    {
        m_aTriangulatedTriangles.push_back({q.A, q.B, q.C});
        m_aTriangulatedTriangles.push_back({q.C, q.D, q.A});
    }

    m_nTriangulatedMemUsage.store(
        static_cast<int64_t>(m_aTriangulatedTriangles.capacity() * sizeof(PKTriangle)),
        std::memory_order_release);
}

const std::vector<PKTriangle>& Mesh::aTriangulatedTriangles() const
{
    if (m_aQuads.empty())
        return m_aTriangles;

    std::call_once(m_onceTriangulated, [this]() { BuildTriangulatedCache(); });
    return m_aTriangulatedTriangles;
}

void Mesh::GetView(PKMeshView* poView) const
{
    if (!poView)
        throw std::invalid_argument("Mesh_GetView output pointer is null");

    poView->pVertices = m_aVertices.empty() ? nullptr : m_aVertices.data();
    poView->nVertices = static_cast<uint32_t>(m_aVertices.size());

    poView->pTriangles = m_aTriangles.empty() ? nullptr : m_aTriangles.data();
    poView->nTriangles = static_cast<uint32_t>(m_aTriangles.size());

    poView->pQuads = m_aQuads.empty() ? nullptr : m_aQuads.data();
    poView->nQuads = static_cast<uint32_t>(m_aQuads.size());
}

void Mesh::GetTriangulatedView(PKTriangulatedMeshView* poView) const
{
    if (!poView)
        throw std::invalid_argument("Mesh_GetTriangulatedView output pointer is null");

    const std::vector<PKTriangle>& aTriangulated = aTriangulatedTriangles();

    poView->pVertices = m_aVertices.empty() ? nullptr : m_aVertices.data();
    poView->nVertices = static_cast<uint32_t>(m_aVertices.size());

    poView->pTriangles = aTriangulated.empty() ? nullptr : aTriangulated.data();
    poView->nTriangles = static_cast<uint32_t>(aTriangulated.size());
}

int64_t Mesh::nMemUsage() const
{
    return static_cast<int64_t>(sizeof(Mesh)) +
           static_cast<int64_t>(m_aVertices.capacity() * sizeof(PKVector3)) +
           static_cast<int64_t>(m_aTriangles.capacity() * sizeof(PKTriangle)) +
           static_cast<int64_t>(m_aQuads.capacity() * sizeof(PKQuad)) +
           m_nTriangulatedMemUsage.load(std::memory_order_acquire);
}
}
