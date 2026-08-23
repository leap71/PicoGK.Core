set(PICOVDB_OPENVDB_PROJECT_ROOT "${PROJECT_SOURCE_DIR}/third_party/openvdb")
set(PICOVDB_OPENVDB_SOURCE_ROOT  "${PICOVDB_OPENVDB_PROJECT_ROOT}/openvdb")
set(PICOVDB_OPENVDB_LIBRARY_ROOT "${PICOVDB_OPENVDB_SOURCE_ROOT}/openvdb")

include(cmake/PicoVDBSources.cmake)

# OpenVDB normally generates version.h in its own CMake graph. We generate only
# the header required by the selected upstream sources, without importing that graph.
set(OpenVDB_MAJOR_VERSION 13)
set(OpenVDB_MINOR_VERSION 0)
set(OpenVDB_PATCH_VERSION 0)
set(OpenVDB_VERSION "13.0.0")
set(OPENVDB_ABI_VERSION_NUMBER 13)
math(EXPR OPENVDB_PACKED_VERSION "(${OpenVDB_MAJOR_VERSION} << 24) | (${OpenVDB_MINOR_VERSION} << 16) | ${OpenVDB_PATCH_VERSION}")
set(OPENVDB_PACKAGE_URL "https://github.com/AcademySoftwareFoundation/openvdb")
set(OPENVDB_PACKAGE_REVISION "v13.0.0")
set(OPENVDB_NAMESPACE_SUFFIX "")

# Keep optional dependency features disabled. Empty values intentionally leave
# the corresponding #cmakedefine entries undefined in generated version.h.
set(OPENVDB_USE_IMATH_HALF "")
set(OPENVDB_USE_BLOSC "")
set(OPENVDB_USE_ZLIB "")
set(OPENVDB_USE_DELAYED_LOADING "")
set(OPENVDB_USE_EXPLICIT_INSTANTIATION "")
set(OPENVDB_ENABLE_ASSERTS "")

# OpenVDB 13's version.h.in injects these macros even when explicit
# instantiation is disabled. Define the exact upstream variables explicitly so
# the generated header is deterministic and does not depend on ambient CMake state.
set(OPENVDB_REAL_TREE_INSTANTIATIONS "")
set(OPENVDB_NUMERIC_TREE_INSTANTIATIONS "")
set(OPENVDB_VEC3_TREE_INSTANTIATIONS "")
set(OPENVDB_VOLUME_TREE_INSTANTIATIONS "")
set(OPENVDB_ALL_TREE_INSTANTIATIONS "")

# Mirror OpenVDB's own generated-header layout exactly. Several OpenVDB
# headers include this file as "version.h", while public consumers commonly
# include it as <openvdb/version.h>.
file(MAKE_DIRECTORY "${CMAKE_CURRENT_BINARY_DIR}/openvdb")
configure_file(
    "${PICOVDB_OPENVDB_LIBRARY_ROOT}/version.h.in"
    "${CMAKE_CURRENT_BINARY_DIR}/openvdb/version.h")

add_library(PicoVDB STATIC ${PICOVDB_SOURCES})
add_library(PicoGK::PicoVDB ALIAS PicoVDB)

set_target_properties(PicoVDB PROPERTIES
    POSITION_INDEPENDENT_CODE ON)

target_compile_features(PicoVDB PUBLIC cxx_std_20)

target_compile_definitions(PicoVDB
    PUBLIC
        OPENVDB_STATICLIB
    PRIVATE
        OPENVDB_PRIVATE)

if(WIN32)
    target_compile_definitions(PicoVDB PRIVATE NOMINMAX WIN32_LEAN_AND_MEAN)
endif()

target_include_directories(PicoVDB
    PUBLIC
        # Match the include layout used by OpenVDB's own static target:
        # source parent + binary root + binary/openvdb.
        "${PICOVDB_OPENVDB_SOURCE_ROOT}"
        "${CMAKE_CURRENT_BINARY_DIR}"
        "${CMAKE_CURRENT_BINARY_DIR}/openvdb"
    PRIVATE
        "${PICOVDB_OPENVDB_LIBRARY_ROOT}")

# A static PicoVDB target carries oneTBB through to the final shared Runtime.
target_link_libraries(PicoVDB PUBLIC TBB::tbb)
