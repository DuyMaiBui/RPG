#pragma once

#include "aura/aura_types.h"
#include "aura_world.h"

namespace aura
{

/* Jolt-backed native world. Mirrors the reference world behaviour behind the
   shared IWorld interface so the C ABI and the managed binding are unchanged. */
class JoltWorld : public IWorld
{
public:
    explicit JoltWorld(const AuraWorldDesc& desc);
    ~JoltWorld() override;

    AuraResultCode CreateBody(const AuraBodyDesc& desc, AuraBodyHandle* outBody) override;
    AuraResultCode DestroyBody(AuraBodyHandle body) override;
    AuraResultCode SetKinematicTarget(AuraBodyHandle body, const AuraPose& pose) override;
    AuraResultCode GetBodyState(AuraBodyHandle body, AuraBodyState* outState) const override;
    uint32_t CopyBodyStates(AuraBodyState* buffer, uint32_t capacity) const override;
    uint32_t BodyCount() const override;

    void Step(float deltaTime) override;

    uint32_t PendingEventCount() const override;
    uint32_t CopyEvents(AuraPhysicsEvent* buffer, uint32_t capacity) override;

    bool Raycast(const AuraRay& ray, float maxDistance, const AuraQueryFilter& filter, AuraQueryHit* outHit) override;
    uint32_t RaycastAll(const AuraRay& ray, float maxDistance, const AuraQueryFilter& filter, AuraQueryHit* buffer, uint32_t capacity) override;
    uint32_t OverlapSphere(const AuraVec3& center, float radius, const AuraQueryFilter& filter, AuraQueryHit* buffer, uint32_t capacity) override;
    AuraResultCode OverlapPoint(const AuraVec3& point, const AuraQueryFilter& filter, AuraQueryHit* buffer, uint32_t capacity, uint32_t* outCount) override;
    AuraResultCode OverlapBox(const AuraVec3& center, const AuraVec3& halfExtents, const AuraQuat& rotation, const AuraQueryFilter& filter, AuraQueryHit* buffer, uint32_t capacity, uint32_t* outCount) override;
    AuraResultCode OverlapCapsule(const AuraVec3& pointA, const AuraVec3& pointB, float radius, const AuraQueryFilter& filter, AuraQueryHit* buffer, uint32_t capacity, uint32_t* outCount) override;
    AuraResultCode OverlapShape(const AuraShapeDesc& shape, const AuraPose& pose, const AuraQueryFilter& filter, AuraQueryHit* buffer, uint32_t capacity, uint32_t* outCount) override;
    AuraResultCode SphereCast(const AuraVec3& origin, float radius, const AuraVec3& direction, float maxDistance, const AuraQueryFilter& filter, AuraQueryHit* outHit, bool* outHasHit) override;
    AuraResultCode CapsuleCast(const AuraVec3& pointA, const AuraVec3& pointB, float radius, const AuraVec3& direction, float maxDistance, const AuraQueryFilter& filter, AuraQueryHit* outHit, bool* outHasHit) override;
    AuraResultCode BoxCast(const AuraVec3& center, const AuraVec3& halfExtents, const AuraQuat& rotation, const AuraVec3& direction, float maxDistance, const AuraQueryFilter& filter, AuraQueryHit* outHit, bool* outHasHit) override;
    AuraResultCode ShapeCast(const AuraShapeDesc& shape, const AuraPose& pose, const AuraVec3& direction, float maxDistance, const AuraQueryFilter& filter, AuraQueryHit* outHit, bool* outHasHit) override;

    uint64_t ComputeStateHash() const override;
    AuraResultCode ApplyStates(const AuraBodyState* states, uint32_t count) override;
    uint32_t CopyContacts(AuraContact* buffer, uint32_t capacity) const override;
    AuraResultCode SetSurfaceVelocity(AuraBodyHandle body, const AuraVec3& velocity) override;

    AuraResultCode CreateJoint(const AuraJointDesc& desc, uint64_t* outJoint) override;
    AuraResultCode DestroyJoint(uint64_t joint) override;
    bool HasJoint(uint64_t joint) const override;

    AuraResultCode CreateCharacter(const AuraCharacterDesc& desc, uint64_t* outCharacter) override;
    AuraResultCode DestroyCharacter(uint64_t character) override;
    AuraResultCode GetCharacterState(uint64_t character, AuraCharacterState* outState) const override;
    AuraResultCode MoveCharacter(uint64_t character, const AuraVec3& desiredTranslation, float deltaTime) override;

    uint32_t CharacterCount() const override;
    uint32_t CopyCharacterStates(AuraCharacterState* buffer, uint32_t capacity) const override;
    AuraResultCode ApplyCharacterStates(const AuraCharacterState* states, uint32_t count) override;

private:
    struct Impl;
    static Impl* CreateImpl(const AuraWorldDesc& desc);
    Impl* impl_;
};

} // namespace aura
