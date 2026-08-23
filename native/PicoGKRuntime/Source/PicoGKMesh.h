// SPDX-License-Identifier: Apache-2.0
#ifndef PICOGK_MESH_H_
#define PICOGK_MESH_H_

#include "PicoGKApiTypes.h"

#include <atomic>
#include <cstdint>
#include <memory>
#include <mutex>
#include <vector>

namespace PicoGK
{
/// Immutable native polygon mesh.
///
/// Mesh preserves triangles and quads as produced by OpenVDB. A complete
/// triangulated view is generated lazily only when requested. Because Mesh is
/// immutable, pointers returned through PKMeshView remain stable for its
/// lifetime.
class Mesh
{
public:
    using Ptr = std::shared_ptr<Mesh>;

    /// Creates a Mesh by copying caller-owned vertex and polygon buffers.
    Mesh(const PKVector3* pVertices,
         uint32_t nVertices,
         const PKTriangle* pTriangles,
         uint32_t nTriangles,
         const PKQuad* pQuads,
         uint32_t nQuads);

    /// Creates a Mesh by taking ownership of completed native buffers.
    Mesh(std::vector<PKVector3>&& aVertices,
         std::vector<PKTriangle>&& aTriangles,
         std::vector<PKQuad>&& aQuads);

    Mesh(const Mesh&) = delete;
    Mesh& operator=(const Mesh&) = delete;

    /// Returns estimated native memory owned by this Mesh [bytes].
    int64_t nMemUsage() const;

    /// Returns the immutable world-space bounding box [mm].
    const PKBBox3& oBBox() const { return m_oBBox; }

    const std::vector<PKVector3>& aVertices() const { return m_aVertices; }
    const std::vector<PKTriangle>& aTriangles() const { return m_aTriangles; }
    const std::vector<PKQuad>& aQuads() const { return m_aQuads; }

    /// Returns all polygons as triangles, building the cache on first use.
    const std::vector<PKTriangle>& aTriangulatedTriangles() const;

    /// Fills a read-only C ABI view into the original polygon storage.
    void GetView(PKMeshView* poView) const;

    /// Fills a read-only view of the complete surface as triangles.
    /// Builds the triangulation cache lazily if quads are present.
    void GetTriangulatedView(PKTriangulatedMeshView* poView) const;

private:
    void Validate() const;
    void BuildBoundingBox();
    void BuildTriangulatedCache() const;

    PKBBox3 m_oBBox{};
    std::vector<PKVector3> m_aVertices;
    std::vector<PKTriangle> m_aTriangles;
    std::vector<PKQuad> m_aQuads;

    mutable std::once_flag m_onceTriangulated;
    mutable std::vector<PKTriangle> m_aTriangulatedTriangles;
    mutable std::atomic<int64_t> m_nTriangulatedMemUsage{0};
};
}

#endif // PICOGK_MESH_H_
