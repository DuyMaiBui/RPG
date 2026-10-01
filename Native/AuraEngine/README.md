# AuraEngine native

Native simulation kernel and physics backends for AuraEngine. The managed
`AuraEngine.*` assemblies under `Assets/Scripts/AuraEngine` are the source of
truth for the public contract; this directory owns the C ABI and the Jolt/Box2D
adapters.

## Status

- `include/aura/aura_types.h` and `include/aura/aura_abi.h` define the frozen,
  versioned C ABI (`AURA_ENGINE_ABI_VERSION`). No C++ class, STL type or
  exception may cross it.
- `include/aura/aura_physics_backend.h` defines the backend-independent
  `aura::IPhysicsBackend` contract implemented by the Jolt and Box2D adapters.
- `src/aura_reference_world.*` is a small C++ reference world (sphere/box,
  gravity, contacts, events, raycast) behind the ABI. `src/capi` implements the
  C ABI over it and `src/headless/main.cpp` runs a sample simulation using only
  the C ABI.

  ```sh
  ./Native/AuraEngine/build.sh
  # builds out/libaura.(dylib|so) + out/aura_headless and runs the smoke test
  ```

- `src/physics/jolt` is the Jolt Physics adapter implementing the shared
  `aura::IWorld` interface. The C ABI is backend-agnostic, so the same
  `libaura.dylib` exports the same functions whichever world is compiled in.

  ```sh
  ./Native/AuraEngine/fetch_jolt.sh   # one-time sparse clone of Jolt 5.x
  ./Native/AuraEngine/build_jolt.sh   # builds with -DAURA_USE_JOLT=ON,
                                      # runs headless, copies the plugin to
                                      # Assets/Plugins/AuraEngine/macOS
  ```

- `src/physics/box2d` is the Box2D 3.1 adapter for `Plane2D`, implementing the
  same `aura::IWorld`. `CreateWorldImpl` dispatches by mode, so one `libaura`
  serves both: 3D -> Jolt, 2D -> Box2D.

  ```sh
  ./Native/AuraEngine/fetch_box2d.sh  # one-time sparse clone of Box2D 3.1
  ./Native/AuraEngine/build_editor.sh # builds Jolt + Box2D, runs headless
                                      # (3D and 2D) and copies the plugin to
                                      # Assets/Plugins/AuraEngine/macOS
  ```

  ```sh
  cmake -S Native/AuraEngine -B build/native \
        -DAURA_JOLT_DIR=/path/to/JoltPhysics \
        -DAURA_BOX2D_DIR=/path/to/box2d
  cmake --build build/native
  ```

The ABI is consumed by `AuraEngine.Physics.Native`
(`NativePhysicsBackend`), which validates `Aura_AbiVersion` and maps native
results onto `AuraResult`. `AuraSimulationInstance` can select the Jolt backend
via `AuraBackendKind.Jolt` and falls back to the managed engine when the plugin
is unavailable.
