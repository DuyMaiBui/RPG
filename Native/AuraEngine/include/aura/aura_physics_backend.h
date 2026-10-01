#pragma once

#include "aura/aura_types.h"

namespace aura
{

/* Backend-independent physics contract implemented by the Jolt and Box2D
   adapters. It mirrors AuraEngine.Physics in the managed layer. Adding a
   backend must not change this interface or any public ABI struct. */
class IPhysicsBackend
{
public:
    virtual ~IPhysicsBackend() = default;

    virtual const char* Name() const = 0;
    virtual uint32_t Capabilities() const = 0;
    virtual AuraPhysicsMode Mode() const = 0;

    virtual AuraResultCode CreateBody(const AuraBodyDesc& desc, AuraBodyHandle* outBody) = 0;
    virtual AuraResultCode DestroyBody(AuraBodyHandle body) = 0;
    virtual bool HasBody(AuraBodyHandle body) const = 0;

    virtual AuraResultCode SetKinematicTarget(AuraBodyHandle body, const AuraPose& pose) = 0;
    virtual AuraResultCode ApplyImpulse(AuraBodyHandle body, const AuraVec3& impulse) = 0;

    virtual AuraResultCode GetBodyState(AuraBodyHandle body, AuraBodyState* outState) const = 0;
    virtual AuraResultCode CopyBodyStates(AuraBodyState* buffer, uint32_t capacity, uint32_t* outCount) const = 0;

    virtual void Step(float deltaTime) = 0;

    virtual AuraResultCode Raycast(const AuraRay& ray, float maxDistance, const AuraQueryFilter& filter, AuraQueryHit* outHit, bool* outHasHit) = 0;
    virtual AuraResultCode RaycastAll(const AuraRay& ray, float maxDistance, const AuraQueryFilter& filter, AuraQueryHit* buffer, uint32_t capacity, uint32_t* outCount) = 0;
    virtual AuraResultCode OverlapSphere(const AuraVec3& center, float radius, const AuraQueryFilter& filter, AuraQueryHit* buffer, uint32_t capacity, uint32_t* outCount) = 0;

    virtual uint32_t PendingEventCount() const = 0;
    virtual AuraResultCode CopyEvents(AuraPhysicsEvent* buffer, uint32_t capacity, uint32_t* outCount) = 0;
};

class IPhysicsBackendFactory
{
public:
    virtual ~IPhysicsBackendFactory() = default;
    virtual const char* Name() const = 0;
    virtual IPhysicsBackend* Create(const AuraWorldDesc& desc) = 0;
};

} // namespace aura
