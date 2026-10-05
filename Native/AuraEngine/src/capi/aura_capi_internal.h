#pragma once

#include "aura/aura_abi.h"

#include "aura_world.h"

#include <atomic>
#include <cstdint>

namespace aura
{

/* World handle registry.

   A world handle is not the world pointer: it is (generation << 32) | (slot + 1), looked up in a fixed table.
   A destroyed world bumps its slot generation, so a stale handle (use after Aura_DestroyWorld, double destroy),
   a zero handle or a garbage value is rejected with AURA_INVALID_WORLD instead of dereferencing freed memory.

   Cost on the hot path: ToWorld is lock-free, two relaxed/acquire atomic loads and two compares, no mutex.
   Create/destroy (cold) serialise on a mutex in aura_capi.cpp, which also makes concurrent double destroy safe:
   exactly one caller wins the slot.

   Limits: validation covers stale and garbage handles. Calling a world function on one thread while another
   thread destroys that same world remains a caller error (a world is single-threaded by contract); the table
   guarantees only that the destroy itself cannot be raced into a double free. */
constexpr uint32_t kMaxWorlds = 4096;

struct WorldSlot
{
    std::atomic<uint32_t> generation;
    std::atomic<IWorld*> world;
};

extern WorldSlot g_worldSlots[kMaxWorlds];

inline IWorld* ToWorld(AuraWorldHandle handle)
{
    const uint32_t slotPlusOne = static_cast<uint32_t>(handle.opaque & 0xFFFFFFFFull);
    if (slotPlusOne == 0 || slotPlusOne > kMaxWorlds)
        return nullptr;
    const WorldSlot& slot = g_worldSlots[slotPlusOne - 1];
    if (slot.generation.load(std::memory_order_acquire) != static_cast<uint32_t>(handle.opaque >> 32))
        return nullptr;
    return slot.world.load(std::memory_order_acquire);
}

} // namespace aura
