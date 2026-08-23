# Explicit OpenVDB 13 source manifest for PicoVDB.
#
# This deliberately does NOT glob the upstream tree. An upstream release may add
# new source files without silently changing the PicoGK Runtime binary.
#
# Phase 1 intentionally mirrors OpenVDB 13's complete compiled core source set.
# This keeps the dependency/build cleanup separate from source-level surgery.
# OpenVDB's upstream openvdb.cc unconditionally initializes/registers point-data
# types, so the point implementation files are required while we use that
# initializer. Once PicoGKRuntime is migrated, PicoVDB can provide a smaller
# initializer and we can remove point support based on PicoGK's real call surface.

set(PICOVDB_SOURCES
    ${PICOVDB_OPENVDB_LIBRARY_ROOT}/Grid.cc

    ${PICOVDB_OPENVDB_LIBRARY_ROOT}/io/Archive.cc
    ${PICOVDB_OPENVDB_LIBRARY_ROOT}/io/Compression.cc
    ${PICOVDB_OPENVDB_LIBRARY_ROOT}/io/DelayedLoadMetadata.cc
    ${PICOVDB_OPENVDB_LIBRARY_ROOT}/io/File.cc
    ${PICOVDB_OPENVDB_LIBRARY_ROOT}/io/GridDescriptor.cc
    ${PICOVDB_OPENVDB_LIBRARY_ROOT}/io/Queue.cc
    ${PICOVDB_OPENVDB_LIBRARY_ROOT}/io/Stream.cc
    ${PICOVDB_OPENVDB_LIBRARY_ROOT}/io/TempFile.cc

    # OpenVDB's built-in Half implementation: no Imath dependency.
    ${PICOVDB_OPENVDB_LIBRARY_ROOT}/math/Half.cc
    ${PICOVDB_OPENVDB_LIBRARY_ROOT}/math/Maps.cc
    ${PICOVDB_OPENVDB_LIBRARY_ROOT}/math/Proximity.cc
    ${PICOVDB_OPENVDB_LIBRARY_ROOT}/math/QuantizedUnitVec.cc
    ${PICOVDB_OPENVDB_LIBRARY_ROOT}/math/Transform.cc

    ${PICOVDB_OPENVDB_LIBRARY_ROOT}/Metadata.cc
    ${PICOVDB_OPENVDB_LIBRARY_ROOT}/MetaMap.cc
    ${PICOVDB_OPENVDB_LIBRARY_ROOT}/openvdb.cc
    ${PICOVDB_OPENVDB_LIBRARY_ROOT}/Platform.cc

    # Required by upstream openvdb.cc. StreamCompression.cc also contains the
    # no-Blosc implementations of openvdb::compression::blosc* and the paged
    # stream implementation used by DelayedLoadMetadata/PointDataLeafNode.
    ${PICOVDB_OPENVDB_LIBRARY_ROOT}/points/AttributeArray.cc
    ${PICOVDB_OPENVDB_LIBRARY_ROOT}/points/AttributeArrayString.cc
    ${PICOVDB_OPENVDB_LIBRARY_ROOT}/points/AttributeGroup.cc
    ${PICOVDB_OPENVDB_LIBRARY_ROOT}/points/AttributeSet.cc
    ${PICOVDB_OPENVDB_LIBRARY_ROOT}/points/StreamCompression.cc
    ${PICOVDB_OPENVDB_LIBRARY_ROOT}/points/points.cc

    ${PICOVDB_OPENVDB_LIBRARY_ROOT}/util/Assert.cc
    ${PICOVDB_OPENVDB_LIBRARY_ROOT}/util/Formats.cc
)
