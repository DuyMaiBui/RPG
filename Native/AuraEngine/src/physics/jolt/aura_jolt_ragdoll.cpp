#include "aura_jolt_internal.h"
#include <Jolt/Physics/Constraints/SwingTwistConstraint.h>

namespace aura
{

AuraResultCode JoltWorld::CreateRagdoll(const AuraRagdollDesc& desc, AuraRagdollHandle* outRagdoll)
{
    if (outRagdoll == nullptr || desc.parts == nullptr || desc.rig.joints == nullptr
        || desc.partCount == 0 || desc.partCount != desc.rig.jointCount)
        return AURA_INVALID_DEFINITION;

    JPH::Ref<JPH::RagdollSettings> settings = new JPH::RagdollSettings();
    settings->mSkeleton = new JPH::Skeleton();
    settings->mParts.resize(desc.partCount);
    for (uint32_t index = 0; index < desc.partCount; ++index)
    {
        const int32_t parent = desc.rig.joints[index].parentIndex;
        if (parent >= static_cast<int32_t>(index) || parent < -1)
            return AURA_INVALID_DEFINITION;
        settings->mSkeleton->AddJoint("aura", parent);

        if (desc.parts[index].body.shapeCount != 1 || desc.parts[index].body.shapes == nullptr)
            return AURA_UNSUPPORTED_SHAPE;
        bool sensor = false;
        JPH::RefConst<JPH::Shape> shape = MakeShape(desc.parts[index].body.shapes[0], sensor);
        if (shape == nullptr)
            return AURA_UNSUPPORTED_SHAPE;
        JPH::RagdollSettings::Part& part = settings->mParts[index];
        const AuraBodyDesc& body = desc.parts[index].body;
        part.SetShape(shape.GetPtr());
        part.mPosition = ToRVec3(body.initialPose.position);
        part.mRotation = ToQuat(body.initialPose.rotation);
        part.mObjectLayer = body.layer;
        part.mMotionType = JPH::EMotionType::Dynamic;
        part.mIsSensor = sensor;
        part.mFriction = body.friction;
        part.mRestitution = body.restitution;
        part.mGravityFactor = body.gravityScale;
        part.mOverrideMassProperties = JPH::EOverrideMassProperties::CalculateInertia;
        part.mMassPropertiesOverride.mMass = body.mass > 0.0f ? body.mass : 1.0f;
        part.mInertiaMultiplier = body.inertiaMultiplier > 0.0f ? body.inertiaMultiplier : 1.0f;

        if (parent >= 0)
        {
            const AuraJointDesc& joint = desc.parts[index].jointToParent;
            if (joint.type == AURA_JOINT_CONE || joint.type == AURA_JOINT_SWING_TWIST)
            {
                const float swing = joint.swingLimit > 0.0f ? joint.swingLimit : 0.0f;
                JPH::SwingTwistConstraintSettings constraint;
                constraint.mSpace = JPH::EConstraintSpace::WorldSpace;
                constraint.mPosition1 = ToRVec3(joint.anchorA);
                constraint.mPosition2 = ToRVec3(joint.anchorB);
                constraint.mTwistAxis1 = ToVec3(joint.axisA).NormalizedOr(JPH::Vec3::sAxisY());
                constraint.mTwistAxis2 = ToVec3(joint.axisB).NormalizedOr(JPH::Vec3::sAxisY());
                constraint.mPlaneAxis1 = ToVec3(joint.normalAxisA).NormalizedOr(JPH::Vec3::sAxisZ());
                constraint.mPlaneAxis2 = ToVec3(joint.normalAxisB).NormalizedOr(JPH::Vec3::sAxisZ());
                constraint.mSwingType = JPH::ESwingType::Cone;
                constraint.mNormalHalfConeAngle = swing;
                constraint.mPlaneHalfConeAngle = swing;
                constraint.mTwistMinAngle = joint.minLimit;
                constraint.mTwistMaxAngle = joint.maxLimit;
                part.mToParent = new JPH::SwingTwistConstraintSettings(constraint);
            }
            else
            {
                JPH::FixedConstraintSettings constraint;
                constraint.mAutoDetectPoint = false;
                constraint.mPoint1 = ToRVec3(joint.anchorA);
                constraint.mPoint2 = ToRVec3(joint.anchorB);
                part.mToParent = new JPH::FixedConstraintSettings(constraint);
            }
        }
    }

    settings->CalculateConstraintPriorities();
    settings->DisableParentChildCollisions();
    JPH::Ref<JPH::Ragdoll> ragdoll = settings->CreateRagdoll(
        static_cast<JPH::CollisionGroup::GroupID>(desc.collisionGroup), 0, &impl_->physics);
    if (ragdoll == nullptr)
        return AURA_OUT_OF_MEMORY;
    ragdoll->AddToPhysicsSystem(JPH::EActivation::Activate);

    int index;
    if (!impl_->freeRagdollSlots.empty())
    {
        index = impl_->freeRagdollSlots.back();
        impl_->freeRagdollSlots.pop_back();
    }
    else
    {
        index = static_cast<int>(impl_->ragdollSlots.size());
        impl_->ragdollSlots.emplace_back();
    }
    Impl::RagdollSlot& slot = impl_->ragdollSlots[index];
    slot.occupied = true;
    slot.ragdoll = std::move(ragdoll);
    *outRagdoll = Impl::MakeRagdollHandle(slot, index);
    return AURA_SUCCESS;
}

AuraResultCode JoltWorld::DestroyRagdoll(AuraRagdollHandle ragdoll)
{
    Impl::RagdollSlot* slot = impl_->FindRagdoll(ragdoll);
    if (slot == nullptr)
        return AURA_INVALID_HANDLE;
    slot->ragdoll->RemoveFromPhysicsSystem();
    slot->ragdoll = nullptr;
    slot->occupied = false;
    slot->generation += 1;
    impl_->freeRagdollSlots.push_back(static_cast<int>(ragdoll.opaque & 0xFFFFFFFFull));
    return AURA_SUCCESS;
}

AuraResultCode JoltWorld::GetRagdollPose(AuraRagdollHandle ragdoll, AuraPose* buffer, uint32_t capacity, uint32_t* outCount) const
{
    const Impl::RagdollSlot* slot = impl_->FindRagdoll(ragdoll);
    if (slot == nullptr || outCount == nullptr)
        return AURA_INVALID_HANDLE;
    const uint32_t count = static_cast<uint32_t>(slot->ragdoll->GetBodyCount());
    *outCount = count;
    if (buffer == nullptr || capacity < count)
        return AURA_CAPACITY_EXCEEDED;
    std::vector<JPH::Mat44> matrices(count);
    JPH::RVec3 root;
    const_cast<JPH::Ragdoll*>(slot->ragdoll.GetPtr())->GetPose(root, matrices.data());
    for (uint32_t index = 0; index < count; ++index)
    {
        buffer[index].position = ToAura(matrices[index].GetTranslation());
        buffer[index].rotation = ToAura(matrices[index].GetQuaternion());
    }
    return AURA_SUCCESS;
}

AuraResultCode JoltWorld::SetRagdollPose(AuraRagdollHandle ragdoll, const AuraPose* poses, uint32_t poseCount)
{
    Impl::RagdollSlot* slot = impl_->FindRagdoll(ragdoll);
    if (slot == nullptr)
        return AURA_INVALID_HANDLE;
    if (poses == nullptr || poseCount != slot->ragdoll->GetBodyCount())
        return AURA_INVALID_DEFINITION;
    std::vector<JPH::Mat44> matrices(poseCount);
    for (uint32_t index = 0; index < poseCount; ++index)
    {
        JPH::Quat rotation = ToQuat(poses[index].rotation);
        if (rotation.LengthSq() < 1.0e-8f)
            rotation = JPH::Quat::sIdentity();
        else
            rotation = rotation.Normalized();
        matrices[index] = JPH::Mat44::sRotationTranslation(rotation, ToRVec3(poses[index].position));
    }
    slot->ragdoll->SetPose(JPH::RVec3::sZero(), matrices.data());
    slot->ragdoll->ResetWarmStart();
    return AURA_SUCCESS;
}

} // namespace aura
