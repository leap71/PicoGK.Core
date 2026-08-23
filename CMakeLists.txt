cmake_minimum_required(VERSION 3.25)

project(PicoGKCore VERSION 3.0.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

set(PICOGK_ARTIFACT_ROOT
    "${CMAKE_CURRENT_SOURCE_DIR}/artifacts"
    CACHE PATH "Root directory for build artifacts")

include(cmake/PicoGKPlatform.cmake)

set(PICOGK_NATIVE_OUTPUT_DIR
    "${PICOGK_ARTIFACT_ROOT}/runtimes/${PICOGK_RID}/native"
    CACHE PATH "Final native runtime directory")

file(MAKE_DIRECTORY "${PICOGK_NATIVE_OUTPUT_DIR}")


# -----------------------------------------------------------------------------
# Dependencies
# -----------------------------------------------------------------------------

if(NOT EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/third_party/oneTBB/CMakeLists.txt")
    message(FATAL_ERROR
        "oneTBB submodule is missing. Run: git submodule update --init --recursive")
endif()

if(NOT EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/third_party/openvdb/openvdb/openvdb/Grid.cc")
    message(FATAL_ERROR
        "OpenVDB submodule is missing. Run: git submodule update --init --recursive")
endif()


# Build only the oneTBB scheduler library required by the OpenVDB subset.
# No installation, examples, tests, allocator proxy, or system package discovery.
set(TBB_TEST OFF CACHE BOOL "" FORCE)
set(TBB_EXAMPLES OFF CACHE BOOL "" FORCE)
set(TBB_INSTALL OFF CACHE BOOL "" FORCE)
set(TBBMALLOC_BUILD OFF CACHE BOOL "" FORCE)
set(TBBMALLOC_PROXY_BUILD OFF CACHE BOOL "" FORCE)

# Do not let the releasing machine's pkg-config environment change the oneTBB
# graph by opportunistically discovering hwloc/tbbbind support.
# PicoGKRuntime only consumes TBB::tbb.
set(TBB_DISABLE_HWLOC_AUTOMATIC_SEARCH ON CACHE BOOL "" FORCE)

add_subdirectory(
    third_party/oneTBB
    ${CMAKE_BINARY_DIR}/third_party/oneTBB
    EXCLUDE_FROM_ALL)

add_subdirectory(native/PicoVDB)

# OpenVDB contains large template-heavy translation units that can exceed
# MSVC's default COFF section limit.
if(MSVC)
    target_compile_options(PicoVDB PRIVATE /bigobj)
endif()

add_subdirectory(native/PicoGKRuntime)

# PicoGKRuntime also contains large translation units that can exceed
# MSVC's default COFF section limit.
if(MSVC)
    target_compile_options(PicoGKRuntime PRIVATE /bigobj)
endif()


# -----------------------------------------------------------------------------
# NuGet packaging
# -----------------------------------------------------------------------------

# Convenience target for a complete local build of the current platform:
# native runtime -> runtimes/<RID>/native -> PicoGK.Core NuGet package.
#
# The native project version is defined above as 3.0.0.
# The NuGet package version is owned by PicoGK.Core.csproj
# (for example 3.0.0-alpha.1).
find_program(PICOGK_DOTNET_EXECUTABLE NAMES dotnet)

set(PICOGK_PACKAGE_OUTPUT_DIR
    "${PICOGK_ARTIFACT_ROOT}/packages"
    CACHE PATH "Output directory for NuGet packages")

if(PICOGK_DOTNET_EXECUTABLE)
    add_custom_target(PicoGKCoreNuGet
        COMMAND ${CMAKE_COMMAND} -E make_directory
                "${PICOGK_PACKAGE_OUTPUT_DIR}"

        COMMAND "${PICOGK_DOTNET_EXECUTABLE}" pack
                "${CMAKE_CURRENT_SOURCE_DIR}/src/PicoGK.Core/PicoGK.Core.csproj"
                -c Release
                -o "${PICOGK_PACKAGE_OUTPUT_DIR}"

        DEPENDS PicoGKRuntime
        WORKING_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}"
        USES_TERMINAL
        COMMENT "Packing PicoGK.Core after native runtime build")
else()
    message(STATUS
        "dotnet not found: PicoGKCoreNuGet convenience target is disabled")
endif()
