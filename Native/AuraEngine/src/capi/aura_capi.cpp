#include "aura_capi_internal.h"

#include <cstring>
#include <mutex>
#include <vector>

namespace aura
{
WorldSlot g_worldSlots[kMaxWorlds];

namespace
{
/* Serialises world creation and destruction (slot bookkeeping and the backend constructors/destructors); lookups
   (ToWorld) never take it. Box2D keeps its worlds in a process-global array that b2CreateWorld/b2DestroyWorld modify
   without synchronisation, so two threads creating Box2D worlds at once used to get the same slot and crash. */
std::mutex g_worldRegistryMutex;
std::vector<uint32_t> g_freeWorldSlots;
uint32_t g_nextWorldSlot = 0;
} // namespace
} // namespace aura

extern "C"
{

/* World/entity/body lifecycle and body-state transfer. Feature families live
   in their own aura_capi_<feature>.cpp units. */
uint32_t Aura_AbiVersion(void) { return AURA_ENGINE_ABI_VERSION; }

AuraResultCode Aura_CheckAbi(uint32_t callerAbiVersion)
{
    return callerAbiVersion == AURA_ENGINE_ABI_VERSION ? AURA_SUCCESS : AURA_ABI_MISMATCH;
}

/* Diagnostic: worlds created and not yet destroyed, so hosts can assert they do not leak across play sessions. */
static std::atomic<uint32_t> g_liveWorlds{ 0 };

uint32_t Aura_LiveWorldCount(void)
{
    return g_liveWorlds.load();
}

AuraResultCode Aura_CreateWorld(const AuraWorldDesc* desc, AuraWorldHandle* outWorld)
{
    if (desc == nullptr || outWorld == nullptr)
        return AURA_INVALID_DEFINITION;
    outWorld->opaque = 0;
    std::lock_guard<std::mutex> lock(aura::g_worldRegistryMutex);
    auto* created = aura::CreateWorldImpl(*desc);
    if (created == nullptr)
        return AURA_SUCCESS;

    uint32_t slotIndex;
    if (!aura::g_freeWorldSlots.empty())
    {
        slotIndex = aura::g_freeWorldSlots.back();
        aura::g_freeWorldSlots.pop_back();
    }
    else if (aura::g_nextWorldSlot < aura::kMaxWorlds)
    {
        slotIndex = aura::g_nextWorldSlot++;
    }
    else
    {
        delete created;
        return AURA_CAPACITY_EXCEEDED;
    }

    aura::WorldSlot& slot = aura::g_worldSlots[slotIndex];
    slot.world.store(created, std::memory_order_release);
    outWorld->opaque = (static_cast<uint64_t>(slot.generation.load(std::memory_order_relaxed)) << 32) | (slotIndex + 1u);
    g_liveWorlds.fetch_add(1);
    return AURA_SUCCESS;
}

AuraResultCode Aura_DestroyWorld(AuraWorldHandle world)
{
    /* Claim and invalidate the slot under the lock: a stale or concurrent second destroy sees the new generation
       and gets AURA_INVALID_WORLD, so the world is deleted exactly once. The delete stays inside the lock because
       the Box2D backend's global world table is not thread-safe. */
    std::lock_guard<std::mutex> lock(aura::g_worldRegistryMutex);
    aura::IWorld* instance = aura::ToWorld(world);
    if (instance == nullptr)
        return AURA_INVALID_WORLD;
    const uint32_t slotIndex = static_cast<uint32_t>(world.opaque & 0xFFFFFFFFull) - 1u;
    aura::WorldSlot& slot = aura::g_worldSlots[slotIndex];
    slot.world.store(nullptr, std::memory_order_release);
    slot.generation.store(slot.generation.load(std::memory_order_relaxed) + 1u, std::memory_order_release);
    aura::g_freeWorldSlots.push_back(slotIndex);
    g_liveWorlds.fetch_sub(1);
    delete instance;
    return AURA_SUCCESS;
}

AuraResultCode Aura_WorldBodyCount(AuraWorldHandle world, uint32_t* outCount)
{
    auto* instance = aura::ToWorld(world);
    if (instance == nullptr || outCount == nullptr)
        return AURA_INVALID_WORLD;
    *outCount = instance->BodyCount();
    return AURA_SUCCESS;
}

AuraResultCode Aura_CreateEntity(AuraWorldHandle world, AuraEntityHandle* outEntity)
{
    if (aura::ToWorld(world) == nullptr || outEntity == nullptr)
        return AURA_INVALID_WORLD;
    outEntity->index = 0;
    outEntity->generation = 0;
    return AURA_SUCCESS;
}

AuraResultCode Aura_DestroyEntity(AuraWorldHandle world, AuraEntityHandle entity)
{
    (void)entity;
    return aura::ToWorld(world) == nullptr ? AURA_INVALID_WORLD : AURA_SUCCESS;
}

AuraResultCode Aura_IsEntityAlive(AuraWorldHandle world, AuraEntityHandle entity, uint8_t* outAlive)
{
    (void)entity;
    if (aura::ToWorld(world) == nullptr || outAlive == nullptr)
        return AURA_INVALID_WORLD;
    *outAlive = 1;
    return AURA_SUCCESS;
}

AuraResultCode Aura_AttachBody(AuraWorldHandle world, AuraEntityHandle entity, const AuraBodyDesc* desc, AuraBodyHandle* outBody)
{
    (void)entity;
    auto* instance = aura::ToWorld(world);
    if (instance == nullptr || desc == nullptr || outBody == nullptr)
        return AURA_INVALID_WORLD;
    return instance->CreateBody(*desc, outBody);
}

AuraResultCode Aura_DestroyBody(AuraWorldHandle world, AuraBodyHandle body)
{
    auto* instance = aura::ToWorld(world);
    return instance == nullptr ? AURA_INVALID_WORLD : instance->DestroyBody(body);
}

AuraResultCode Aura_SetKinematicTarget(AuraWorldHandle world, AuraBodyHandle body, const AuraPose* pose)
{
    auto* instance = aura::ToWorld(world);
    if (instance == nullptr || pose == nullptr)
        return AURA_INVALID_WORLD;
    return instance->SetKinematicTarget(body, *pose);
}

AuraResultCode Aura_Step(AuraWorldHandle world, AuraTick tick, float deltaTime)
{
    (void)tick;
    auto* instance = aura::ToWorld(world);
    if (instance == nullptr)
        return AURA_INVALID_WORLD;
    instance->Step(deltaTime);
    return AURA_SUCCESS;
}

AuraResultCode Aura_CopyBodyStates(AuraWorldHandle world, AuraBodyState* buffer, uint32_t capacity, uint32_t* outCount)
{
    auto* instance = aura::ToWorld(world);
    if (instance == nullptr || buffer == nullptr || outCount == nullptr)
        return AURA_INVALID_WORLD;
    *outCount = instance->CopyBodyStates(buffer, capacity);
    return AURA_SUCCESS;
}

AuraResultCode Aura_GetBodyState(AuraWorldHandle world, AuraBodyHandle body, AuraBodyState* outState)
{
    auto* instance = aura::ToWorld(world);
    if (instance == nullptr || outState == nullptr)
        return AURA_INVALID_WORLD;
    return instance->GetBodyState(body, outState);
}

} // extern "C"
