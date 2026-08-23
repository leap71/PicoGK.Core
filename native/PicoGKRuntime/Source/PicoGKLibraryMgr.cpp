// SPDX-License-Identifier: Apache-2.0
#include "PicoGKLibraryMgr.h"

#include <openvdb/openvdb.h>

#include <cmath>
#include <stdexcept>

#ifndef PICOGK_LIB_VERSION
#define PICOGK_LIB_VERSION "0.0.0"
#endif

#ifndef PICOGK_BUILD_INFO
#define PICOGK_BUILD_INFO "PicoGK.Core native runtime"
#endif

namespace PicoGK
{
Library::Instance::Instance(float fVoxelSizeMM)
    : m_fVoxelSizeMM(fVoxelSizeMM)
{
    if (!std::isfinite(fVoxelSizeMM) || fVoxelSizeMM <= 0.0f)
        throw std::invalid_argument("Voxel size must be finite and greater than zero");
}

int64_t Library::Instance::nMemUsage() const
{
    return m_oMeshes.nMemUsage() + m_oVoxels.nMemUsage();
}

Library& Library::oLib()
{
    static Library oLibrary;
    return oLibrary;
}

Library::Library()
{
    openvdb::initialize();
}

Library::~Library() = default;

PKINSTANCE Library::hCreateInstance(float fVoxelSizeMM)
{
    return m_oInstances.hAdd(std::make_shared<Instance>(fVoxelSizeMM));
}

bool Library::bDestroyInstance(PKINSTANCE hInstance)
{
    return m_oInstances.bDestroy(hInstance);
}

bool Library::bIsValid(PKINSTANCE hInstance) const
{
    return m_oInstances.bIsValid(hInstance);
}

Library::Instance::Ptr Library::roGetInstance(PKINSTANCE hInstance) const
{
    return m_oInstances.roGet(hInstance);
}

Library::Instance::Ptr Library::roTryGetInstance(PKINSTANCE hInstance) const
{
    return m_oInstances.roTryGet(hInstance);
}

std::string Library::strName() const
{
    return "PicoGK Core Library";
}

std::string Library::strVersion() const
{
    return PICOGK_LIB_VERSION;
}

std::string Library::strBuildInfo() const
{
    return PICOGK_BUILD_INFO;
}
}
