#pragma once

#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

#ifndef AURA_ENGINE_ABI_VERSION
#define AURA_ENGINE_ABI_VERSION 12u
#endif

typedef uint32_t AuraEntityIndex;
typedef uint32_t AuraEntityGeneration;
typedef uint32_t AuraBodyIndex;
typedef uint32_t AuraBodyGeneration;
typedef uint32_t AuraShapeIndex;
typedef uint32_t AuraShapeGeneration;
typedef uint32_t AuraTick;
typedef uint32_t AuraLayer;

typedef enum AuraResultCode
{
    AURA_SUCCESS = 0,
    AURA_INVALID_HANDLE = 1,
    AURA_INVALID_DEFINITION = 2,
    AURA_UNSUPPORTED_SHAPE = 3,
    AURA_UNSUPPORTED_QUERY = 4,
    AURA_OUT_OF_MEMORY = 5,
    AURA_INVALID_WORLD = 6,
    AURA_ABI_MISMATCH = 7,
    AURA_CAPACITY_EXCEEDED = 8,
    AURA_BACKEND_FAILURE = 9,
    /* v10: the body is removed from the simulation (Aura_SetBodyEnabled). */
    AURA_BODY_DISABLED = 10,
    /* v10: the operation is valid but not available for this body/joint type or backend. */
    AURA_UNSUPPORTED_OPERATION = 11
} AuraResultCode;

typedef enum AuraBodyType
{
    AURA_BODY_STATIC = 0,
    AURA_BODY_DYNAMIC = 1,
    AURA_BODY_KINEMATIC = 2
} AuraBodyType;

typedef enum AuraShapeType
{
    AURA_SHAPE_BOX = 0,
    AURA_SHAPE_SPHERE = 1,
    AURA_SHAPE_CAPSULE = 2,
    AURA_SHAPE_CYLINDER = 3,
    AURA_SHAPE_CONVEX_MESH = 4,
    AURA_SHAPE_TRIANGLE_MESH = 5,
    AURA_SHAPE_PLANE = 6,
    AURA_SHAPE_TAPERED_CAPSULE = 7,
    AURA_SHAPE_TAPERED_CYLINDER = 8,
    AURA_SHAPE_HEIGHT_FIELD = 9
} AuraShapeType;

typedef enum AuraActiveEdgeMode
{
    AURA_ACTIVE_EDGES_ONLY = 0,
    AURA_ACTIVE_EDGES_ALL = 1
} AuraActiveEdgeMode;

typedef enum AuraPhysicsMode
{
    AURA_MODE_FULL_3D = 0,
    AURA_MODE_PLANE_2D = 1
} AuraPhysicsMode;

typedef struct AuraVec3
{
    float x;
    float y;
    float z;
} AuraVec3;

typedef struct AuraQuat
{
    float x;
    float y;
    float z;
    float w;
} AuraQuat;

typedef struct AuraPose
{
    AuraVec3 position;
    AuraQuat rotation;
} AuraPose;

typedef struct AuraEntityHandle
{
    AuraEntityIndex index;
    AuraEntityGeneration generation;
} AuraEntityHandle;

typedef struct AuraBodyHandle
{
    AuraBodyIndex index;
    AuraBodyGeneration generation;
} AuraBodyHandle;

typedef struct AuraShapeDesc
{
    AuraShapeType type;
    AuraPose localPose;
    uint8_t isTrigger;
    /* Plane2D (Box2D) only: solid only against bodies approaching from the shape's
       local +Y side (rotated by localPose); Jolt ignores it. Reuses a former pad byte. */
    uint8_t isOneWay;
    uint8_t _pad0[2];
    float friction;
    float restitution;
    float density;
    AuraLayer layer;
    AuraVec3 halfExtents;
    float radius;
    float height;
    int32_t meshAsset;

    /* Plane normal (local) and tapered top radius. Kept before the pointers so
       the fixed layout is identical across compilers and runtimes. */
    AuraVec3 planeNormal;
    float topRadius;

    /* Convex hull / triangle mesh data (backend-dependent, optional). */
    const float* vertices;
    uint32_t vertexCount;
    const uint32_t* indices;
    uint32_t indexCount;

    /* Optional per-triangle/square material indices. For triangle meshes this
       contains indexCount / 3 entries; height fields use (N - 1)^2 entries. */
    const uint32_t* materialIndices;
    uint32_t materialIndexCount;
    uint32_t shapeFilterGroup;
    uint32_t shapeFilterMask;
    int32_t activeEdgeMode;
    float activeEdgeCosThresholdAngle;
} AuraShapeDesc;

typedef struct AuraBodyDesc
{
    AuraBodyType type;
    uint32_t layer;
    uint64_t collisionMask;
    int32_t groupIndex;
    float mass;
    float gravityScale;
    float friction;
    float restitution;
    float density;
    AuraPose initialPose;
    AuraVec3 initialLinearVelocity;
    AuraVec3 initialAngularVelocity;
    const AuraShapeDesc* shapes;
    uint32_t shapeCount;
    uint32_t _pad0;

    /* Rigidbody-like tuning (fixed-size fields kept after the pointers). */
    float linearDamping;
    float angularDamping;
    float maxLinearVelocity;
    float maxAngularVelocity;
    AuraVec3 centerOfMass;
    float inertiaMultiplier;
    uint32_t freezeFlags;
    int32_t collisionDetection;
    uint8_t allowSleeping;
    uint8_t _pad1[3];
} AuraBodyDesc;

typedef enum AuraJointType
{
    AURA_JOINT_DISTANCE = 0,
    AURA_JOINT_FIXED = 1,
    AURA_JOINT_HINGE = 2,
    AURA_JOINT_POINT = 3,
    AURA_JOINT_SLIDER = 4,
    AURA_JOINT_CONE = 5,
    AURA_JOINT_SWING_TWIST = 6,
    AURA_JOINT_PULLEY = 7,
    AURA_JOINT_SPRING = 8,
    AURA_JOINT_SIX_DOF = 9,
    /* Box2D (Plane2D) only; Jolt returns AURA_UNSUPPORTED_QUERY from Aura_CreateJoint.
       WHEEL: bodyA chassis, bodyB wheel, axisA = suspension axis, spring* = suspension, min/maxLimit = travel,
       motor* = wheel spin (rad/s, max torque). MOUSE: bodyB is dragged toward the world point anchorB; bodyA may be
       an invalid handle (internal static anchor); springFrequency/springDamping = stiffness, maxMotorForce = max force.
       ROPE: distance = max length, optional min length through enableLimit + minLimit; no push, no spring. */
    AURA_JOINT_WHEEL = 10,
    AURA_JOINT_MOUSE = 11,
    AURA_JOINT_ROPE = 12,
    /* Jolt 3D only. Gear/RackAndPinion couple two existing joints (jointRefA/B), see AuraJointDesc. */
    AURA_JOINT_GEAR = 13,
    AURA_JOINT_RACK_AND_PINION = 14,
    /* Reserved: Jolt path constraints are not implemented; CreateJoint returns AURA_UNSUPPORTED_OPERATION. */
    AURA_JOINT_PATH = 15
} AuraJointType;

/* Handle of an existing joint (the two halves of the uint64 joint handle). */
typedef struct AuraJointRef
{
    uint32_t index;
    uint32_t generation;
} AuraJointRef;

/* SixDof per-axis mode. Axis order: 0..2 translation X/Y/Z, 3..5 rotation X/Y/Z (constraint frame). */
typedef enum AuraJointAxisMode
{
    AURA_JOINT_AXIS_LOCKED = 0,
    AURA_JOINT_AXIS_FREE = 1,
    AURA_JOINT_AXIS_LIMITED = 2
} AuraJointAxisMode;

typedef struct AuraJointAxisLimit
{
    uint8_t mode;
    uint8_t _pad0[3];
    /* LIMITED range in m (translation) or rad (rotation). Rotation Y/Z with a cone swing use maxLimit as the half angle. */
    float minLimit;
    float maxLimit;
    /* Max friction force (N) or torque (N*m) while not motor driven. 0 = none. */
    float maxFriction;
} AuraJointAxisLimit;

typedef struct AuraJointDesc
{
    int32_t type;
    AuraBodyHandle bodyA;
    AuraBodyHandle bodyB;
    AuraVec3 anchorA;
    AuraVec3 anchorB;
    AuraVec3 axisA;
    AuraVec3 axisB;
    AuraVec3 normalAxisA;
    AuraVec3 normalAxisB;
    AuraVec3 fixedPoint;
    float distance;
    float minLimit;
    float maxLimit;
    float swingLimit;
    float motorTargetVelocity;
    float maxMotorForce;
    float springFrequency;
    float springDamping;
    uint8_t enableLimit;
    uint8_t motorEnabled;
    uint8_t _pad0[2];
    /* ---- v11 appendix (Jolt 3D constraint set; a zeroed appendix keeps the pre-v11 meaning) ----
       Pulley: fixedPoint/fixedPointB are the two world rope anchors, ratio (0 = 1) scales segment B, the rope length is
       distance (0 = current length) or, with enableLimit, minLimit..maxLimit.
       Cone: half angle = maxLimit or swingLimit.
       SwingTwist: swingLimit = normal half cone, planeSwingLimit (0 = swingLimit) = plane half cone, minLimit/maxLimit =
       twist range, maxFriction = friction torque, pyramidSwing selects the pyramid swing shape.
       SixDof: axes[6] (axisA/normalAxisA = frame X/Y on body A, axisB/normalAxisB on body B), pyramidSwing.
       Gear: axisA/axisB = hinge axes in world space, ratio = teeth ratio (A rotation = -ratio * B rotation),
       jointRefA/B = the hinge joints of body A / body B. RackAndPinion: axisA = pinion hinge axis, axisB = rack slider
       axis, ratio = pinion radians per rack metre, jointRefA = pinion hinge, jointRefB = rack slider. */
    AuraVec3 fixedPointB;
    float ratio;
    float planeSwingLimit;
    float maxFriction;
    AuraJointRef jointRefA;
    AuraJointRef jointRefB;
    AuraJointAxisLimit axes[6];
    uint8_t pyramidSwing;
    uint8_t _pad1[3];
} AuraJointDesc;

/* v10 runtime joint control. Motor modes: OFF, drive to a target relative
   velocity (rad/s for hinge/revolute, m/s for slider/prismatic) or, on Jolt
   only, drive to a target position (rad or m, relative to the creation pose). */
typedef enum AuraJointMotorMode
{
    AURA_JOINT_MOTOR_OFF = 0,
    AURA_JOINT_MOTOR_VELOCITY = 1,
    AURA_JOINT_MOTOR_POSITION = 2
} AuraJointMotorMode;

typedef struct AuraJointMotorDesc
{
    int32_t mode;
    float target;
    /* Maximum motor force (slider/prismatic, N) or torque (hinge/revolute, N*m). Must be > 0 when mode != OFF. */
    float maxForce;
    /* Position mode spring tuning; 0 keeps the backend default. */
    float springFrequency;
    float springDamping;
    uint32_t _pad0;
} AuraJointMotorDesc;

typedef struct AuraJointFeedback
{
    /* Reaction force magnitude (N) the joint applied during the last step, excluding motor drive and limit-axis load. */
    float force;
    /* Reaction torque magnitude (N*m) outside the joint's free axis, including limit torque, excluding motor drive. */
    float torque;
    /* Force (slider/prismatic) or torque (hinge/revolute) currently spent by the motor. */
    float motorLoad;
    /* Current hinge angle (rad) or slider translation (m) relative to the creation pose. */
    float position;
    int32_t motorMode;
    uint8_t isBroken;
    uint8_t _pad0[3];
} AuraJointFeedback;

typedef struct AuraContact
{
    AuraBodyHandle bodyA;
    AuraBodyHandle bodyB;
    AuraVec3 point;
    AuraVec3 normal;
    float penetration;
    float impulse;
} AuraContact;

typedef struct AuraCharacterDesc
{
    AuraPose pose;
    float radius;
    float height;
    float mass;
    float maxSlopeAngle;
    AuraLayer layer;
    uint32_t _pad0;
    uint64_t collisionMask;
    /* Appended: largest ledge the Box2D (Plane2D) mover steps up while walking; 0 disables. Jolt ignores it. */
    float stepHeight;
    uint32_t _pad1;
} AuraCharacterDesc;

typedef struct AuraCharacterState
{
    AuraVec3 position;
    AuraVec3 velocity;
    uint8_t isGrounded;
    uint8_t _pad0[3];
} AuraCharacterState;

typedef struct AuraWorldDesc
{
    AuraPhysicsMode mode;
    AuraVec3 gravity;
    uint32_t initialBodyCapacity;
    float fixedDeltaTime;
    const uint64_t* collisionMasks;
    uint32_t collisionMaskCount;
} AuraWorldDesc;

/* AuraBodyState::flags bits (v10). */
#define AURA_BODY_FLAG_DISABLED 1u

typedef struct AuraBodyState
{
    AuraBodyHandle body;
    AuraEntityHandle entity;
    AuraPose pose;
    AuraVec3 linearVelocity;
    AuraVec3 angularVelocity;
    uint8_t isAwake;
    uint8_t _pad0[3];
    uint32_t flags;
} AuraBodyState;

typedef struct AuraPhysicsEvent
{
    int32_t type;
    AuraEntityHandle entityA;
    AuraEntityHandle entityB;
    AuraBodyHandle bodyA;
    AuraBodyHandle bodyB;
    AuraShapeIndex shapeA;
    AuraShapeIndex shapeB;
    AuraVec3 point;
    AuraVec3 normal;
    float impulse;
} AuraPhysicsEvent;

typedef struct AuraRay
{
    AuraVec3 origin;
    AuraVec3 direction;
} AuraRay;

typedef struct AuraQueryFilter
{
    uint64_t layerMask;
    int32_t triggerInteraction;
    int32_t flags;
    AuraEntityHandle ignoredEntity;
    AuraBodyHandle ignoredBody;
    uint32_t shapeFilterGroup;
    uint32_t shapeFilterMask;
    int32_t activeEdgeMode;
    AuraVec3 activeEdgeMovementDirection;
} AuraQueryFilter;

typedef struct AuraQueryHit
{
    AuraEntityHandle entity;
    AuraBodyHandle body;
    AuraShapeIndex shape;
    float distance;
    AuraVec3 point;
    AuraVec3 normal;
    uint32_t materialIndex;
} AuraQueryHit;

typedef struct AuraWorldHandle
{
    uint64_t opaque;
} AuraWorldHandle;

typedef struct AuraWaterHandle
{
    uint64_t opaque;
} AuraWaterHandle;

typedef struct AuraWaterDesc
{
    float surfaceHeight;
    AuraVec3 surfaceNormal;
    float density;
    float linearDrag;
} AuraWaterDesc;

/* Vehicle ABI types are appended so existing layouts remain unchanged. */
typedef struct AuraVehicleHandle
{
    uint64_t opaque;
} AuraVehicleHandle;

typedef struct AuraVehicleDesc
{
    AuraBodyHandle chassis;
    AuraVec3 up;
    AuraVec3 forward;
    AuraVec3 wheelPositions[4];
    float wheelRadius;
    float wheelWidth;
    float suspensionMinLength;
    float suspensionMaxLength;
    float suspensionFrequency;
    float suspensionDamping;
    float maxSteerAngle;
    float maxPitchRollAngle;
    float maxEngineTorque;
    AuraLayer wheelObjectLayer;
    uint32_t _pad0;
} AuraVehicleDesc;

typedef struct AuraVehicleWheelState
{
    uint8_t hasContact;
    uint8_t _pad0[3];
    float suspensionLength;
    float steerAngle;
    float angularVelocity;
    AuraVec3 contactNormal;
} AuraVehicleWheelState;

/* Softbody ABI types are appended so the v7 vehicle layouts remain unchanged. */
typedef struct AuraSoftBodyHandle
{
    uint64_t opaque;
} AuraSoftBodyHandle;

typedef struct AuraSoftBodyDesc
{
    AuraPose initialPose;
    AuraLayer objectLayer;
    uint32_t vertexCount;
    const float* vertexPositions;
    uint32_t faceCount;
    const uint32_t* faceIndices;
    const float* inverseMass;
} AuraSoftBodyDesc;

typedef struct AuraSoftBodyState
{
    AuraSoftBodyHandle softBody;
    uint32_t vertexCount;
    float* vertexPositions;
} AuraSoftBodyState;

typedef struct AuraRigJointDesc { int32_t parentIndex; AuraPose bindPose; } AuraRigJointDesc;
typedef struct AuraRigDesc { const AuraRigJointDesc* joints; uint32_t jointCount; } AuraRigDesc;
typedef struct AuraRagdollPartDesc { AuraBodyDesc body; AuraJointDesc jointToParent; } AuraRagdollPartDesc;
typedef struct AuraRagdollDesc
{
    AuraRigDesc rig;
    const AuraRagdollPartDesc* parts;
    uint32_t partCount;
    uint32_t collisionGroup;
} AuraRagdollDesc;
typedef struct AuraRagdollHandle { uint64_t opaque; } AuraRagdollHandle;

/* Package E: force fields (zones). Appended after v10; existing layouts are unchanged. */
typedef struct AuraForceFieldHandle { uint64_t opaque; } AuraForceFieldHandle;

typedef enum AuraForceFieldShape
{
    AURA_FIELD_SHAPE_SPHERE = 0, /* circle in 2D worlds */
    AURA_FIELD_SHAPE_BOX = 1
} AuraForceFieldShape;

typedef enum AuraForceFieldKind
{
    /* Constant vector (acceleration or force). */
    AURA_FIELD_DIRECTIONAL = 0,
    /* Toward the field centre for strength > 0, away for strength < 0. */
    AURA_FIELD_RADIAL = 1,
    /* Linear drag toward the wind velocity in `vector`; strength is the drag rate. */
    AURA_FIELD_DRAG = 2
} AuraForceFieldKind;

typedef enum AuraForceFieldMode
{
    /* Acceleration: scaled by the body gravity scale, independent of mass (drag: strength is 1/s). */
    AURA_FIELD_MODE_ACCELERATION = 0,
    /* Force: divided by the body mass, ignores the gravity scale (drag: strength is N per m/s). */
    AURA_FIELD_MODE_FORCE = 1
} AuraForceFieldMode;

typedef enum AuraForceFieldFalloff
{
    AURA_FIELD_FALLOFF_NONE = 0,
    AURA_FIELD_FALLOFF_LINEAR = 1,
    AURA_FIELD_FALLOFF_INVERSE_SQUARE = 2
} AuraForceFieldFalloff;

typedef struct AuraForceFieldDesc
{
    int32_t shape;
    int32_t kind;
    int32_t mode;
    int32_t falloff;
    AuraPose pose;        /* zone centre and orientation (2D: position x/y and rotation about z) */
    AuraVec3 halfExtents; /* box shape */
    float radius;         /* sphere shape */
    AuraVec3 vector;      /* directional acceleration/force, or drag wind velocity */
    float strength;       /* radial strength (magnitude at distance 1 for inverse-square) or drag rate */
    float minRadius;      /* radial: distances below are clamped to this (softening) */
    float maxRadius;      /* radial: no effect beyond (0 = shape extent) */
    uint64_t layerMask;   /* bit i set = affects bodies on layer i (all bits = every layer) */
    uint8_t enabled;
    uint8_t _pad0[7];
} AuraForceFieldDesc;

#ifdef __cplusplus
}
#endif
