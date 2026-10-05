#include "aura_jolt_internal.h"

#include <Jolt/Physics/Body/AllowedDOFs.h>
#include <Jolt/RegisterTypes.h>

#include <cstdarg>
#include <cstdio>
#include <cstring>
#include <mutex>

#include "aura_event_order.h"

namespace aura
{
namespace
{
void AuraJoltTrace(const char* fmt, ...)
{
    va_list args;
    va_start(args, fmt);
    std::vfprintf(stderr, fmt, args);
    va_end(args);
}

#ifdef JPH_ENABLE_ASSERTS
bool AuraJoltAssertFailed(const char* expression, const char* message, const char* file, JPH::uint line)
{
    std::fprintf(stderr, "[aura][jolt-assert] %s : %s (%s:%u)\n", expression, message != nullptr ? message : "", file, line);
    return false;
}
#endif

struct JoltGlobal
{
    std::once_flag once;

    /* Several threads may create their first world at once; call_once makes the one-time registration race-free
       and blocks the others until it has finished. */
    void Ensure()
    {
        std::call_once(once, []() { Initialize(); });
    }

    static void Initialize()
    {
        JPH::Trace = &AuraJoltTrace;
#ifdef JPH_ENABLE_ASSERTS
        JPH::AssertFailed = &AuraJoltAssertFailed;
#endif

        JPH::RegisterDefaultAllocator();
        JPH::Factory::sInstance = new JPH::Factory();
        JPH::RegisterTypes();
    }
};

JoltGlobal& Global()
{
    static JoltGlobal instance;
    return instance;
}
} // namespace

JoltWorld::Impl* JoltWorld::CreateImpl(const AuraWorldDesc& desc)
{
    Global().Ensure();
    return new Impl(desc);
}

JoltWorld::JoltWorld(const AuraWorldDesc& desc)
    : impl_(CreateImpl(desc))
{
}

JoltWorld::~JoltWorld()
{
    delete impl_;
}

AuraResultCode JoltWorld::CreateBody(const AuraBodyDesc& desc, AuraBodyHandle* outBody)
{
    if (outBody == nullptr || desc.shapeCount == 0 || desc.shapes == nullptr)
        return AURA_INVALID_DEFINITION;

    if (desc.type == AURA_BODY_DYNAMIC)
    {
        for (uint32_t i = 0; i < desc.shapeCount; ++i)
            if (desc.shapes[i].type == AURA_SHAPE_TRIANGLE_MESH)
                return AURA_UNSUPPORTED_SHAPE;
    }

    bool sensor = false;
    JPH::RefConst<JPH::Shape> shape = impl_->BuildShape(desc, sensor);
    if (shape == nullptr)
        return AURA_UNSUPPORTED_SHAPE;

    int index;
    if (!impl_->freeSlots.empty())
    {
        index = impl_->freeSlots.back();
        impl_->freeSlots.pop_back();
    }
    else
    {
        index = static_cast<int>(impl_->slots.size());
        impl_->slots.push_back(Impl::Slot{});
    }

    Impl::Slot& slot = impl_->slots[index];
    const uint32_t generation = index < static_cast<int>(impl_->slots.size()) ? slot.generation : 0;

    JPH::BodyInterface& bi = impl_->physics.GetBodyInterface();
    const JPH::EMotionType motion = desc.type == AURA_BODY_STATIC ? JPH::EMotionType::Static
        : (desc.type == AURA_BODY_KINEMATIC ? JPH::EMotionType::Kinematic : JPH::EMotionType::Dynamic);

    const AuraBodyHandle handle{ static_cast<uint32_t>(index), generation };
    JPH::BodyCreationSettings settings(shape.GetPtr(), ToRVec3(desc.initialPose.position), ToQuat(desc.initialPose.rotation), motion, desc.layer);

    /* Per-shape material: when the body-level material is unset, take friction
       and restitution from the first shape, matching the managed backend. */
    float friction = desc.friction;
    float restitution = desc.restitution;
    if (friction == 0.0f && restitution == 0.0f && desc.shapeCount > 0)
    {
        friction = desc.shapes[0].friction;
        restitution = desc.shapes[0].restitution;
    }
    settings.mFriction = friction;
    settings.mRestitution = restitution;
    settings.mEnhancedInternalEdgeRemoval = true;
    settings.mGravityFactor = desc.gravityScale;
    settings.mIsSensor = sensor;
    settings.mUserData = UserDataFromHandle(handle);
    settings.mLinearDamping = desc.linearDamping;
    settings.mAngularDamping = desc.angularDamping;
    settings.mAllowSleeping = desc.allowSleeping != 0;
    settings.mMotionQuality = desc.collisionDetection == 1 ? JPH::EMotionQuality::LinearCast : JPH::EMotionQuality::Discrete;
    if (desc.maxLinearVelocity > 0.0f)
        settings.mMaxLinearVelocity = desc.maxLinearVelocity;
    if (desc.maxAngularVelocity > 0.0f)
        settings.mMaxAngularVelocity = desc.maxAngularVelocity;

    const uint32_t allowed = 0x3Fu & ~desc.freezeFlags;
    settings.mAllowedDOFs = static_cast<JPH::EAllowedDOFs>(allowed == 0 ? 0x3Fu : allowed);

    /* Bodies made only of shapes that can be simulated dynamically keep Jolt
       motion properties for every motion type so Aura_SetMotionType can promote
       or demote them at runtime. Mesh, height field and plane shapes are static-only. */
    bool canChangeMotion = true;
    for (uint32_t i = 0; i < desc.shapeCount; ++i)
    {
        const AuraShapeType type = desc.shapes[i].type;
        if (type == AURA_SHAPE_TRIANGLE_MESH || type == AURA_SHAPE_HEIGHT_FIELD || type == AURA_SHAPE_PLANE)
            canChangeMotion = false;
    }
    if (canChangeMotion)
    {
        settings.mAllowDynamicOrKinematic = true;
        settings.mOverrideMassProperties = JPH::EOverrideMassProperties::CalculateInertia;
        settings.mMassPropertiesOverride.mMass = desc.mass > 0.0f ? desc.mass : 1.0f;
        settings.mInertiaMultiplier = desc.inertiaMultiplier > 0.0f ? desc.inertiaMultiplier : 1.0f;
    }

    if (motion == JPH::EMotionType::Dynamic)
    {
        settings.mOverrideMassProperties = JPH::EOverrideMassProperties::CalculateInertia;
        settings.mMassPropertiesOverride.mMass = desc.mass > 0.0f ? desc.mass : 1.0f;
        settings.mInertiaMultiplier = desc.inertiaMultiplier > 0.0f ? desc.inertiaMultiplier : 1.0f;

        if (desc.centerOfMass.x != 0.0f || desc.centerOfMass.y != 0.0f || desc.centerOfMass.z != 0.0f)
        {
            JPH::OffsetCenterOfMassShapeSettings comSettings(
                JPH::Vec3(desc.centerOfMass.x, desc.centerOfMass.y, desc.centerOfMass.z), shape.GetPtr());
            JPH::ShapeSettings::ShapeResult comResult = comSettings.Create();
            if (comResult.IsValid())
                shape = comResult.Get();
        }
    }

    JPH::Body* body = bi.CreateBody(settings);
    if (body == nullptr)
        return AURA_OUT_OF_MEMORY;


    const JPH::BodyID id = body->GetID();
    bi.AddBody(id, JPH::EActivation::Activate);
    bi.SetLinearVelocity(id, ToVec3(desc.initialLinearVelocity));
    bi.SetAngularVelocity(id, ToVec3(desc.initialAngularVelocity));

    slot.occupied = true;
    slot.id = id;
    slot.body = body;
    slot.sensor = sensor;
    slot.enabled = true;
    slot.canChangeMotion = canChangeMotion;
    slot.shapeFilterGroup = desc.shapeCount > 0 && desc.shapes[0].shapeFilterGroup != 0u ? desc.shapes[0].shapeFilterGroup : 1u;
    slot.shapeFilterMask = desc.shapeCount > 0 && desc.shapes[0].shapeFilterMask != 0u ? desc.shapes[0].shapeFilterMask : ~0u;
    impl_->idToSlot[id.GetIndex()] = static_cast<uint32_t>(index);
    impl_->shapeFilterGroups[id.GetIndex()] = slot.shapeFilterGroup;
    impl_->shapeFilterMasks[id.GetIndex()] = slot.shapeFilterMask;
    if (impl_->simShapeFilterGroups.size() <= id.GetIndex())
    {
        impl_->simShapeFilterGroups.resize(id.GetIndex() + 1, 1u);
        impl_->simShapeFilterMasks.resize(id.GetIndex() + 1, ~0u);
    }
    if (impl_->simBodyCollisionMasks.size() <= id.GetIndex())
        impl_->simBodyCollisionMasks.resize(id.GetIndex() + 1, ~0ull);
    impl_->simBodyCollisionMasks[id.GetIndex()] = ~0ull;
    impl_->simShapeFilterGroups[id.GetIndex()] = slot.shapeFilterGroup;
    impl_->simShapeFilterMasks[id.GetIndex()] = slot.shapeFilterMask;

    *outBody = handle;
    return AURA_SUCCESS;
}

AuraResultCode JoltWorld::DestroyBody(AuraBodyHandle body)
{
    Impl::Slot* slot = impl_->Find(body);
    if (slot == nullptr)
        return AURA_INVALID_HANDLE;

    for (size_t index = 0; index < impl_->vehicleSlots.size(); ++index)
    {
        Impl::VehicleSlot& vehicle = impl_->vehicleSlots[index];
        if (vehicle.occupied && vehicle.chassis.index == body.index && vehicle.chassis.generation == body.generation)
            DestroyVehicle(Impl::MakeVehicleHandle(vehicle, static_cast<int>(index)));
    }

    JPH::BodyInterface& bi = impl_->physics.GetBodyInterface();
    impl_->idToSlot.erase(slot->id.GetIndex());
    impl_->shapeFilterGroups.erase(slot->id.GetIndex());
    impl_->shapeFilterMasks.erase(slot->id.GetIndex());

    for (size_t index = 0; index < impl_->jointSlots.size(); ++index)
    {
        Impl::JointSlot& joint = impl_->jointSlots[index];
        if (!joint.occupied)
            continue;
        const bool touchesA = joint.bodyA.index == body.index && joint.bodyA.generation == body.generation;
        const bool touchesB = joint.bodyB.index == body.index && joint.bodyB.generation == body.generation;
        if (!touchesA && !touchesB)
            continue;
        impl_->RemoveJointConstraint(index);
        joint.occupied = false;
        joint.generation += 1;
        impl_->freeJointSlots.push_back(static_cast<int>(index));
    }

    {
        std::lock_guard<std::mutex> lock(impl_->eventMutex);
        for (auto it = impl_->contacts.begin(); it != impl_->contacts.end();)
        {
            const bool touchesA = it->second.bodyA.index == body.index && it->second.bodyA.generation == body.generation;
            const bool touchesB = it->second.bodyB.index == body.index && it->second.bodyB.generation == body.generation;
            it = (touchesA || touchesB) ? impl_->contacts.erase(it) : ++it;
        }
    }

    if (slot->enabled)
        bi.RemoveBody(slot->id);
    bi.DestroyBody(slot->id);
    slot->occupied = false;
    slot->body = nullptr;
    slot->generation += 1;
    impl_->freeSlots.push_back(static_cast<int>(body.index));
    return AURA_SUCCESS;
}

AuraResultCode JoltWorld::SetKinematicTarget(AuraBodyHandle body, const AuraPose& pose)
{
    Impl::Slot* slot = impl_->Find(body);
    if (slot == nullptr)
        return AURA_INVALID_HANDLE;

    if (!slot->enabled)
        return AURA_BODY_DISABLED;

    /* MoveKinematic needs motion properties; plane, mesh and height-field bodies have none and crash Jolt. */
    if (slot->body == nullptr || slot->body->GetMotionType() != JPH::EMotionType::Kinematic)
        return AURA_UNSUPPORTED_OPERATION;

    const JPH::BodyInterface& bi = impl_->physics.GetBodyInterface();
    const_cast<JPH::BodyInterface&>(bi).MoveKinematic(slot->id, ToRVec3(pose.position), ToQuat(pose.rotation), impl_->lastDelta);
    slot->kinematicTargetPending = true;
    return AURA_SUCCESS;
}

AuraResultCode JoltWorld::GetBodyState(AuraBodyHandle body, AuraBodyState* outState) const
{
    const Impl::Slot* slot = impl_->Find(body);
    if (slot == nullptr || outState == nullptr)
        return AURA_INVALID_HANDLE;

    impl_->FillState(body, *slot, *outState);
    return AURA_SUCCESS;
}

uint32_t JoltWorld::CopyBodyStates(AuraBodyState* buffer, uint32_t capacity) const
{
    uint32_t written = 0;
    for (size_t index = 0; index < impl_->slots.size() && written < capacity; ++index)
    {
        const Impl::Slot& slot = impl_->slots[index];
        if (!slot.occupied)
            continue;
        impl_->FillState(Impl::MakeHandle(slot, static_cast<int>(index)), slot, buffer[written]);
        ++written;
    }
    return written;
}

uint32_t JoltWorld::BodyCount() const
{
    uint32_t count = 0;
    for (const Impl::Slot& slot : impl_->slots)
        if (slot.occupied)
            ++count;
    return count;
}

void JoltWorld::Step(float deltaTime)
{
    if (deltaTime > 0.0f)
        impl_->lastDelta = deltaTime;
    std::vector<uint32_t> wake;
    {
        std::lock_guard<std::mutex> lock(impl_->eventMutex);
        impl_->events.clear();
        wake.assign(impl_->wakeOnStep.begin(), impl_->wakeOnStep.end());
        impl_->wakeOnStep.clear();
    }

    {
        JPH::BodyInterface& bi = impl_->physics.GetBodyInterface();
        for (uint32_t index : wake)
        {
            if (index < impl_->slots.size() && impl_->slots[index].occupied && impl_->slots[index].enabled)
                bi.ActivateBody(impl_->slots[index].id);
        }
    }

    impl_->ApplyForceFields(deltaTime);
    impl_->physics.Update(deltaTime, 1, &impl_->tempAllocator, &impl_->jobSystem);

    /* Jolt reports contacts from several worker threads in timing-dependent order; make the sequence deterministic. */
    {
        std::lock_guard<std::mutex> lock(impl_->eventMutex);
        SortEventsDeterministic(impl_->events);
    }

    /* A kinematic target is reached within one step. Without this the velocity set by MoveKinematic keeps
       carrying the body past the target on every further step of the same frame and the error compounds. */
    {
        JPH::BodyInterface& bi = impl_->physics.GetBodyInterface();
        for (Impl::Slot& slot : impl_->slots)
        {
            if (!slot.kinematicTargetPending)
                continue;
            slot.kinematicTargetPending = false;
            if (slot.occupied && slot.enabled)
                bi.SetLinearAndAngularVelocity(slot.id, JPH::Vec3::sZero(), JPH::Vec3::sZero());
        }
    }

    impl_->ProcessJointBreaks();
}

AuraResultCode JoltWorld::CreateWater(const AuraWaterDesc& desc, AuraWaterHandle* outWater)
{
    if (outWater == nullptr || desc.density <= 0.0f)
        return AURA_INVALID_DEFINITION;
    impl_->water = desc;
    impl_->waterActive = true;
    *outWater = AuraWaterHandle{ 1 };
    return AURA_SUCCESS;
}

AuraResultCode JoltWorld::DestroyWater(AuraWaterHandle water)
{
    if (!impl_->waterActive || water.opaque != 1)
        return AURA_INVALID_HANDLE;
    impl_->waterActive = false;
    return AURA_SUCCESS;
}

AuraResultCode JoltWorld::SetWaterParameters(AuraWaterHandle water, const AuraWaterDesc& desc)
{
    if (!impl_->waterActive || water.opaque != 1 || desc.density <= 0.0f)
        return AURA_INVALID_HANDLE;
    impl_->water = desc;
    return AURA_SUCCESS;
}

AuraResultCode JoltWorld::ApplyWaterStep(AuraWaterHandle water, float deltaTime)
{
    if (!impl_->waterActive || water.opaque != 1 || deltaTime < 0.0f)
        return AURA_INVALID_HANDLE;
    JPH::BodyInterface& bi = impl_->physics.GetBodyInterface();
    const JPH::Vec3 gravity = ToVec3(impl_->gravity);
    for (const Impl::Slot& slot : impl_->slots)
    {
        if (!slot.occupied || !slot.enabled || slot.body == nullptr || slot.body->GetMotionType() != JPH::EMotionType::Dynamic)
            continue;
        const JPH::AABox bounds = slot.body->GetWorldSpaceBounds();
        const float height = bounds.mMax.GetY() - bounds.mMin.GetY();
        const float submerged = height > 0.0f ? std::clamp((impl_->water.surfaceHeight - bounds.mMin.GetY()) / height, 0.0f, 1.0f) : 0.0f;
        if (submerged > 0.0f)
            bi.AddImpulse(slot.id, -gravity * (impl_->water.density * slot.body->GetShape()->GetVolume() * submerged * deltaTime));
    }
    return AURA_SUCCESS;
}

uint32_t JoltWorld::PendingEventCount() const
{
    std::lock_guard<std::mutex> lock(impl_->eventMutex);
    return static_cast<uint32_t>(impl_->events.size());
}

uint32_t JoltWorld::CopyEvents(AuraPhysicsEvent* buffer, uint32_t capacity)
{
    std::lock_guard<std::mutex> lock(impl_->eventMutex);
    const uint32_t count = std::min(static_cast<uint32_t>(impl_->events.size()), capacity);
    for (uint32_t i = 0; i < count; ++i)
        buffer[i] = impl_->events[i];
    impl_->events.erase(impl_->events.begin(), impl_->events.begin() + count);
    return count;
}

uint64_t JoltWorld::ComputeStateHash() const
{
    uint64_t hash = 14695981039346656037ull;
    auto combine = [&hash](uint64_t value)
    {
        hash ^= value;
        hash *= 1099511628211ull;
    };

    AuraBodyState state{};
    for (size_t index = 0; index < impl_->slots.size(); ++index)
    {
        const Impl::Slot& slot = impl_->slots[index];
        if (!slot.occupied)
            continue;
        impl_->FillState(Impl::MakeHandle(slot, static_cast<int>(index)), slot, state);
        combine(index);
        combine(static_cast<uint32_t>(state.pose.position.x * 1000.0f));
        combine(static_cast<uint32_t>(state.pose.position.y * 1000.0f));
        combine(static_cast<uint32_t>(state.pose.position.z * 1000.0f));
    }
    return hash;
}

AuraResultCode JoltWorld::ApplyStates(const AuraBodyState* states, uint32_t count)
{
    return ApplyStatesWithExtras(states, nullptr, count);
}

uint32_t JoltWorld::CopyBodyExtras(BodyExtra* buffer, uint32_t capacity) const
{
    const JPH::BodyInterface& bi = impl_->physics.GetBodyInterface();
    uint32_t written = 0;
    for (size_t index = 0; index < impl_->slots.size() && written < capacity; ++index)
    {
        const Impl::Slot& slot = impl_->slots[index];
        if (!slot.occupied)
            continue;
        BodyExtra extra{};
        const JPH::EMotionType motion = bi.GetMotionType(slot.id);
        extra.motionType = motion == JPH::EMotionType::Static ? AURA_BODY_STATIC
            : (motion == JPH::EMotionType::Kinematic ? AURA_BODY_KINEMATIC : AURA_BODY_DYNAMIC);
        extra.flags = kBodyExtraHasMotionType | (slot.kinematicTargetPending ? kBodyExtraKinematicPending : 0u);
        buffer[written++] = extra;
    }
    return written;
}

/* The caller (Aura_DeserializeState) has validated every handle and value, so this cannot fail half way. */
AuraResultCode JoltWorld::ApplyStatesWithExtras(const AuraBodyState* states, const BodyExtra* extras, uint32_t count)
{
    JPH::BodyInterface& bi = impl_->physics.GetBodyInterface();
    for (uint32_t i = 0; i < count; ++i)
    {
        const AuraBodyState& state = states[i];
        Impl::Slot* slot = impl_->Find(state.body);
        if (slot == nullptr)
            return AURA_INVALID_HANDLE;

        /* Snapshots carry the enabled flag: restore the pose with the body in the
           simulation, then remove it again when it was disabled when captured. */
        const bool wantEnabled = (state.flags & AURA_BODY_FLAG_DISABLED) == 0u;
        if (!slot->enabled)
            impl_->SetEnabledInternal(*slot, state.body, true);

        /* Restore the motion type when the body can still switch to it (static is always reachable). */
        if (extras != nullptr && (extras[i].flags & kBodyExtraHasMotionType) != 0u && !impl_->IsVehicleChassis(state.body))
        {
            const JPH::EMotionType wanted = extras[i].motionType == AURA_BODY_STATIC ? JPH::EMotionType::Static
                : (extras[i].motionType == AURA_BODY_KINEMATIC ? JPH::EMotionType::Kinematic : JPH::EMotionType::Dynamic);
            if (bi.GetMotionType(slot->id) != wanted && (wanted == JPH::EMotionType::Static || slot->canChangeMotion))
                bi.SetMotionType(slot->id, wanted, JPH::EActivation::DontActivate);
        }

        bi.SetPositionAndRotation(slot->id, ToRVec3(state.pose.position), ToQuat(state.pose.rotation), JPH::EActivation::DontActivate);
        bi.SetLinearVelocity(slot->id, ToVec3(state.linearVelocity));
        bi.SetAngularVelocity(slot->id, ToVec3(state.angularVelocity));

        slot->kinematicTargetPending = extras != nullptr && (extras[i].flags & kBodyExtraKinematicPending) != 0u;

        /* Keep sleeping bodies asleep (and awake ones awake) as captured; the velocity setters above wake on non-zero. */
        if (bi.GetMotionType(slot->id) != JPH::EMotionType::Static)
        {
            if (state.isAwake != 0)
                bi.ActivateBody(slot->id);
            else
                bi.DeactivateBody(slot->id);
        }
        if (!wantEnabled)
            impl_->SetEnabledInternal(*slot, state.body, false);
    }
    return AURA_SUCCESS;
}

} // namespace aura
