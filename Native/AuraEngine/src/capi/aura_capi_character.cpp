#include "aura_capi_internal.h"

extern "C"
{

AuraResultCode Aura_CreateCharacter(AuraWorldHandle world, const AuraCharacterDesc* desc, uint64_t* outCharacter)
{
    auto* instance = aura::ToWorld(world);
    if (instance == nullptr || desc == nullptr || outCharacter == nullptr)
        return AURA_INVALID_WORLD;
    return instance->CreateCharacter(*desc, outCharacter);
}

AuraResultCode Aura_DestroyCharacter(AuraWorldHandle world, uint64_t character)
{
    auto* instance = aura::ToWorld(world);
    if (instance == nullptr)
        return AURA_INVALID_WORLD;
    return instance->DestroyCharacter(character);
}

AuraResultCode Aura_GetCharacterState(AuraWorldHandle world, uint64_t character, AuraCharacterState* outState)
{
    auto* instance = aura::ToWorld(world);
    if (instance == nullptr || outState == nullptr)
        return AURA_INVALID_WORLD;
    return instance->GetCharacterState(character, outState);
}

AuraResultCode Aura_MoveCharacter(AuraWorldHandle world, uint64_t character, AuraVec3 desiredTranslation, float deltaTime)
{
    auto* instance = aura::ToWorld(world);
    if (instance == nullptr)
        return AURA_INVALID_WORLD;
    return instance->MoveCharacter(character, desiredTranslation, deltaTime);
}

} // extern "C"
