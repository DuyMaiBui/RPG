# Android NDK toolchain wrapper for AuraEngine.
#
# Requires ANDROID_NDK_HOME (or ANDROID_NDK_ROOT) pointing at an installed NDK
# with the CMake toolchain file at build/cmake/android.toolchain.cmake.
#
# Usage:
#   cmake -S Native/AuraEngine -B build/android \
#         -DCMAKE_TOOLCHAIN_FILE=Native/AuraEngine/cmake/android.toolchain.cmake \
#         -DANDROID_ABI=arm64-v8a \
#         -DANDROID_PLATFORM=android-24 \
#         -DAURA_USE_JOLT=ON -DAURA_USE_BOX2D=ON -DAURA_BUILD_PLUGIN=ON

set(_aura_ndk "$ENV{ANDROID_NDK_HOME}")
if(NOT _aura_ndk)
    set(_aura_ndk "$ENV{ANDROID_NDK_ROOT}")
endif()
if(NOT _aura_ndk)
    message(FATAL_ERROR "ANDROID_NDK_HOME or ANDROID_NDK_ROOT must be set to build for Android.")
endif()

set(_aura_ndk_toolchain "${_aura_ndk}/build/cmake/android.toolchain.cmake")
if(NOT EXISTS "${_aura_ndk_toolchain}")
    message(FATAL_ERROR "Android NDK toolchain not found at ${_aura_ndk_toolchain}.")
endif()

include("${_aura_ndk_toolchain}")
