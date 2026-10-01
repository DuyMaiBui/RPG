#include "aura_jolt_internal.h"

#include <Jolt/Physics/Constraints/ConeConstraint.h>
#include <Jolt/Physics/Constraints/DistanceConstraint.h>
#include <Jolt/Physics/Constraints/FixedConstraint.h>
#include <Jolt/Physics/Constraints/HingeConstraint.h>
#include <Jolt/Physics/Constraints/MotorSettings.h>
#include <Jolt/Physics/Constraints/PointConstraint.h>
#include <Jolt/Physics/Constraints/PulleyConstraint.h>
#include <Jolt/Physics/Constraints/SliderConstraint.h>
#include <Jolt/Physics/Constraints/SwingTwistConstraint.h>

namespace aura
{

/* Jolt constraint construction. A new joint type is one case here. */
AuraResultCode JoltWorld::CreateJoint(const AuraJointDesc& desc, uint64_t* outJoint)
{
    if (outJoint == nullptr)
        return AURA_INVALID_HANDLE;

    Impl::Slot* slotA = impl_->Find(desc.bodyA);
    Impl::Slot* slotB = impl_->Find(desc.bodyB);
    if (slotA == nullptr || slotB == nullptr || slotA->body == nullptr || slotB->body == nullptr)
        return AURA_INVALID_HANDLE;
    if (slotA == slotB)
        return AURA_INVALID_DEFINITION;

    JPH::Body& bodyA = *slotA->body;
    JPH::Body& bodyB = *slotB->body;
    const JPH::RVec3 point1 = ToRVec3(desc.anchorA);
    const JPH::RVec3 point2 = ToRVec3(desc.anchorB);

    auto axis = [](const AuraVec3& value) -> JPH::Vec3
    {
        const JPH::Vec3 v(value.x, value.y, value.z);
        return v.LengthSq() > 1.0e-8f ? v.Normalized() : JPH::Vec3::sAxisY();
    };

    JPH::TwoBodyConstraint* constraint = nullptr;
    switch (static_cast<AuraJointType>(desc.type))
    {
    case AURA_JOINT_FIXED:
    {
        JPH::FixedConstraintSettings settings;
        settings.mAutoDetectPoint = false;
        settings.mPoint1 = point1;
        settings.mPoint2 = point2;
        constraint = settings.Create(bodyA, bodyB);
        break;
    }
    case AURA_JOINT_POINT:
    {
        JPH::PointConstraintSettings settings;
        settings.mPoint1 = point1;
        settings.mPoint2 = point2;
        constraint = settings.Create(bodyA, bodyB);
        break;
    }
    case AURA_JOINT_DISTANCE:
    {
        JPH::DistanceConstraintSettings settings;
        settings.mPoint1 = point1;
        settings.mPoint2 = point2;
        settings.mMinDistance = desc.distance;
        settings.mMaxDistance = desc.distance;
        constraint = settings.Create(bodyA, bodyB);
        break;
    }
    case AURA_JOINT_SPRING:
    {
        JPH::DistanceConstraintSettings settings;
        settings.mPoint1 = point1;
        settings.mPoint2 = point2;
        settings.mMinDistance = desc.distance;
        settings.mMaxDistance = desc.distance;
        settings.mLimitsSpringSettings.mMode = JPH::ESpringMode::FrequencyAndDamping;
        settings.mLimitsSpringSettings.mFrequency = desc.springFrequency > 0.0f ? desc.springFrequency : 1.0f;
        settings.mLimitsSpringSettings.mDamping = desc.springDamping > 0.0f ? desc.springDamping : 1.0f;
        constraint = settings.Create(bodyA, bodyB);
        break;
    }
    case AURA_JOINT_HINGE:
    {
        JPH::HingeConstraintSettings settings;
        settings.mPoint1 = point1;
        settings.mPoint2 = point2;
        settings.mHingeAxis1 = axis(desc.axisA);
        settings.mHingeAxis2 = axis(desc.axisB);
        settings.mNormalAxis1 = axis(desc.normalAxisA);
        settings.mNormalAxis2 = axis(desc.normalAxisB);
        if (desc.enableLimit != 0)
        {
            settings.mLimitsMin = desc.minLimit;
            settings.mLimitsMax = desc.maxLimit;
        }
        if (desc.motorEnabled != 0)
            settings.mMotorSettings.SetTorqueLimit(desc.maxMotorForce);
        auto* hinge = static_cast<JPH::HingeConstraint*>(settings.Create(bodyA, bodyB));
        if (hinge != nullptr && desc.motorEnabled != 0)
        {
            hinge->SetMotorState(JPH::EMotorState::Velocity);
            hinge->SetTargetAngularVelocity(desc.motorTargetVelocity);
        }
        constraint = hinge;
        break;
    }
    case AURA_JOINT_SLIDER:
    {
        JPH::SliderConstraintSettings settings;
        settings.mAutoDetectPoint = false;
        settings.mPoint1 = point1;
        settings.mPoint2 = point2;
        settings.mSliderAxis1 = axis(desc.axisA);
        settings.mSliderAxis2 = axis(desc.axisB);
        settings.mNormalAxis1 = axis(desc.normalAxisA);
        settings.mNormalAxis2 = axis(desc.normalAxisB);
        if (desc.enableLimit != 0)
        {
            settings.mLimitsMin = desc.minLimit;
            settings.mLimitsMax = desc.maxLimit;
        }
        if (desc.motorEnabled != 0)
            settings.mMotorSettings.SetForceLimit(desc.maxMotorForce);
        auto* slider = static_cast<JPH::SliderConstraint*>(settings.Create(bodyA, bodyB));
        if (slider != nullptr && desc.motorEnabled != 0)
        {
            slider->SetMotorState(JPH::EMotorState::Velocity);
            slider->SetTargetVelocity(desc.motorTargetVelocity);
        }
        constraint = slider;
        break;
    }
    case AURA_JOINT_CONE:
    {
        JPH::ConeConstraintSettings settings;
        settings.mPoint1 = point1;
        settings.mPoint2 = point2;
        settings.mTwistAxis1 = axis(desc.axisA);
        settings.mTwistAxis2 = axis(desc.axisB);
        settings.mHalfConeAngle = desc.maxLimit > 0.0f ? desc.maxLimit : desc.swingLimit;
        constraint = settings.Create(bodyA, bodyB);
        break;
    }
    case AURA_JOINT_SWING_TWIST:
    {
        JPH::SwingTwistConstraintSettings settings;
        settings.mPosition1 = point1;
        settings.mPosition2 = point2;
        settings.mTwistAxis1 = axis(desc.axisA);
        settings.mTwistAxis2 = axis(desc.axisB);
        settings.mPlaneAxis1 = axis(desc.normalAxisA);
        settings.mPlaneAxis2 = axis(desc.normalAxisB);
        settings.mSwingType = JPH::ESwingType::Cone;
        settings.mNormalHalfConeAngle = desc.swingLimit;
        settings.mPlaneHalfConeAngle = desc.swingLimit;
        settings.mTwistMinAngle = desc.minLimit;
        settings.mTwistMaxAngle = desc.maxLimit;
        constraint = settings.Create(bodyA, bodyB);
        break;
    }
    case AURA_JOINT_PULLEY:
    {
        JPH::PulleyConstraintSettings settings;
        settings.mBodyPoint1 = point1;
        settings.mBodyPoint2 = point2;
        settings.mFixedPoint1 = ToRVec3(desc.fixedPoint);
        settings.mFixedPoint2 = ToRVec3(desc.fixedPoint);
        settings.mRatio = 1.0f;
        settings.mMinLength = desc.distance;
        settings.mMaxLength = desc.distance;
        constraint = settings.Create(bodyA, bodyB);
        break;
    }
    default:
        return AURA_UNSUPPORTED_QUERY;
    }

    if (constraint == nullptr)
        return AURA_OUT_OF_MEMORY;

    impl_->physics.AddConstraint(constraint);

    int index;
    if (!impl_->freeJointSlots.empty())
    {
        index = impl_->freeJointSlots.back();
        impl_->freeJointSlots.pop_back();
    }
    else
    {
        index = static_cast<int>(impl_->jointSlots.size());
        impl_->jointSlots.emplace_back();
    }

    Impl::JointSlot& slot = impl_->jointSlots[index];
    slot.occupied = true;
    slot.constraint = constraint;
    slot.bodyA = desc.bodyA;
    slot.bodyB = desc.bodyB;
    *outJoint = Impl::MakeJointHandle(slot, index);
    return AURA_SUCCESS;
}

AuraResultCode JoltWorld::DestroyJoint(uint64_t joint)
{
    Impl::JointSlot* slot = impl_->FindJoint(joint);
    if (slot == nullptr)
        return AURA_INVALID_HANDLE;

    if (slot->constraint != nullptr)
    {
        impl_->physics.RemoveConstraint(slot->constraint);
        slot->constraint->Release();
        slot->constraint = nullptr;
    }

    slot->occupied = false;
    slot->generation += 1;
    impl_->freeJointSlots.push_back(static_cast<int>(joint & 0xFFFFFFFFull));
    return AURA_SUCCESS;
}

bool JoltWorld::HasJoint(uint64_t joint) const
{
    return impl_->FindJoint(joint) != nullptr;
}

} // namespace aura
