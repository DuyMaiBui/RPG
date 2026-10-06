#pragma once

#include "aura/aura_types.h"
#include "aura_world.h"

namespace aura
{

/* Box2D 3.x backend for Plane2D. Implements the shared IWorld interface so the
   C ABI and the managed binding are unchanged. */
class Box2DWorld : public IWorld
{
public:
    explicit Box2DWorld(const AuraWorldDesc& desc);
    ~Box2DWorld() override;

    AuraResultCode CreateBody(const AuraBodyDesc& desc, AuraBodyHandle* outBody) override;
    AuraResultCode DestroyBody(AuraBodyHandle body) override;
    AuraResultCode SetKinematicTarget(AuraBodyHandle body, const AuraPose& pose) override;
    AuraResultCode GetBodyState(AuraBodyHandle body, AuraBodyState* outState) const override;
    uint32_t CopyBodyStates(AuraBodyState* buffer, uint32_t capacity) const override;
    uint32_t BodyCount() const override;

    AuraResultCode Step(float deltaTime) override;

    uint32_t PendingEventCount() const override;
    uint32_t CopyEvents(AuraPhysicsEvent* buffer, uint32_t capacity) override;

    bool Raycast(const AuraRay& ray, float maxDistance, const AuraQueryFilter& filter, AuraQueryHit* outHit) override;
    uint32_t RaycastAll(const AuraRay& ray, float maxDistance, const AuraQueryFilter& filter, AuraQueryHit* buffer, uint32_t capacity) override;
    uint32_t OverlapSphere(const AuraVec3& center, float radius, const AuraQueryFilter& filter, AuraQueryHit* buffer, uint32_t capacity) override;
    AuraResultCode OverlapPoint(const AuraVec3&, const AuraQueryFilter&, AuraQueryHit*, uint32_t, uint32_t*) override;
    AuraResultCode OverlapBox(const AuraVec3&, const AuraVec3&, const AuraQuat&, const AuraQueryFilter&, AuraQueryHit*, uint32_t, uint32_t*) override;
    AuraResultCode OverlapCapsule(const AuraVec3&, const AuraVec3&, float, const AuraQueryFilter&, AuraQueryHit*, uint32_t, uint32_t*) override;
    AuraResultCode OverlapShape(const AuraShapeDesc&, const AuraPose&, const AuraQueryFilter&, AuraQueryHit*, uint32_t, uint32_t*) override;
    AuraResultCode SphereCast(const AuraVec3&, float, const AuraVec3&, float, const AuraQueryFilter&, AuraQueryHit*, bool*) override;
    AuraResultCode CapsuleCast(const AuraVec3&, const AuraVec3&, float, const AuraVec3&, float, const AuraQueryFilter&, AuraQueryHit*, bool*) override;
    AuraResultCode BoxCast(const AuraVec3&, const AuraVec3&, const AuraQuat&, const AuraVec3&, float, const AuraQueryFilter&, AuraQueryHit*, bool*) override;
    AuraResultCode ShapeCast(const AuraShapeDesc&, const AuraPose&, const AuraVec3&, float, const AuraQueryFilter&, AuraQueryHit*, bool*) override;

    uint64_t ComputeStateHash() const override;
    AuraResultCode ApplyStates(const AuraBodyState* states, uint32_t count) override;
    uint32_t CopyBodyExtras(BodyExtra* buffer, uint32_t capacity) const override;
    AuraResultCode ApplyStatesWithExtras(const AuraBodyState* states, const BodyExtra* extras, uint32_t count) override;
    uint32_t CopyContacts(AuraContact* buffer, uint32_t capacity) const override;
    AuraResultCode SetSurfaceVelocity(AuraBodyHandle body, const AuraVec3& velocity) override;
    AuraResultCode CreateWater(const AuraWaterDesc& desc, AuraWaterHandle* outWater) override;
    AuraResultCode DestroyWater(AuraWaterHandle water) override;
    AuraResultCode SetWaterParameters(AuraWaterHandle water, const AuraWaterDesc& desc) override;
    AuraResultCode ApplyWaterStep(AuraWaterHandle water, float deltaTime) override;

    AuraResultCode CreateJoint(const AuraJointDesc& desc, uint64_t* outJoint) override;
    AuraResultCode DestroyJoint(uint64_t joint) override;
    bool HasJoint(uint64_t joint) const override;

    AuraResultCode SetLinearVelocity(AuraBodyHandle body, const AuraVec3& velocity) override;
    AuraResultCode SetAngularVelocity(AuraBodyHandle body, const AuraVec3& velocity) override;
    AuraResultCode AddForce(AuraBodyHandle body, const AuraVec3& force) override;
    AuraResultCode AddImpulse(AuraBodyHandle body, const AuraVec3& impulse) override;
    AuraResultCode AddTorque(AuraBodyHandle body, const AuraVec3& torque) override;
    AuraResultCode AddAngularImpulse(AuraBodyHandle body, const AuraVec3& impulse) override;
    AuraResultCode SetBodyPose(AuraBodyHandle body, const AuraPose& pose, bool zeroVelocity) override;
    AuraResultCode SetGravityScale(AuraBodyHandle body, float gravityScale) override;
    AuraResultCode SetFriction(AuraBodyHandle body, float friction) override;
    AuraResultCode SetRestitution(AuraBodyHandle body, float restitution) override;
    AuraResultCode SetMotionType(AuraBodyHandle body, AuraBodyType type) override;
    AuraResultCode SetBodyLayer(AuraBodyHandle body, AuraLayer layer, uint64_t collisionMask) override;
    AuraResultCode SetBodyEnabled(AuraBodyHandle body, bool enabled) override;
    AuraResultCode IsBodyEnabled(AuraBodyHandle body, bool* outEnabled) const override;
    AuraResultCode SetJointMotor(uint64_t joint, const AuraJointMotorDesc& motor) override;
    AuraResultCode SetJointLimits(uint64_t joint, bool enabled, float minLimit, float maxLimit) override;
    AuraResultCode SetJointBreakThreshold(uint64_t joint, float maxForce, float maxTorque) override;
    AuraResultCode IsJointBroken(uint64_t joint, bool* outBroken) const override;
    AuraResultCode GetJointFeedback(uint64_t joint, AuraJointFeedback* outFeedback) const override;
    AuraResultCode SetJointTarget(uint64_t joint, const AuraVec3& target) override;

    AuraResultCode SetWorldGravity(const AuraVec3& gravity) override;
    AuraResultCode GetWorldGravity(AuraVec3* outGravity) const override;
    AuraResultCode SetBodyCollisionDetection(AuraBodyHandle body, int32_t collisionDetection) override;
    ForceFieldRegistry* ForceFields() override;

    AuraResultCode CreateCharacter(const AuraCharacterDesc& desc, uint64_t* outCharacter) override;
    AuraResultCode DestroyCharacter(uint64_t character) override;
    AuraResultCode GetCharacterState(uint64_t character, AuraCharacterState* outState) const override;
    AuraResultCode MoveCharacter(uint64_t character, const AuraVec3& desiredTranslation, float deltaTime) override;

    uint32_t CharacterCount() const override;
    uint32_t CopyCharacterStates(AuraCharacterState* buffer, uint32_t capacity) const override;
    AuraResultCode ApplyCharacterStates(const AuraCharacterState* states, uint32_t count) override;
    AuraResultCode CreateSoftBody(const AuraSoftBodyDesc&, AuraSoftBodyHandle*) override;
    AuraResultCode DestroySoftBody(AuraSoftBodyHandle) override;
    AuraResultCode GetSoftBodyState(AuraSoftBodyHandle, float*, uint32_t, AuraSoftBodyState*) const override;

private:
    struct Impl;
    static Impl* CreateImpl(const AuraWorldDesc& desc);
    Impl* impl_;
};

} // namespace aura
