#include <atomic>
#include "aura_capi_internal.h"

#include <cstring>

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
    auto* created = aura::CreateWorldImpl(*desc);
    outWorld->opaque = reinterpret_cast<uint64_t>(created);
    if (created != nullptr)
        g_liveWorlds.fetch_add(1);
    return AURA_SUCCESS;
}

AuraResultCode Aura_DestroyWorld(AuraWorldHandle world)
{
    auto* instance = aura::ToWorld(world);
    if (instance == nullptr)
        return AURA_INVALID_WORLD;
    delete instance;
    g_liveWorlds.fetch_sub(1);
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
