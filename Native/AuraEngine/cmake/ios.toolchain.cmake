# iOS toolchain wrapper for AuraEngine.
#
# Uses the CMake iOS cross-compiling support with the Xcode command line tools.
# Requires an active Xcode (xcode-select -p must point at Xcode, not the
# standalone Command Line Tools) so the iphoneos SDK is available.
#
# Usage:
#   cmake -S Native/AuraEngine -B build/ios \
#         -G Xcode \
#         -DCMAKE_TOOLCHAIN_FILE=Native/AuraEngine/cmake/ios.toolchain.cmake \
#         -DIOS_ARCH=arm64 \
#         -DAURA_USE_JOLT=ON -DAURA_USE_BOX2D=ON -DAURA_BUILD_PLUGIN=ON

set(CMAKE_SYSTEM_NAME iOS)

if(NOT DEFINED IOS_ARCH)
    set(IOS_ARCH arm64)
endif()

set(CMAKE_OSX_ARCHITECTURES "${IOS_ARCH}" CACHE STRING "" FORCE)
set(CMAKE_OSX_DEPLOYMENT_TARGET "13.0" CACHE STRING "" FORCE)
set(CMAKE_OSX_SYSROOT iphoneos CACHE STRING "" FORCE)
