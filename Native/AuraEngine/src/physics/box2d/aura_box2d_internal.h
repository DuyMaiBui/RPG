#pragma once

/* Private to the Box2D backend translation units (world, queries, joints). */

#include "aura_box2d_world.h"
#include "aura_force_fields.h"
#include "aura_event_order.h"
#include "aura_box2d_character.h"
#include "aura_box2d_oneway.h"

#include <box2d/box2d.h>

#include <algorithm>
#include <cmath>
#include <cstdint>
#include <vector>

namespace aura
{
namespace box2d_internal
{
inline constexpr uint32_t kMaxLayers = 64;

inline float AngleFromQuat(const AuraQuat& q) { return 2.0f * std::atan2(q.z, q.w); }

inline AuraQuat QuatFromAngle(float angle)
{
    AuraQuat q{};
    q.z = std::sin(angle * 0.5f);
    q.w = std::cos(angle * 0.5f);
    return q;
}

inline b2Vec2 ToVec2(const AuraVec3& v) { return b2Vec2{ v.x, v.y }; }
inline AuraVec3 ToAura(const b2Vec2& v) { return AuraVec3{ v.x, v.y, 0.0f }; }
inline AuraQuat ToQuat(const b2Rot& rot) { return QuatFromAngle(b2Rot_GetAngle(rot)); }

inline uint64_t UserDataFromHandleValue(AuraBodyHandle handle)
{
    return (static_cast<uint64_t>(handle.index) << 32) | handle.generation;
}

inline void* Encode(AuraBodyHandle handle)
{
    return reinterpret_cast<void*>(UserDataFromHandleValue(handle));
}

inline AuraBodyHandle Decode(void* userData)
{
    const uint64_t value = reinterpret_cast<uint64_t>(userData);
    return AuraBodyHandle{ static_cast<uint32_t>(value >> 32), static_cast<uint32_t>(value & 0xFFFFFFFFu) };
}
constexpr float kPi = 3.14159265358979323846f;

inline bool IsFinite(float value) { return std::isfinite(value); }
inline bool IsFinite(const AuraVec3& v) { return std::isfinite(v.x) && std::isfinite(v.y) && std::isfinite(v.z); }
inline float Length(b2Vec2 v) { return std::sqrt(v.x * v.x + v.y * v.y); }

/* Resting bodies sleep through property changes, so wake them (not static bodies, not disabled ones). */
inline void Wake(b2BodyId body)
{
    if (b2Body_IsEnabled(body) && b2Body_GetType(body) != b2_staticBody)
        b2Body_SetAwake(body, true);
}
} // namespace box2d_internal

using namespace box2d_internal;

struct Box2DWorld::Impl
{
    struct Slot
    {
        bool occupied = false;
        uint32_t generation = 0;
        b2BodyId body = b2_nullBodyId;
        /* Set by SetKinematicTarget; the velocity it produced is cleared after the step that reached the target. */
        bool kinematicTargetPending = false;
        /* Area of the solid (non-sensor) shapes: the 2D "volume" displaced in water, per metre of depth. */
        float area = 0.0f;
    };

    struct JointSlot
    {
        bool occupied = false;
        uint32_t generation = 0;
        b2JointId joint = b2_nullJointId;
        AuraBodyHandle bodyA{};
        AuraBodyHandle bodyB{};
        AuraJointType type = AURA_JOINT_FIXED;
        b2Vec2 localAxisA{ 1.0f, 0.0f };
        float breakForce = 0.0f;
        float breakTorque = 0.0f;
        bool broken = false;
        float lastForce = 0.0f;
        float lastTorque = 0.0f;
        /* Requested limits (hinge angle, slider/wheel translation, rope length range) and rest length (distance/spring).
           The constraint itself is fed an eased copy, see Box2DWorld::Impl::EaseJoint. */
        bool limitEnabled = false;
        float limitMin = 0.0f;
        float limitMax = 0.0f;
        float restLength = 0.0f;
        float springHertz = 0.0f; /* requested spring frequency, capped to the step rate by EaseJoint */
    };

    b2WorldId world = b2_nullWorldId;
    b2BodyId groundBody = b2_nullBodyId; /* lazily created static anchor for mouse joints */
    std::vector<Slot> slots;
    std::vector<int> freeSlots;
    std::vector<JointSlot> jointSlots;
    std::vector<int> freeJointSlots;
    std::vector<AuraPhysicsEvent> events;
    uint64_t matrix[kMaxLayers];
    float lastDelta = 1.0f / 60.0f;
    AuraVec3 gravity{ 0.0f, -9.81f, 0.0f };
    AuraWaterDesc water{};
    bool waterActive = false;
    Box2DCharacters characters; /* package C: kinematic capsule movers (aura_box2d_character.cpp) */
    ForceFieldRegistry fields; /* package E: zones applied before every step (aura_box2d_fields.cpp) */

    static uint64_t MakeJointHandle(const JointSlot& slot, int index)
    {
        return (static_cast<uint64_t>(slot.generation) << 32) | static_cast<uint32_t>(index);
    }

    JointSlot* FindJoint(uint64_t handle)
    {
        const uint32_t index = static_cast<uint32_t>(handle & 0xFFFFFFFFull);
        const uint32_t generation = static_cast<uint32_t>(handle >> 32);
        if (index < jointSlots.size() && jointSlots[index].occupied && jointSlots[index].generation == generation)
            return &jointSlots[index];
        return nullptr;
    }

    const JointSlot* FindJoint(uint64_t handle) const
    {
        const uint32_t index = static_cast<uint32_t>(handle & 0xFFFFFFFFull);
        const uint32_t generation = static_cast<uint32_t>(handle >> 32);
        if (index < jointSlots.size() && jointSlots[index].occupied && jointSlots[index].generation == generation)
            return &jointSlots[index];
        return nullptr;
    }

    b2BodyId GroundBody()
    {
        if (!b2Body_IsValid(groundBody))
        {
            const b2BodyDef def = b2DefaultBodyDef();
            groundBody = b2CreateBody(world, &def);
        }
        return groundBody;
    }

    explicit Impl(const AuraWorldDesc& desc)
    {
        for (uint32_t i = 0; i < kMaxLayers; ++i)
            matrix[i] = ~0ull;
        if (desc.collisionMasks != nullptr)
        {
            for (uint32_t i = 0; i < desc.collisionMaskCount && i < kMaxLayers; ++i)
                matrix[i] = desc.collisionMasks[i];
        }

        b2WorldDef worldDef = b2DefaultWorldDef();
        worldDef.gravity = b2Vec2{ desc.gravity.x, desc.gravity.y };
        gravity = desc.gravity;
        world = b2CreateWorld(&worldDef);
        oneway::Install(world);
    }

    Slot* Find(AuraBodyHandle handle)
    {
        if (handle.index < slots.size())
        {
            Slot& slot = slots[handle.index];
            if (slot.occupied && slot.generation == handle.generation)
                return &slot;
        }
        return nullptr;
    }

    const Slot* Find(AuraBodyHandle handle) const
    {
        if (handle.index < slots.size())
        {
            const Slot& slot = slots[handle.index];
            if (slot.occupied && slot.generation == handle.generation)
                return &slot;
        }
        return nullptr;
    }

    static AuraBodyHandle HandleFromShape(b2ShapeId shape)
    {
        if (!b2Shape_IsValid(shape))
            return AuraBodyHandle{ 0xFFFFFFFFu, 0xFFFFFFFFu };
        const b2BodyId body = b2Shape_GetBody(shape);
        return Decode(b2Body_GetUserData(body));
    }

    void FillState(AuraBodyHandle handle, const Slot& slot, AuraBodyState& state) const
    {
        state.body = handle;
        state.entity = AuraEntityHandle{ 0, 0 };
        const b2Vec2 position = b2Body_GetPosition(slot.body);
        state.pose.position = ToAura(position);
        state.pose.rotation = ToQuat(b2Body_GetRotation(slot.body));
        state.linearVelocity = ToAura(b2Body_GetLinearVelocity(slot.body));
        const float angular = b2Body_GetAngularVelocity(slot.body);
        state.angularVelocity = AuraVec3{ 0.0f, 0.0f, angular };
        state.isAwake = b2Body_IsAwake(slot.body) ? 1 : 0;
        state.flags = b2Body_IsEnabled(slot.body) ? 0u : AURA_BODY_FLAG_DISABLED;
    }

    /* Joint loads of the last step (aura_box2d_world.cpp, joint control section). */
    bool JointLoads(const JointSlot& joint, float& force, float& torque, float& motorLoad) const;
    void ProcessJointBreaks();
    void EaseJoint(JointSlot& joint, float deltaTime);
    bool WellConditioned(const JointSlot& joint) const;
    void EaseJoints(float deltaTime);
    void ApplyForceFields(float deltaTime); /* aura_box2d_fields.cpp */

    void GatherEvents()
    {
        events.clear();

        const b2ContactEvents contacts = b2World_GetContactEvents(world);
        for (int i = 0; i < contacts.beginCount; ++i)
        {
            const AuraBodyHandle a = HandleFromShape(contacts.beginEvents[i].shapeIdA);
            const AuraBodyHandle b = HandleFromShape(contacts.beginEvents[i].shapeIdB);
            if (a.index == 0xFFFFFFFFu || b.index == 0xFFFFFFFFu)
                continue;
            AuraPhysicsEvent event{};
            event.type = 0;
            event.bodyA = a;
            event.bodyB = b;
            events.push_back(event);
        }
        for (int i = 0; i < contacts.endCount; ++i)
        {
            const AuraBodyHandle a = HandleFromShape(contacts.endEvents[i].shapeIdA);
            const AuraBodyHandle b = HandleFromShape(contacts.endEvents[i].shapeIdB);
            if (a.index == 0xFFFFFFFFu || b.index == 0xFFFFFFFFu)
                continue;
            AuraPhysicsEvent event{};
            event.type = 1;
            event.bodyA = a;
            event.bodyB = b;
            events.push_back(event);
        }

        const b2SensorEvents sensors = b2World_GetSensorEvents(world);
        for (int i = 0; i < sensors.beginCount; ++i)
        {
            const AuraBodyHandle sensor = HandleFromShape(sensors.beginEvents[i].sensorShapeId);
            const AuraBodyHandle visitor = HandleFromShape(sensors.beginEvents[i].visitorShapeId);
            if (sensor.index == 0xFFFFFFFFu || visitor.index == 0xFFFFFFFFu)
                continue;
            AuraPhysicsEvent event{};
            event.type = 2;
            event.bodyA = sensor;
            event.bodyB = visitor;
            events.push_back(event);
        }
        for (int i = 0; i < sensors.endCount; ++i)
        {
            const AuraBodyHandle sensor = HandleFromShape(sensors.endEvents[i].sensorShapeId);
            const AuraBodyHandle visitor = HandleFromShape(sensors.endEvents[i].visitorShapeId);
            if (sensor.index == 0xFFFFFFFFu || visitor.index == 0xFFFFFFFFu)
                continue;
            AuraPhysicsEvent event{};
            event.type = 3;
            event.bodyA = sensor;
            event.bodyB = visitor;
            events.push_back(event);
        }

        SortEventsDeterministic(events);
    }
};

} // namespace aura
