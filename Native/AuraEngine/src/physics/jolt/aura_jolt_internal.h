#pragma once

/* Shared internal surface of the Jolt backend.

   The monolithic JoltWorld implementation is split across per-feature
   translation units (core, shapes, queries, contacts, joints, character).
   Every unit includes this header so it can reach the single JoltWorld::Impl
   state and the shared Jolt<->Aura conversion helpers without editing a
   sibling unit. Adding a feature should mean adding a new .cpp file, not
   growing one shared switch. */

#include "aura/aura_types.h"
#include "aura_world.h"
#include "aura_jolt_world.h"

#include <Jolt/Jolt.h>

#include <Jolt/Core/Factory.h>
#include <Jolt/Core/JobSystemThreadPool.h>
#include <Jolt/Core/TempAllocator.h>
#include <Jolt/Physics/Body/Body.h>
#include <Jolt/Physics/Body/BodyCreationSettings.h>
#include <Jolt/Physics/Body/BodyInterface.h>
#include <Jolt/Physics/Body/BodyLockInterface.h>
#include <Jolt/Physics/Body/BodyFilter.h>
#include <Jolt/Physics/Character/CharacterVirtual.h>
#include <Jolt/Physics/Collision/CastResult.h>
#include <Jolt/Physics/Collision/CollidePointResult.h>
#include <Jolt/Physics/Collision/CollideShape.h>
#include <Jolt/Physics/Collision/ContactListener.h>
#include <Jolt/Physics/Collision/NarrowPhaseQuery.h>
#include <Jolt/Physics/Collision/RayCast.h>
#include <Jolt/Physics/Collision/Shape/BoxShape.h>
#include <Jolt/Physics/Collision/Shape/CapsuleShape.h>
#include <Jolt/Physics/Collision/Shape/ConvexHullShape.h>
#include <Jolt/Physics/Collision/Shape/CylinderShape.h>
#include <Jolt/Physics/Collision/Shape/HeightFieldShape.h>
#include <Jolt/Physics/Collision/Shape/MeshShape.h>
#include <Jolt/Physics/Collision/Shape/OffsetCenterOfMassShape.h>
#include <Jolt/Physics/Collision/Shape/PlaneShape.h>
#include <Jolt/Physics/Collision/Shape/SphereShape.h>
#include <Jolt/Physics/Collision/Shape/TaperedCapsuleShape.h>
#include <Jolt/Physics/Collision/Shape/TaperedCylinderShape.h>
#include <Jolt/Physics/Collision/Shape/StaticCompoundShape.h>
#include <Jolt/Physics/Collision/ShapeFilter.h>
#include <Jolt/Physics/Collision/SimShapeFilter.h>
#include <Jolt/Physics/Constraints/TwoBodyConstraint.h>
#include <Jolt/Physics/PhysicsSystem.h>

#include <algorithm>
#include <cstdint>
#include <mutex>
#include <thread>
#include <unordered_map>
#include <unordered_set>
#include <vector>

namespace aura
{

constexpr uint32_t kMaxLayers = 64;
constexpr uint32_t kMaxBodies = 65536;

inline JPH::Vec3 ToVec3(const AuraVec3& v) { return JPH::Vec3(v.x, v.y, v.z); }
inline JPH::RVec3 ToRVec3(const AuraVec3& v) { return JPH::RVec3(v.x, v.y, v.z); }
inline JPH::Quat ToQuat(const AuraQuat& q) { return JPH::Quat(q.x, q.y, q.z, q.w); }
inline AuraVec3 ToAura(const JPH::Vec3& v) { return AuraVec3{ v.GetX(), v.GetY(), v.GetZ() }; }
#ifdef JPH_DOUBLE_PRECISION
inline AuraVec3 ToAura(const JPH::RVec3& v) { return AuraVec3{ static_cast<float>(v.GetX()), static_cast<float>(v.GetY()), static_cast<float>(v.GetZ()) }; }
#endif
inline AuraQuat ToAura(const JPH::Quat& q) { return AuraQuat{ q.GetX(), q.GetY(), q.GetZ(), q.GetW() }; }

inline uint64_t UserDataFromHandle(AuraBodyHandle handle)
{
    return (static_cast<uint64_t>(handle.index) << 32) | handle.generation;
}

class BroadPhaseLayerInterfaceImpl final : public JPH::BroadPhaseLayerInterface
{
public:
    uint GetNumBroadPhaseLayers() const override { return 2; }

    JPH::BroadPhaseLayer GetBroadPhaseLayer(JPH::ObjectLayer layer) const override
    {
        return JPH::BroadPhaseLayer(layer == 0 ? 0 : 1);
    }

#if defined(JPH_EXTERNAL_PROFILE) || defined(JPH_PROFILE_ENABLED)
    virtual const char* GetBroadPhaseLayerName(JPH::BroadPhaseLayer layer) const override
    {
        return layer == JPH::BroadPhaseLayer(0) ? "static" : "moving";
    }
#endif
};

class ObjectVsBroadPhaseLayerFilterImpl final : public JPH::ObjectVsBroadPhaseLayerFilter
{
public:
    bool ShouldCollide(JPH::ObjectLayer, JPH::BroadPhaseLayer) const override { return true; }
};

class ObjectLayerPairFilterImpl final : public JPH::ObjectLayerPairFilter
{
public:
    const uint64_t* Matrix = nullptr;

    bool ShouldCollide(JPH::ObjectLayer a, JPH::ObjectLayer b) const override
    {
        if (Matrix == nullptr || a >= kMaxLayers || b >= kMaxLayers)
            return true;
        return ((Matrix[a] >> b) & 1ull) != 0ull && ((Matrix[b] >> a) & 1ull) != 0ull;
    }
};

struct RayCollector final : public JPH::CastRayCollector
{
    JPH::RayCastResult results[64];
    uint32_t count = 0;

    void AddHit(const JPH::RayCastResult& inResult) override
    {
        if (count < 64)
            results[count++] = inResult;
    }
};

struct ShapeCollector final : public JPH::CollideShapeCollector
{
    JPH::CollideShapeResult results[64];
    uint32_t count = 0;

    void AddHit(const JPH::CollideShapeResult& inResult) override
    {
        if (count < 64)
            results[count++] = inResult;
    }
};

inline bool ShapeFiltersCollide(uint32_t groupA, uint32_t maskA, uint32_t groupB, uint32_t maskB)
{
    return (groupA & maskB) != 0u && (groupB & maskA) != 0u;
}

class AuraSimShapeFilter final : public JPH::SimShapeFilter
{
public:
    const std::vector<uint32_t>* groups = nullptr;
    const std::vector<uint32_t>* masks = nullptr;

    bool ShouldCollide(const JPH::Body& body1, const JPH::Shape*, const JPH::SubShapeID&,
                       const JPH::Body& body2, const JPH::Shape*, const JPH::SubShapeID&) const override
    {
        const auto get = [this](const JPH::Body& body, uint32_t& group, uint32_t& mask)
        {
            const uint32_t index = static_cast<uint32_t>(body.GetUserData() >> 32);
            if (groups == nullptr || masks == nullptr || index >= groups->size() || index >= masks->size())
                return;
            group = (*groups)[index];
            mask = (*masks)[index];
        };
        uint32_t group1 = 1u, mask1 = ~0u, group2 = 1u, mask2 = ~0u;
        get(body1, group1, mask1);
        get(body2, group2, mask2);
        return ShapeFiltersCollide(group1, mask1, group2, mask2);
    }
};

class AuraQueryShapeFilter final : public JPH::ShapeFilter
{
public:
    const std::unordered_map<uint32_t, uint32_t>* groups = nullptr;
    const std::unordered_map<uint32_t, uint32_t>* masks = nullptr;
    uint32_t group = 1u;
    uint32_t mask = ~0u;

    bool ShouldCollide(const JPH::Shape*, const JPH::SubShapeID&) const override
    {
        if (mBodyID2.IsInvalid() || groups == nullptr || masks == nullptr)
            return true;
        const uint32_t bodyIndex = mBodyID2.GetIndex();
        const auto groupIt = groups->find(bodyIndex);
        const auto maskIt = masks->find(bodyIndex);
        return groupIt == groups->end() || maskIt == masks->end()
            || ShapeFiltersCollide(group, mask, groupIt->second, maskIt->second);
    }
};

/* Builds a single Jolt shape from an Aura shape description. Defined in
   aura_jolt_shapes.cpp so shape work stays in one feature unit. */
JPH::RefConst<JPH::Shape> MakeShape(const AuraShapeDesc& shape, bool& sensor);

struct JoltWorld::Impl
{
    struct Slot
    {
        bool occupied = false;
        uint32_t generation = 0;
        JPH::BodyID id;
        JPH::Body* body = nullptr;
        bool sensor = false;
        uint32_t shapeFilterGroup = 1u;
        uint32_t shapeFilterMask = ~0u;
    };

    struct JointSlot
    {
        bool occupied = false;
        uint32_t generation = 0;
        JPH::TwoBodyConstraint* constraint = nullptr;
        AuraBodyHandle bodyA{};
        AuraBodyHandle bodyB{};
    };

    struct CharacterSlot
    {
        bool occupied = false;
        uint32_t generation = 0;
        JPH::Ref<JPH::CharacterVirtual> character;
        AuraLayer layer = 0;
    };

    std::vector<Slot> slots;
    std::vector<int> freeSlots;
    std::vector<JointSlot> jointSlots;
    std::vector<int> freeJointSlots;
    std::vector<CharacterSlot> characterSlots;
    std::vector<int> freeCharacterSlots;
    std::unordered_map<uint32_t, uint32_t> idToSlot;
    std::vector<AuraPhysicsEvent> events;
    std::unordered_map<uint64_t, bool> contactTrigger;
    std::unordered_map<uint64_t, AuraContact> contacts;
    std::unordered_map<uint32_t, AuraVec3> surfaceVelocities;
    std::unordered_set<uint32_t> wakeOnStep;
    std::unordered_map<uint32_t, uint32_t> shapeFilterGroups;
    std::unordered_map<uint32_t, uint32_t> shapeFilterMasks;
    std::vector<uint32_t> simShapeFilterGroups;
    std::vector<uint32_t> simShapeFilterMasks;

    /* Jolt calls contact callbacks from its worker threads, so the event buffer
       and the trigger bookkeeping must be synchronized. */
    mutable std::mutex eventMutex;

    BroadPhaseLayerInterfaceImpl broadPhase;
    ObjectVsBroadPhaseLayerFilterImpl objectVsBroadPhase;
    ObjectLayerPairFilterImpl objectVsObject;
    JPH::TempAllocatorImpl tempAllocator{ 64 * 1024 * 1024 };
    JPH::JobSystemThreadPool jobSystem{ JPH::cMaxPhysicsJobs, JPH::cMaxPhysicsBarriers, static_cast<int>(std::max(1u, std::thread::hardware_concurrency()) - 1u) };
    JPH::PhysicsSystem physics;
    AuraSimShapeFilter simShapeFilter;

    AuraPhysicsMode mode = AURA_MODE_FULL_3D;
    AuraVec3 gravity{ 0.0f, -9.81f, 0.0f };
    uint64_t matrix[kMaxLayers];
    float lastDelta = 1.0f / 60.0f;

    class Listener final : public JPH::ContactListener
    {
    public:
        Impl* owner = nullptr;

        void OnContactAdded(const JPH::Body& body1, const JPH::Body& body2, const JPH::ContactManifold& manifold, JPH::ContactSettings& settings) override
        {
            owner->ApplySurfaceVelocity(body1, body2, settings);
            owner->OnContactAdded(body1, body2, manifold);
        }

        void OnContactPersisted(const JPH::Body& body1, const JPH::Body& body2, const JPH::ContactManifold& manifold, JPH::ContactSettings& settings) override
        {
            owner->ApplySurfaceVelocity(body1, body2, settings);
            owner->OnContactPersisted(body1, body2, manifold);
        }

        void OnContactRemoved(const JPH::SubShapeIDPair& pair) override
        {
            owner->OnContactRemoved(pair);
        }
    };

    Listener listener;

    explicit Impl(const AuraWorldDesc& desc)
    {
        mode = desc.mode;
        gravity = desc.mode == AURA_MODE_PLANE_2D ? AuraVec3{ desc.gravity.x, desc.gravity.y, 0.0f } : desc.gravity;
        for (uint32_t i = 0; i < kMaxLayers; ++i)
            matrix[i] = ~0ull;
        if (desc.collisionMasks != nullptr)
        {
            for (uint32_t i = 0; i < desc.collisionMaskCount && i < kMaxLayers; ++i)
                matrix[i] = desc.collisionMasks[i];
        }

        objectVsObject.Matrix = matrix;
        listener.owner = this;

        physics.Init(kMaxBodies, 0, 8192, 8192, broadPhase, objectVsBroadPhase, objectVsObject);
        physics.SetGravity(ToVec3(gravity));
        physics.SetContactListener(&listener);
        simShapeFilter.groups = &simShapeFilterGroups;
        simShapeFilter.masks = &simShapeFilterMasks;
        physics.SetSimShapeFilter(&simShapeFilter);

        /* Smaller penetration slop so resting bodies do not visibly sink into
           the ground (Jolt default is 0.02). */
        JPH::PhysicsSettings settings = physics.GetPhysicsSettings();
        settings.mPenetrationSlop = 0.0f;
        physics.SetPhysicsSettings(settings);
    }

    static AuraBodyHandle MakeHandle(const Slot& slot, int index)
    {
        AuraBodyHandle handle{};
        handle.index = static_cast<uint32_t>(index);
        handle.generation = slot.generation;
        return handle;
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

    AuraBodyHandle HandleFromBodyId(JPH::BodyID id) const
    {
        auto it = idToSlot.find(id.GetIndex());
        if (it == idToSlot.end())
            return AuraBodyHandle{ 0xFFFFFFFFu, 0xFFFFFFFFu };
        return MakeHandle(slots[it->second], static_cast<int>(it->second));
    }

    static uint64_t MakeJointHandle(const JointSlot& slot, int index)
    {
        return (static_cast<uint64_t>(slot.generation) << 32) | static_cast<uint32_t>(index);
    }

    JointSlot* FindJoint(uint64_t handle)
    {
        const uint32_t index = static_cast<uint32_t>(handle & 0xFFFFFFFFull);
        const uint32_t generation = static_cast<uint32_t>(handle >> 32);
        if (index < jointSlots.size())
        {
            JointSlot& slot = jointSlots[index];
            if (slot.occupied && slot.generation == generation)
                return &slot;
        }
        return nullptr;
    }

    const JointSlot* FindJoint(uint64_t handle) const
    {
        const uint32_t index = static_cast<uint32_t>(handle & 0xFFFFFFFFull);
        const uint32_t generation = static_cast<uint32_t>(handle >> 32);
        if (index < jointSlots.size())
        {
            const JointSlot& slot = jointSlots[index];
            if (slot.occupied && slot.generation == generation)
                return &slot;
        }
        return nullptr;
    }

    static uint64_t MakeCharacterHandle(const CharacterSlot& slot, int index)
    {
        return (static_cast<uint64_t>(slot.generation) << 32) | static_cast<uint32_t>(index);
    }

    CharacterSlot* FindCharacter(uint64_t handle)
    {
        const uint32_t index = static_cast<uint32_t>(handle & 0xFFFFFFFFull);
        const uint32_t generation = static_cast<uint32_t>(handle >> 32);
        if (index < characterSlots.size())
        {
            CharacterSlot& slot = characterSlots[index];
            if (slot.occupied && slot.generation == generation)
                return &slot;
        }
        return nullptr;
    }

    const CharacterSlot* FindCharacter(uint64_t handle) const
    {
        const uint32_t index = static_cast<uint32_t>(handle & 0xFFFFFFFFull);
        const uint32_t generation = static_cast<uint32_t>(handle >> 32);
        if (index < characterSlots.size())
        {
            const CharacterSlot& slot = characterSlots[index];
            if (slot.occupied && slot.generation == generation)
                return &slot;
        }
        return nullptr;
    }

    /* Resolves the body a query should ignore (AuraQueryFilter flags bit 2). */
    JPH::BodyID IgnoredBody(const AuraQueryFilter& filter) const
    {
        if ((filter.flags & 4) == 0)
            return JPH::BodyID();
        const Slot* slot = Find(filter.ignoredBody);
        return slot != nullptr ? slot->id : JPH::BodyID();
    }

    /* Contact / trigger bookkeeping (defined in aura_jolt_contacts.cpp). */
    void ApplySurfaceVelocity(const JPH::Body& body1, const JPH::Body& body2, JPH::ContactSettings& settings);
    void RecordContact(const AuraBodyHandle& a, const AuraBodyHandle& b, uint64_t key, const JPH::ContactManifold& manifold);
    void OnContactPersisted(const JPH::Body& body1, const JPH::Body& body2, const JPH::ContactManifold& manifold);
    void OnContactAdded(const JPH::Body& body1, const JPH::Body& body2, const JPH::ContactManifold& manifold);
    void OnContactRemoved(const JPH::SubShapeIDPair& pair);

    static AuraBodyHandle EntityHandleBody(const JPH::Body& body)
    {
        const uint64_t userData = body.GetUserData();
        return AuraBodyHandle{ static_cast<uint32_t>(userData >> 32), static_cast<uint32_t>(userData & 0xFFFFFFFFu) };
    }

    /* Composite shape assembly (defined in aura_jolt_shapes.cpp). */
    JPH::RefConst<JPH::Shape> BuildShape(const AuraBodyDesc& desc, bool& anySensor);

    /* Surface normal lookup (defined in aura_jolt_queries.cpp). */
    JPH::Vec3 SurfaceNormal(JPH::BodyID id, const JPH::SubShapeID& subShape, JPH::RVec3Arg point) const;

    void FillState(AuraBodyHandle handle, const Slot& slot, AuraBodyState& state) const
    {
        const JPH::BodyInterface& bi = physics.GetBodyInterface();
        state.body = handle;
        state.entity = AuraEntityHandle{ 0, 0 };
        state.pose.position = ToAura(bi.GetPosition(slot.id));
        state.pose.rotation = ToAura(bi.GetRotation(slot.id));
        state.linearVelocity = ToAura(bi.GetLinearVelocity(slot.id));
        state.angularVelocity = ToAura(bi.GetAngularVelocity(slot.id));
        state.isAwake = bi.IsActive(slot.id) ? 1 : 0;
        state.flags = 0;
    }
};

} // namespace aura
