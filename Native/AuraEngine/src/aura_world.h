#pragma once

#include "aura/aura_types.h"
#include "aura_body_extra.h"

#include <cstdint>

namespace aura
{

class ForceFieldRegistry;

/* Internal native world contract shared by the reference world and the Jolt
   backend. The C ABI in aura_capi.cpp only talks to this interface, so the
   backend can be swapped at build time (AURA_USE_JOLT). */
class IWorld
{
public:
    virtual ~IWorld() = default;

    virtual AuraResultCode CreateBody(const AuraBodyDesc& desc, AuraBodyHandle* outBody) = 0;
    virtual AuraResultCode DestroyBody(AuraBodyHandle body) = 0;
    virtual AuraResultCode SetKinematicTarget(AuraBodyHandle body, const AuraPose& pose) = 0;
    virtual AuraResultCode GetBodyState(AuraBodyHandle body, AuraBodyState* outState) const = 0;
    virtual uint32_t CopyBodyStates(AuraBodyState* buffer, uint32_t capacity) const = 0;
    virtual uint32_t BodyCount() const = 0;

    virtual void Step(float deltaTime) = 0;

    virtual uint32_t PendingEventCount() const = 0;
    virtual uint32_t CopyEvents(AuraPhysicsEvent* buffer, uint32_t capacity) = 0;
    virtual uint32_t CopyContacts(AuraContact* buffer, uint32_t capacity) const = 0;

    virtual AuraResultCode SetSurfaceVelocity(AuraBodyHandle body, const AuraVec3& velocity) = 0;
    virtual AuraResultCode CreateWater(const AuraWaterDesc& desc, AuraWaterHandle* outWater) = 0;
    virtual AuraResultCode DestroyWater(AuraWaterHandle water) = 0;
    virtual AuraResultCode SetWaterParameters(AuraWaterHandle water, const AuraWaterDesc& desc) = 0;
    virtual AuraResultCode ApplyWaterStep(AuraWaterHandle water, float deltaTime) = 0;

    virtual bool Raycast(const AuraRay& ray, float maxDistance, const AuraQueryFilter& filter, AuraQueryHit* outHit) = 0;
    virtual uint32_t RaycastAll(const AuraRay& ray, float maxDistance, const AuraQueryFilter& filter, AuraQueryHit* buffer, uint32_t capacity) = 0;
    virtual uint32_t OverlapSphere(const AuraVec3& center, float radius, const AuraQueryFilter& filter, AuraQueryHit* buffer, uint32_t capacity) = 0;

    virtual AuraResultCode OverlapPoint(const AuraVec3&, const AuraQueryFilter&, AuraQueryHit*, uint32_t, uint32_t*) { return AURA_UNSUPPORTED_QUERY; }
    virtual AuraResultCode OverlapBox(const AuraVec3&, const AuraVec3&, const AuraQuat&, const AuraQueryFilter&, AuraQueryHit*, uint32_t, uint32_t*) { return AURA_UNSUPPORTED_QUERY; }
    virtual AuraResultCode OverlapCapsule(const AuraVec3&, const AuraVec3&, float, const AuraQueryFilter&, AuraQueryHit*, uint32_t, uint32_t*) { return AURA_UNSUPPORTED_QUERY; }
    virtual AuraResultCode OverlapShape(const AuraShapeDesc&, const AuraPose&, const AuraQueryFilter&, AuraQueryHit*, uint32_t, uint32_t*) { return AURA_UNSUPPORTED_QUERY; }
    virtual AuraResultCode SphereCast(const AuraVec3&, float, const AuraVec3&, float, const AuraQueryFilter&, AuraQueryHit*, bool*) { return AURA_UNSUPPORTED_QUERY; }
    virtual AuraResultCode CapsuleCast(const AuraVec3&, const AuraVec3&, float, const AuraVec3&, float, const AuraQueryFilter&, AuraQueryHit*, bool*) { return AURA_UNSUPPORTED_QUERY; }
    virtual AuraResultCode BoxCast(const AuraVec3&, const AuraVec3&, const AuraQuat&, const AuraVec3&, float, const AuraQueryFilter&, AuraQueryHit*, bool*) { return AURA_UNSUPPORTED_QUERY; }
    virtual AuraResultCode ShapeCast(const AuraShapeDesc&, const AuraPose&, const AuraVec3&, float, const AuraQueryFilter&, AuraQueryHit*, bool*) { return AURA_UNSUPPORTED_QUERY; }

    virtual uint64_t ComputeStateHash() const = 0;
    virtual AuraResultCode ApplyStates(const AuraBodyState* states, uint32_t count) = 0;
    /* Snapshot v3: backend-specific per-body state (exact rotation, motion type, kinematic target). The defaults
       carry nothing, so a backend without extras round-trips through ApplyStates only. Callers validate handles first. */
    virtual uint32_t CopyBodyExtras(BodyExtra* buffer, uint32_t capacity) const
    {
        uint32_t count = BodyCount();
        if (count > capacity)
            count = capacity;
        for (uint32_t i = 0; i < count; ++i)
            buffer[i] = BodyExtra{};
        return count;
    }
    virtual AuraResultCode ApplyStatesWithExtras(const AuraBodyState* states, const BodyExtra*, uint32_t count) { return ApplyStates(states, count); }

    virtual AuraResultCode CreateJoint(const AuraJointDesc& desc, uint64_t* outJoint) = 0;
    virtual AuraResultCode DestroyJoint(uint64_t joint) = 0;
    virtual bool HasJoint(uint64_t joint) const = 0;

    /* v10 runtime body/joint control. Backends that do not override these report
       AURA_UNSUPPORTED_OPERATION instead of silently ignoring the call. */
    virtual AuraResultCode SetLinearVelocity(AuraBodyHandle, const AuraVec3&) { return AURA_UNSUPPORTED_OPERATION; }
    virtual AuraResultCode SetAngularVelocity(AuraBodyHandle, const AuraVec3&) { return AURA_UNSUPPORTED_OPERATION; }
    virtual AuraResultCode AddForce(AuraBodyHandle, const AuraVec3&) { return AURA_UNSUPPORTED_OPERATION; }
    virtual AuraResultCode AddImpulse(AuraBodyHandle, const AuraVec3&) { return AURA_UNSUPPORTED_OPERATION; }
    virtual AuraResultCode AddTorque(AuraBodyHandle, const AuraVec3&) { return AURA_UNSUPPORTED_OPERATION; }
    virtual AuraResultCode AddAngularImpulse(AuraBodyHandle, const AuraVec3&) { return AURA_UNSUPPORTED_OPERATION; }
    virtual AuraResultCode SetBodyPose(AuraBodyHandle, const AuraPose&, bool) { return AURA_UNSUPPORTED_OPERATION; }
    virtual AuraResultCode SetGravityScale(AuraBodyHandle, float) { return AURA_UNSUPPORTED_OPERATION; }
    virtual AuraResultCode SetFriction(AuraBodyHandle, float) { return AURA_UNSUPPORTED_OPERATION; }
    virtual AuraResultCode SetRestitution(AuraBodyHandle, float) { return AURA_UNSUPPORTED_OPERATION; }
    virtual AuraResultCode SetMotionType(AuraBodyHandle, AuraBodyType) { return AURA_UNSUPPORTED_OPERATION; }
    virtual AuraResultCode SetBodyLayer(AuraBodyHandle, AuraLayer, uint64_t) { return AURA_UNSUPPORTED_OPERATION; }
    virtual AuraResultCode SetBodyEnabled(AuraBodyHandle, bool) { return AURA_UNSUPPORTED_OPERATION; }
    virtual AuraResultCode IsBodyEnabled(AuraBodyHandle, bool*) const { return AURA_UNSUPPORTED_OPERATION; }
    virtual AuraResultCode SetJointMotor(uint64_t, const AuraJointMotorDesc&) { return AURA_UNSUPPORTED_OPERATION; }
    virtual AuraResultCode SetJointLimits(uint64_t, bool, float, float) { return AURA_UNSUPPORTED_OPERATION; }
    virtual AuraResultCode SetJointBreakThreshold(uint64_t, float, float) { return AURA_UNSUPPORTED_OPERATION; }
    virtual AuraResultCode IsJointBroken(uint64_t, bool*) const { return AURA_UNSUPPORTED_OPERATION; }
    virtual AuraResultCode GetJointFeedback(uint64_t, AuraJointFeedback*) const { return AURA_UNSUPPORTED_OPERATION; }
    virtual AuraResultCode SetJointAxisLimits(uint64_t, uint32_t, const AuraJointAxisLimit&) { return AURA_UNSUPPORTED_OPERATION; }
    virtual AuraResultCode SetJointAxisMotor(uint64_t, uint32_t, const AuraJointMotorDesc&) { return AURA_UNSUPPORTED_OPERATION; }
    virtual AuraResultCode SetJointTarget(uint64_t, const AuraVec3&) { return AURA_UNSUPPORTED_OPERATION; }

    /* Package E: world gravity, runtime collision detection and force fields (aura_force_fields.h). */
    virtual AuraResultCode SetWorldGravity(const AuraVec3&) { return AURA_UNSUPPORTED_OPERATION; }
    virtual AuraResultCode GetWorldGravity(AuraVec3*) const { return AURA_UNSUPPORTED_OPERATION; }
    virtual AuraResultCode SetBodyCollisionDetection(AuraBodyHandle, int32_t) { return AURA_UNSUPPORTED_OPERATION; }
    virtual ForceFieldRegistry* ForceFields() { return nullptr; }

    virtual AuraResultCode CreateCharacter(const AuraCharacterDesc& desc, uint64_t* outCharacter) = 0;
    virtual AuraResultCode DestroyCharacter(uint64_t character) = 0;
    virtual AuraResultCode GetCharacterState(uint64_t character, AuraCharacterState* outState) const = 0;
    virtual AuraResultCode MoveCharacter(uint64_t character, const AuraVec3& desiredTranslation, float deltaTime) = 0;

    /* Snapshot support: character states are copied/applied in slot order so a
       round trip restores every live character. */
    virtual uint32_t CharacterCount() const = 0;
    virtual uint32_t CopyCharacterStates(AuraCharacterState* buffer, uint32_t capacity) const = 0;
    virtual AuraResultCode ApplyCharacterStates(const AuraCharacterState* states, uint32_t count) = 0;

    virtual AuraResultCode CreateVehicle(const AuraVehicleDesc&, AuraVehicleHandle*) { return AURA_UNSUPPORTED_SHAPE; }
    virtual AuraResultCode DestroyVehicle(AuraVehicleHandle) { return AURA_UNSUPPORTED_SHAPE; }
    virtual AuraResultCode SetVehicleInput(AuraVehicleHandle, float, float, float, float) { return AURA_UNSUPPORTED_SHAPE; }
    virtual AuraResultCode GetVehicleWheelState(AuraVehicleHandle, uint32_t, AuraVehicleWheelState*) const { return AURA_UNSUPPORTED_SHAPE; }
    virtual AuraResultCode CreateSoftBody(const AuraSoftBodyDesc&, AuraSoftBodyHandle*) { return AURA_UNSUPPORTED_SHAPE; }
    virtual AuraResultCode DestroySoftBody(AuraSoftBodyHandle) { return AURA_UNSUPPORTED_SHAPE; }
    virtual AuraResultCode GetSoftBodyState(AuraSoftBodyHandle, float*, uint32_t, AuraSoftBodyState*) const { return AURA_UNSUPPORTED_SHAPE; }
    virtual AuraResultCode CreateRagdoll(const AuraRagdollDesc&, AuraRagdollHandle*) { return AURA_UNSUPPORTED_SHAPE; }
    virtual AuraResultCode DestroyRagdoll(AuraRagdollHandle) { return AURA_UNSUPPORTED_SHAPE; }
    virtual AuraResultCode GetRagdollPose(AuraRagdollHandle, AuraPose*, uint32_t, uint32_t*) const { return AURA_UNSUPPORTED_SHAPE; }
    virtual AuraResultCode SetRagdollPose(AuraRagdollHandle, const AuraPose*, uint32_t) { return AURA_UNSUPPORTED_SHAPE; }
};

IWorld* CreateWorldImpl(const AuraWorldDesc& desc);

} // namespace aura
