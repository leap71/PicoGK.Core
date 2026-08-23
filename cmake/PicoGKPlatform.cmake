# Determine the NuGet Runtime Identifier (RID) used as the native output folder.
# It can always be overridden explicitly with -DPICOGK_RID=<rid>.

if(NOT DEFINED PICOGK_RID OR PICOGK_RID STREQUAL "")
    if(APPLE)
        if(CMAKE_OSX_ARCHITECTURES MATCHES "x86_64")
            set(PICOGK_RID "osx-x64")
        else()
            # Apple Silicon is the default target for the current PicoGK Mac build.
            set(PICOGK_RID "osx-arm64")
        endif()
    elseif(WIN32)
        if(CMAKE_SIZEOF_VOID_P EQUAL 8)
            set(PICOGK_RID "win-x64")
        else()
            message(FATAL_ERROR "Only 64-bit Windows builds are supported by this prototype.")
        endif()
    elseif(UNIX)
        if(CMAKE_SYSTEM_PROCESSOR MATCHES "^(aarch64|arm64)$")
            set(PICOGK_RID "linux-arm64")
        else()
            set(PICOGK_RID "linux-x64")
        endif()
    else()
        message(FATAL_ERROR "Unable to determine PICOGK_RID for this platform.")
    endif()
endif()

message(STATUS "PicoGK native Runtime Identifier: ${PICOGK_RID}")
