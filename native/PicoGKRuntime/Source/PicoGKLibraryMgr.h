// SPDX-License-Identifier: Apache-2.0
#ifndef PICOGK_LIBRARY_MGR_H_
#define PICOGK_LIBRARY_MGR_H_

#include "PicoGKApiTypes.h"
#include "PicoGKHandleManager.h"
#include "PicoGKMesh.h"
#include "PicoGKVoxels.h"

#include <cstdint>
#include <memory>
#include <string>

namespace PicoGK
{
/// Process-wide native PicoGK runtime manager.
///
/// Library owns Library::Instance objects. Each Instance is a deterministic
/// lifetime and memory-accounting domain for its Mesh and Voxels objects.
class Library
{
public:
    /// Per-context geometry owner with a fixed voxel size.
    class Instance
    {
    public:
        using Ptr = std::shared_ptr<Instance>;

        explicit Instance(float fVoxelSizeMM);

        /// Returns the voxel edge length shared by Voxels created in this instance [mm].
        float fVoxelSizeMM() const { return m_fVoxelSizeMM; }

        /// Returns estimated native Mesh + Voxels memory [bytes].
        int64_t nMemUsage() const;

        PicoGKHandleManager<Mesh, PKMESH>      m_oMeshes{"Meshes"};
        PicoGKHandleManager<Voxels, PKVOXELS>  m_oVoxels{"Voxels"};

    private:
        float m_fVoxelSizeMM = 0.0f;
    };

    /// Returns the process-wide runtime manager.
    static Library& oLib();

    PKINSTANCE hCreateInstance(float fVoxelSizeMM);
    bool bDestroyInstance(PKINSTANCE hInstance);
    bool bIsValid(PKINSTANCE hInstance) const;

    Instance::Ptr roGetInstance(PKINSTANCE hInstance) const;
    Instance::Ptr roTryGetInstance(PKINSTANCE hInstance) const;

    std::string strName() const;
    std::string strVersion() const;
    std::string strBuildInfo() const;

    Library(const Library&) = delete;
    Library& operator=(const Library&) = delete;

private:
    Library();
    ~Library();

    PicoGKHandleManager<Instance, PKINSTANCE> m_oInstances{"LibraryInstance"};
};
}

#endif // PICOGK_LIBRARY_MGR_H_
