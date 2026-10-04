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

/* v10 body control. All entries return AURA_INVALID_HANDLE for stale handles,
   AURA_INVALID_DEFINITION for non-finite/out-of-range arguments or operations
   that do not apply to the body's motion type (e.g. force on a static body) and
   AURA_BODY_DISABLED while the body is disabled (except Aura_SetBodyEnabled,
   Aura_IsBodyEnabled, Aura_SetBodyPose, Aura_SetGravityScale, Aura_SetFriction,
   Aura_SetRestitution, Aura_SetMotionType and Aura_SetBodyLayer which still
   configure it). 2D worlds ignore the z of linear vectors and the x/y of
   angular vectors. Forces/torques apply to the next step only. */
AuraResultCode Aura_SetLinearVelocity(AuraWorldHandle world, AuraBodyHandle body, AuraVec3 velocity);
AuraResultCode Aura_SetAngularVelocity(AuraWorldHandle world, AuraBodyHandle body, AuraVec3 velocity);
AuraResultCode Aura_AddForce(AuraWorldHandle world, AuraBodyHandle body, AuraVec3 force);
AuraResultCode Aura_AddImpulse(AuraWorldHandle world, AuraBodyHandle body, AuraVec3 impulse);
AuraResultCode Aura_AddTorque(AuraWorldHandle world, AuraBodyHandle body, AuraVec3 torque);
AuraResultCode Aura_AddAngularImpulse(AuraWorldHandle world, AuraBodyHandle body, AuraVec3 impulse);
AuraResultCode Aura_SetBodyPose(AuraWorldHandle world, AuraBodyHandle body, const AuraPose* pose, uint8_t zeroVelocity);
AuraResultCode Aura_SetGravityScale(AuraWorldHandle world, AuraBodyHandle body, float gravityScale);
AuraResultCode Aura_SetFriction(AuraWorldHandle world, AuraBodyHandle body, float friction);
AuraResultCode Aura_SetRestitution(AuraWorldHandle world, AuraBodyHandle body, float restitution);
/* Bodies built from triangle mesh, height field or plane shapes stay static (AURA_UNSUPPORTED_OPERATION). */
AuraResultCode Aura_SetMotionType(AuraWorldHandle world, AuraBodyHandle body, int32_t bodyType);
/* collisionMask bit i = may collide with layer i, applied on top of the world collision matrix. */
AuraResultCode Aura_SetBodyLayer(AuraWorldHandle world, AuraBodyHandle body, AuraLayer layer, uint64_t collisionMask);
AuraResultCode Aura_SetBodyEnabled(AuraWorldHandle world, AuraBodyHandle body, uint8_t enabled);
AuraResultCode Aura_IsBodyEnabled(AuraWorldHandle world, AuraBodyHandle body, uint8_t* outEnabled);

AuraResultCode Aura_Step(AuraWorldHandle world, AuraTick tick, float deltaTime);

AuraResultCode Aura_CopyBodyStates(AuraWorldHandle world, AuraBodyState* buffer, uint32_t capacity, uint32_t* outCount);
AuraResultCode Aura_GetBodyState(AuraWorldHandle world, AuraBodyHandle body, AuraBodyState* outState);

AuraResultCode Aura_CreateWater(AuraWorldHandle world, const AuraWaterDesc* desc, AuraWaterHandle* outWater);
AuraResultCode Aura_DestroyWater(AuraWorldHandle world, AuraWaterHandle water);
AuraResultCode Aura_SetWaterParameters(AuraWorldHandle world, AuraWaterHandle water, const AuraWaterDesc* desc);
AuraResultCode Aura_ApplyWaterStep(AuraWorldHandle world, AuraWaterHandle water, float deltaTime);

AuraResultCode Aura_PendingEventCount(AuraWorldHandle world, uint32_t* outCount);
AuraResultCode Aura_CopyEvents(AuraWorldHandle world, AuraPhysicsEvent* buffer, uint32_t capacity, uint32_t* outCount);

AuraResultCode Aura_Raycast(AuraWorldHandle world, const AuraRay* ray, float maxDistance, const AuraQueryFilter* filter, AuraQueryHit* outHit, uint8_t* outHasHit);
AuraResultCode Aura_RaycastAll(AuraWorldHandle world, const AuraRay* ray, float maxDistance, const AuraQueryFilter* filter, AuraQueryHit* buffer, uint32_t capacity, uint32_t* outCount);
AuraResultCode Aura_OverlapSphere(AuraWorldHandle world, AuraVec3 center, float radius, const AuraQueryFilter* filter, AuraQueryHit* buffer, uint32_t capacity, uint32_t* outCount);
AuraResultCode Aura_OverlapPoint(AuraWorldHandle world, AuraVec3 point, const AuraQueryFilter* filter, AuraQueryHit* buffer, uint32_t capacity, uint32_t* outCount);
AuraResultCode Aura_OverlapBox(AuraWorldHandle world, AuraVec3 center, AuraVec3 halfExtents, AuraQuat rotation, const AuraQueryFilter* filter, AuraQueryHit* buffer, uint32_t capacity, uint32_t* outCount);
AuraResultCode Aura_OverlapCapsule(AuraWorldHandle world, AuraVec3 pointA, AuraVec3 pointB, float radius, const AuraQueryFilter* filter, AuraQueryHit* buffer, uint32_t capacity, uint32_t* outCount);
AuraResultCode Aura_OverlapShape(AuraWorldHandle world, const AuraShapeDesc* shape, const AuraPose* pose, const AuraQueryFilter* filter, AuraQueryHit* buffer, uint32_t capacity, uint32_t* outCount);
AuraResultCode Aura_SphereCast(AuraWorldHandle world, AuraVec3 origin, float radius, AuraVec3 direction, float maxDistance, const AuraQueryFilter* filter, AuraQueryHit* outHit, uint8_t* outHasHit);
AuraResultCode Aura_CapsuleCast(AuraWorldHandle world, AuraVec3 pointA, AuraVec3 pointB, float radius, AuraVec3 direction, float maxDistance, const AuraQueryFilter* filter, AuraQueryHit* outHit, uint8_t* outHasHit);
AuraResultCode Aura_BoxCast(AuraWorldHandle world, AuraVec3 center, AuraVec3 halfExtents, AuraQuat rotation, AuraVec3 direction, float maxDistance, const AuraQueryFilter* filter, AuraQueryHit* outHit, uint8_t* outHasHit);
AuraResultCode Aura_ShapeCast(AuraWorldHandle world, const AuraShapeDesc* shape, const AuraPose* pose, AuraVec3 direction, float maxDistance, const AuraQueryFilter* filter, AuraQueryHit* outHit, uint8_t* outHasHit);

AuraResultCode Aura_CreateJoint(AuraWorldHandle world, const AuraJointDesc* desc, uint64_t* outJoint);
AuraResultCode Aura_DestroyJoint(AuraWorldHandle world, uint64_t joint);
AuraResultCode Aura_HasJoint(AuraWorldHandle world, uint64_t joint, uint8_t* outHas);

/* v10 joint control. Support matrix (everything else returns AURA_UNSUPPORTED_OPERATION):
     motor:  hinge, slider (Jolt: velocity+position; Box2D revolute/prismatic: velocity only)
     limits: hinge, slider
     break:  fixed, point, hinge, slider, distance, spring (torque threshold only for fixed/hinge/slider)
     feedback/IsJointBroken: every joint type. */
AuraResultCode Aura_SetJointMotor(AuraWorldHandle world, uint64_t joint, const AuraJointMotorDesc* motor);
/* Limits are relative to the creation pose. hinge: -pi <= min <= 0 <= max <= pi; slider: min <= 0 <= max. */
AuraResultCode Aura_SetJointLimits(AuraWorldHandle world, uint64_t joint, uint8_t enabled, float minLimit, float maxLimit);
/* Thresholds <= 0 mean unbreakable. A joint that exceeds either one after a step is removed from the
   simulation and flagged broken; its handle stays valid until Aura_DestroyJoint. */
AuraResultCode Aura_SetJointBreakThreshold(AuraWorldHandle world, uint64_t joint, float maxForce, float maxTorque);
AuraResultCode Aura_IsJointBroken(AuraWorldHandle world, uint64_t joint, uint8_t* outBroken);
AuraResultCode Aura_GetJointFeedback(AuraWorldHandle world, uint64_t joint, AuraJointFeedback* outFeedback);

/* Jolt 3D constraint set (SixDof, SwingTwist, Cone, Pulley, Gear, RackAndPinion). Support matrix, everything else
   returns AURA_UNSUPPORTED_OPERATION:
     SetJointMotor:      hinge, slider only (use the axis calls below for SixDof and SwingTwist)
     SetJointLimits:     hinge, slider (position), SwingTwist (twist min/max, rad, -pi..pi), Pulley (rope length min..max, 0 <= min <= max)
     SetJointAxisLimits: SixDof axes 0..5; SwingTwist axis 0 = twist (min/max), 1 = normal swing half cone, 2 = plane swing
                         half cone (max only, mode LIMITED)
     SetJointAxisMotor:  SixDof axes 0..5 (velocity: m/s or rad/s; position: translation axes and rotation X),
                         SwingTwist axis 0 = twist (velocity, position), 1/2 = swing about constraint Y/Z (velocity)
     break thresholds:   adds SixDof, Cone, SwingTwist, Pulley (force only), Gear, RackAndPinion (torque only)
     feedback:           Pulley reports the current rope length in position.
   Gear and RackAndPinion depend on their referenced joints: destroying or breaking either one removes the dependent
   constraint from the simulation and flags it broken (its handle stays valid until Aura_DestroyJoint). */
AuraResultCode Aura_SetJointAxisLimits(AuraWorldHandle world, uint64_t joint, uint32_t axis, const AuraJointAxisLimit* limit);
AuraResultCode Aura_SetJointAxisMotor(AuraWorldHandle world, uint64_t joint, uint32_t axis, const AuraJointMotorDesc* motor);
/* Package B: moves the target of a mouse joint (world space, z ignored in 2D). AURA_UNSUPPORTED_OPERATION for other
   joint types and for Jolt. */
AuraResultCode Aura_SetJointTarget(AuraWorldHandle world, uint64_t joint, AuraVec3 target);

AuraResultCode Aura_CopyContacts(AuraWorldHandle world, AuraContact* buffer, uint32_t capacity, uint32_t* outCount);

AuraResultCode Aura_SetSurfaceVelocity(AuraWorldHandle world, AuraBodyHandle body, AuraVec3 velocity);

AuraResultCode Aura_CreateCharacter(AuraWorldHandle world, const AuraCharacterDesc* desc, uint64_t* outCharacter);
AuraResultCode Aura_DestroyCharacter(AuraWorldHandle world, uint64_t character);
AuraResultCode Aura_GetCharacterState(AuraWorldHandle world, uint64_t character, AuraCharacterState* outState);
AuraResultCode Aura_MoveCharacter(AuraWorldHandle world, uint64_t character, AuraVec3 desiredTranslation, float deltaTime);

AuraResultCode Aura_CreateVehicle(AuraWorldHandle world, const AuraVehicleDesc* desc, AuraVehicleHandle* outVehicle);
AuraResultCode Aura_DestroyVehicle(AuraWorldHandle world, AuraVehicleHandle vehicle);
AuraResultCode Aura_SetVehicleInput(AuraWorldHandle world, AuraVehicleHandle vehicle, float forward, float steering, float brake, float handBrake);
AuraResultCode Aura_GetVehicleWheelState(AuraWorldHandle world, AuraVehicleHandle vehicle, uint32_t wheelIndex, AuraVehicleWheelState* outState);

AuraResultCode Aura_CreateSoftBody(AuraWorldHandle world, const AuraSoftBodyDesc* desc, AuraSoftBodyHandle* outSoftBody);
AuraResultCode Aura_DestroySoftBody(AuraWorldHandle world, AuraSoftBodyHandle softBody);
AuraResultCode Aura_GetSoftBodyState(AuraWorldHandle world, AuraSoftBodyHandle softBody, float* vertexPositions, uint32_t vertexCapacity, AuraSoftBodyState* outState);
AuraResultCode Aura_CreateRagdoll(AuraWorldHandle world, const AuraRagdollDesc* desc, AuraRagdollHandle* outRagdoll);
AuraResultCode Aura_DestroyRagdoll(AuraWorldHandle world, AuraRagdollHandle ragdoll);
AuraResultCode Aura_GetRagdollPose(AuraWorldHandle world, AuraRagdollHandle ragdoll, AuraPose* buffer, uint32_t capacity, uint32_t* outCount);
AuraResultCode Aura_SetRagdollPose(AuraWorldHandle world, AuraRagdollHandle ragdoll, const AuraPose* poses, uint32_t poseCount);

/* Package E: world gravity, runtime collision detection mode and force fields.
   Aura_SetWorldGravity wakes sleeping bodies; 2D worlds ignore z. collisionDetection: 0 discrete, 1 continuous
   (Jolt LinearCast, Box2D bullet). Force fields apply to dynamic enabled bodies whose centre of mass lies inside the
   zone, once per Aura_Step before integration, in ascending body then field order. Stale field handles return
   AURA_INVALID_HANDLE. Fields are configuration: they are not part of snapshots or the state hash. */
AuraResultCode Aura_SetWorldGravity(AuraWorldHandle world, AuraVec3 gravity);
AuraResultCode Aura_GetWorldGravity(AuraWorldHandle world, AuraVec3* outGravity);
AuraResultCode Aura_SetBodyCollisionDetection(AuraWorldHandle world, AuraBodyHandle body, int32_t collisionDetection);
AuraResultCode Aura_CreateForceField(AuraWorldHandle world, const AuraForceFieldDesc* desc, AuraForceFieldHandle* outField);
AuraResultCode Aura_UpdateForceField(AuraWorldHandle world, AuraForceFieldHandle field, const AuraForceFieldDesc* desc);
AuraResultCode Aura_DestroyForceField(AuraWorldHandle world, AuraForceFieldHandle field);

AuraResultCode Aura_ComputeStateHash(AuraWorldHandle world, uint64_t* outHash);

AuraResultCode Aura_SerializeState(AuraWorldHandle world, uint8_t* buffer, uint32_t capacity, uint32_t* outSize);
AuraResultCode Aura_DeserializeState(AuraWorldHandle world, const uint8_t* buffer, uint32_t size);

#ifdef __cplusplus
}
#endif
