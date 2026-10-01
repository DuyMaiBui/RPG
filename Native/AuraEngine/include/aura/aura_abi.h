#pragma once

#include "aura/aura_types.h"

#ifdef __cplusplus
extern "C" {
#endif

/* The C ABI is the only public native boundary. No C++ classes, STL types or
   exceptions may cross it. Every entry returns an AuraResultCode (except pure
   version/query-count helpers) and validates handles and ABI compatibility. */

uint32_t Aura_AbiVersion(void);

/* Returns AURA_ABI_MISMATCH when the caller's expected version differs. */
AuraResultCode Aura_CheckAbi(uint32_t callerAbiVersion);

AuraResultCode Aura_CreateWorld(const AuraWorldDesc* desc, AuraWorldHandle* outWorld);
AuraResultCode Aura_DestroyWorld(AuraWorldHandle world);
AuraResultCode Aura_WorldBodyCount(AuraWorldHandle world, uint32_t* outCount);

AuraResultCode Aura_CreateEntity(AuraWorldHandle world, AuraEntityHandle* outEntity);
AuraResultCode Aura_DestroyEntity(AuraWorldHandle world, AuraEntityHandle entity);
AuraResultCode Aura_IsEntityAlive(AuraWorldHandle world, AuraEntityHandle entity, uint8_t* outAlive);

AuraResultCode Aura_AttachBody(AuraWorldHandle world, AuraEntityHandle entity, const AuraBodyDesc* desc, AuraBodyHandle* outBody);
AuraResultCode Aura_DestroyBody(AuraWorldHandle world, AuraBodyHandle body);
AuraResultCode Aura_SetKinematicTarget(AuraWorldHandle world, AuraBodyHandle body, const AuraPose* pose);

AuraResultCode Aura_Step(AuraWorldHandle world, AuraTick tick, float deltaTime);

AuraResultCode Aura_CopyBodyStates(AuraWorldHandle world, AuraBodyState* buffer, uint32_t capacity, uint32_t* outCount);
AuraResultCode Aura_GetBodyState(AuraWorldHandle world, AuraBodyHandle body, AuraBodyState* outState);

AuraResultCode Aura_PendingEventCount(AuraWorldHandle world, uint32_t* outCount);
AuraResultCode Aura_CopyEvents(AuraWorldHandle world, AuraPhysicsEvent* buffer, uint32_t capacity, uint32_t* outCount);

AuraResultCode Aura_Raycast(AuraWorldHandle world, const AuraRay* ray, float maxDistance, const AuraQueryFilter* filter, AuraQueryHit* outHit, uint8_t* outHasHit);
AuraResultCode Aura_RaycastAll(AuraWorldHandle world, const AuraRay* ray, float maxDistance, const AuraQueryFilter* filter, AuraQueryHit* buffer, uint32_t capacity, uint32_t* outCount);
AuraResultCode Aura_OverlapSphere(AuraWorldHandle world, AuraVec3 center, float radius, const AuraQueryFilter* filter, AuraQueryHit* buffer, uint32_t capacity, uint32_t* outCount);

AuraResultCode Aura_CreateJoint(AuraWorldHandle world, const AuraJointDesc* desc, uint64_t* outJoint);
AuraResultCode Aura_DestroyJoint(AuraWorldHandle world, uint64_t joint);
AuraResultCode Aura_HasJoint(AuraWorldHandle world, uint64_t joint, uint8_t* outHas);

AuraResultCode Aura_CopyContacts(AuraWorldHandle world, AuraContact* buffer, uint32_t capacity, uint32_t* outCount);

AuraResultCode Aura_SetSurfaceVelocity(AuraWorldHandle world, AuraBodyHandle body, AuraVec3 velocity);

AuraResultCode Aura_CreateCharacter(AuraWorldHandle world, const AuraCharacterDesc* desc, uint64_t* outCharacter);
AuraResultCode Aura_DestroyCharacter(AuraWorldHandle world, uint64_t character);
AuraResultCode Aura_GetCharacterState(AuraWorldHandle world, uint64_t character, AuraCharacterState* outState);
AuraResultCode Aura_MoveCharacter(AuraWorldHandle world, uint64_t character, AuraVec3 desiredTranslation, float deltaTime);

AuraResultCode Aura_ComputeStateHash(AuraWorldHandle world, uint64_t* outHash);

AuraResultCode Aura_SerializeState(AuraWorldHandle world, uint8_t* buffer, uint32_t capacity, uint32_t* outSize);
AuraResultCode Aura_DeserializeState(AuraWorldHandle world, const uint8_t* buffer, uint32_t size);

#ifdef __cplusplus
}
#endif
