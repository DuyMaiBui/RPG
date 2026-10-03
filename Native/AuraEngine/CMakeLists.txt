cmake_minimum_required(VERSION 3.20)
project(AuraEngineNative CXX C)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

option(AURA_BUILD_TESTS "Build the headless AuraEngine smoke test" ON)
option(AURA_BUILD_BENCHMARKS "Build the standalone aura_bench performance harness" OFF)
option(AURA_BUILD_PLUGIN "Build the shared libaura plugin" OFF)
option(AURA_USE_JOLT "Use the Jolt Physics backend (3D)" OFF)
option(AURA_USE_BOX2D "Use the Box2D backend (2D)" OFF)

set(AURA_SOURCES
    src/aura_reference_world.cpp
    src/aura_backend_select.cpp
    src/capi/aura_capi.cpp
    src/capi/aura_capi_events.cpp
    src/capi/aura_capi_queries.cpp
    src/capi/aura_capi_contacts.cpp
    src/capi/aura_capi_joints.cpp
    src/capi/aura_capi_character.cpp
    src/capi/aura_capi_vehicle.cpp
    src/capi/aura_capi_softbody.cpp
    src/capi/aura_capi_ragdoll.cpp
    src/capi/aura_capi_water.cpp
    src/capi/aura_capi_snapshot.cpp
)

if(AURA_USE_JOLT)
    set(JOLT_DIR "${CMAKE_CURRENT_SOURCE_DIR}/third_party/JoltPhysics")
    if(NOT EXISTS "${JOLT_DIR}/Build/CMakeLists.txt")
        message(FATAL_ERROR "AURA_USE_JOLT requires third_party/JoltPhysics (run fetch_jolt.sh).")
    endif()

    set(TARGET_UNIT_TESTS OFF CACHE BOOL "" FORCE)
    set(TARGET_HELLO_WORLD OFF CACHE BOOL "" FORCE)
    set(TARGET_PERFORMANCE_TEST OFF CACHE BOOL "" FORCE)
    set(TARGET_SAMPLES OFF CACHE BOOL "" FORCE)
    set(TARGET_VIEWER OFF CACHE BOOL "" FORCE)
    set(INTERPROCEDURAL_OPTIMIZATION OFF CACHE BOOL "" FORCE)
    set(ENABLE_ALL_WARNINGS OFF CACHE BOOL "" FORCE)
    set(USE_ASSERTS ON CACHE BOOL "" FORCE)
    set(USE_AVX2 OFF CACHE BOOL "" FORCE)
    set(USE_AVX OFF CACHE BOOL "" FORCE)
    set(USE_F16C OFF CACHE BOOL "" FORCE)
    set(USE_FMADD OFF CACHE BOOL "" FORCE)
    set(CPP_EXCEPTIONS_ENABLED ON CACHE BOOL "" FORCE)

    add_subdirectory("${JOLT_DIR}/Build" "${CMAKE_BINARY_DIR}/jolt")
    list(APPEND AURA_SOURCES
        src/physics/jolt/aura_jolt_world.cpp
        src/physics/jolt/aura_jolt_shapes.cpp
        src/physics/jolt/aura_jolt_queries.cpp
        src/physics/jolt/aura_jolt_contacts.cpp
        src/physics/jolt/aura_jolt_joints.cpp
        src/physics/jolt/aura_jolt_character.cpp
        src/physics/jolt/aura_jolt_vehicle.cpp
        src/physics/jolt/aura_jolt_vehicle_registry.cpp
         src/physics/jolt/aura_jolt_softbody.cpp
         src/physics/jolt/aura_jolt_softbody_registry.cpp
         src/physics/jolt/aura_jolt_ragdoll.cpp)
endif()

if(AURA_USE_BOX2D)
    set(BOX2D_DIR "${CMAKE_CURRENT_SOURCE_DIR}/third_party/box2d")
    if(NOT EXISTS "${BOX2D_DIR}/CMakeLists.txt")
        message(FATAL_ERROR "AURA_USE_BOX2D requires third_party/box2d (run fetch_box2d.sh).")
    endif()

    set(BOX2D_SAMPLES OFF CACHE BOOL "" FORCE)
    set(BOX2D_UNIT_TESTS OFF CACHE BOOL "" FORCE)
    set(BOX2D_BENCHMARKS OFF CACHE BOOL "" FORCE)
    set(BOX2D_DOCS OFF CACHE BOOL "" FORCE)
    set(BOX2D_PROFILE OFF CACHE BOOL "" FORCE)
    set(BOX2D_VALIDATE OFF CACHE BOOL "" FORCE)

    add_subdirectory("${BOX2D_DIR}" "${CMAKE_BINARY_DIR}/box2d")
    list(APPEND AURA_SOURCES src/physics/box2d/aura_box2d_world.cpp)
endif()

add_library(aura_native STATIC ${AURA_SOURCES})
target_include_directories(aura_native PUBLIC include PRIVATE src)
target_compile_definitions(aura_native PUBLIC AURA_ENGINE_ABI_VERSION=9)
set_target_properties(aura_native PROPERTIES POSITION_INDEPENDENT_CODE ON)

if(AURA_USE_JOLT)
    target_compile_definitions(aura_native PRIVATE AURA_USE_JOLT=1 JPH_ENABLE_ASSERTS=1)
    target_link_libraries(aura_native PUBLIC Jolt)
endif()

if(AURA_USE_BOX2D)
    target_compile_definitions(aura_native PRIVATE AURA_USE_BOX2D=1)
    target_link_libraries(aura_native PUBLIC box2d)
endif()

if(AURA_BUILD_PLUGIN)
    add_library(aura_shared SHARED ${AURA_SOURCES})
    target_include_directories(aura_shared PUBLIC include PRIVATE src)
    target_compile_definitions(aura_shared PUBLIC AURA_ENGINE_ABI_VERSION=9)
    set_target_properties(aura_shared PROPERTIES OUTPUT_NAME aura)
    if(AURA_USE_JOLT)
        target_compile_definitions(aura_shared PRIVATE AURA_USE_JOLT=1 JPH_ENABLE_ASSERTS=1)
        target_link_libraries(aura_shared PUBLIC Jolt)
    endif()
    if(AURA_USE_BOX2D)
        target_compile_definitions(aura_shared PRIVATE AURA_USE_BOX2D=1)
        target_link_libraries(aura_shared PUBLIC box2d)
    endif()
endif()

if(AURA_BUILD_TESTS)
    enable_testing()
    add_executable(aura_headless src/headless/main.cpp)
    target_link_libraries(aura_headless PRIVATE aura_native)
    add_test(NAME aura_headless COMMAND aura_headless)
endif()

if(AURA_BUILD_BENCHMARKS)
    add_executable(aura_bench src/bench/main.cpp)
    target_link_libraries(aura_bench PRIVATE aura_native)
endif()
