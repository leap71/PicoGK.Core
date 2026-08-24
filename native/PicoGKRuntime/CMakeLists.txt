add_library(PicoGKRuntime SHARED
    Source/PicoGKLibrary.cpp
    Source/PicoGKLibraryMgr.cpp
    Source/PicoGKMesh.cpp
    Source/PicoGKVoxels.cpp)

# Preserve PicoGK's historical native library basename (picogk.dylib / picogk.dll).
set_target_properties(PicoGKRuntime PROPERTIES
    OUTPUT_NAME "picogk"
    PREFIX ""
    CXX_VISIBILITY_PRESET hidden
    VISIBILITY_INLINES_HIDDEN YES
    # Import/static archives are build products, not NuGet runtime payloads.
    ARCHIVE_OUTPUT_DIRECTORY "${CMAKE_CURRENT_BINARY_DIR}/archive"
    LIBRARY_OUTPUT_DIRECTORY "${PICOGK_NATIVE_OUTPUT_DIR}"
    RUNTIME_OUTPUT_DIRECTORY "${PICOGK_NATIVE_OUTPUT_DIR}")

# Multi-config generators (Visual Studio/Xcode) otherwise append a configuration
# directory. Force every configuration to the NuGet native handoff directory.
foreach(config DEBUG RELEASE RELWITHDEBINFO MINSIZEREL)
    string(TOUPPER "${config}" config_upper)
    set_target_properties(PicoGKRuntime PROPERTIES
        ARCHIVE_OUTPUT_DIRECTORY_${config_upper} "${CMAKE_CURRENT_BINARY_DIR}/archive/${config}"
        LIBRARY_OUTPUT_DIRECTORY_${config_upper} "${PICOGK_NATIVE_OUTPUT_DIR}"
        RUNTIME_OUTPUT_DIRECTORY_${config_upper} "${PICOGK_NATIVE_OUTPUT_DIR}")
endforeach()

target_compile_features(PicoGKRuntime PRIVATE cxx_std_20)

target_include_directories(PicoGKRuntime
    PRIVATE
        "${CMAKE_CURRENT_SOURCE_DIR}/API"
        "${CMAKE_CURRENT_SOURCE_DIR}/Source")

target_compile_definitions(PicoGKRuntime
    PRIVATE
        PICOGK_BUILD_LIBRARY
        PICOGK_LIB_VERSION="${PROJECT_VERSION}"
        PICOGK_BUILD_INFO="PicoGK.Core native runtime ${PROJECT_VERSION}")

target_link_libraries(PicoGKRuntime PRIVATE PicoGK::PicoVDB)

if(APPLE)
    # The Runtime and TBB are packaged side-by-side. Build the final relocatable
    # relationship directly rather than repairing Mach-O paths after copying.
    set_target_properties(PicoGKRuntime PROPERTIES
        BUILD_RPATH "@loader_path"
        INSTALL_RPATH "@loader_path")
elseif(UNIX)
    # Match the macOS side-by-side packaging model on ELF platforms. The runtime
    # and its TBB dependency are loaded from the same NuGet native directory.
    set_target_properties(PicoGKRuntime PROPERTIES
        BUILD_RPATH "$ORIGIN"
        INSTALL_RPATH "$ORIGIN")
elseif(WIN32)
    target_compile_definitions(PicoGKRuntime PRIVATE NOMINMAX WIN32_LEAN_AND_MEAN)
endif()

# This project packages the build-tree output directly. Do not leave CMake's
# automatic build-tree library search paths in the shipped runtime; use only
# the relocatable install rpath selected above.
set_target_properties(PicoGKRuntime PROPERTIES
    BUILD_WITH_INSTALL_RPATH TRUE
    INSTALL_RPATH_USE_LINK_PATH FALSE)

# oneTBB is deliberately dynamic. Package the loader-facing library name next
# to PicoGKRuntime, not oneTBB's fully versioned build filename.
#
# On macOS/Linux, for example:
#   physical build file:  libtbb.12.19.dylib / libtbb.so.12.19
#   loader reference:     @rpath/libtbb.12.dylib / libtbb.so.12
#   packaged filename:    libtbb.12.dylib / libtbb.so.12
#
# Copy the actual binary rather than relying on a symlink surviving NuGet
# packaging/extraction. Windows has no SONAME concept, so use the target name.
if(UNIX)
    add_custom_command(TARGET PicoGKRuntime POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E rm -f
                "${PICOGK_NATIVE_OUTPUT_DIR}/$<TARGET_FILE_NAME:TBB::tbb>"
        COMMAND ${CMAKE_COMMAND} -E copy_if_different
                "$<TARGET_FILE:TBB::tbb>"
                "${PICOGK_NATIVE_OUTPUT_DIR}/$<TARGET_SONAME_FILE_NAME:TBB::tbb>"
        COMMENT "Packaging oneTBB as $<TARGET_SONAME_FILE_NAME:TBB::tbb>")
else()
    add_custom_command(TARGET PicoGKRuntime POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_if_different
                "$<TARGET_FILE:TBB::tbb>"
                "${PICOGK_NATIVE_OUTPUT_DIR}/$<TARGET_FILE_NAME:TBB::tbb>"
        COMMENT "Copying oneTBB next to PicoGKRuntime")
endif()
