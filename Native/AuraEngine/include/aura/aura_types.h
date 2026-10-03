#pragma once

#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

#ifndef AURA_ENGINE_ABI_VERSION
#define AURA_ENGINE_ABI_VERSION 8u
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
    AURA_BACKEND_FAILURE = 9
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
    uint8_t _pad0[3];
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
    AURA_JOINT_SIX_DOF = 9
} AuraJointType;

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
} AuraJointDesc;

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

#ifdef __cplusplus
}
#endif
