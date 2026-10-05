#include "aura_jolt_internal.h"

#include <Jolt/Physics/Constraints/ConeConstraint.h>
#include <Jolt/Physics/Constraints/DistanceConstraint.h>
#include <Jolt/Physics/Constraints/FixedConstraint.h>
#include <Jolt/Physics/Constraints/GearConstraint.h>
#include <Jolt/Physics/Constraints/HingeConstraint.h>
#include <Jolt/Physics/Constraints/MotorSettings.h>
#include <Jolt/Physics/Constraints/PointConstraint.h>
#include <Jolt/Physics/Constraints/PulleyConstraint.h>
#include <Jolt/Physics/Constraints/RackAndPinionConstraint.h>
#include <Jolt/Physics/Constraints/SixDOFConstraint.h>
#include <Jolt/Physics/Constraints/SliderConstraint.h>
#include <Jolt/Physics/Constraints/SwingTwistConstraint.h>

#include <cfloat>
#include <cmath>

namespace aura
{
namespace
{
constexpr float kPi = 3.14159265358979323846f;

/* The C ABI struct and the managed NativeJointDesc mirror must stay in lockstep with this size. */
static_assert(sizeof(AuraJointAxisLimit) == 16, "AuraJointAxisLimit layout");
static_assert(sizeof(AuraJointDesc) == 280, "AuraJointDesc layout");

bool IsFinite(float value) { return std::isfinite(value); }
bool InRange(float value, float lo, float hi) { return IsFinite(value) && value >= lo && value <= hi; }

bool Touches(const AuraBodyHandle& a, const AuraBodyHandle& b, const AuraBodyHandle& body)
{
    return (a.index == body.index && a.generation == body.generation) || (b.index == body.index && b.generation == body.generation);
}

uint64_t RefHandle(const AuraJointRef& ref)
{
    return (static_cast<uint64_t>(ref.generation) << 32) | ref.index;
}

/* Orthonormal constraint frame (X, Y) from two arbitrary axes. */
void MakeFrame(const AuraVec3& xa, const AuraVec3& ya, JPH::Vec3& x, JPH::Vec3& y)
{
    const JPH::Vec3 rawX(xa.x, xa.y, xa.z);
    x = rawX.LengthSq() > 1.0e-8f ? rawX.Normalized() : JPH::Vec3::sAxisX();
    JPH::Vec3 rawY(ya.x, ya.y, ya.z);
    rawY -= x * x.Dot(rawY);
    y = rawY.LengthSq() > 1.0e-8f ? rawY.Normalized() : x.GetNormalizedPerpendicular();
}

bool ValidAxisLimit(uint32_t axis, const AuraJointAxisLimit& limit, bool pyramid)
{
    if (axis > 5 || limit.mode > AURA_JOINT_AXIS_LIMITED || !IsFinite(limit.maxFriction) || limit.maxFriction < 0.0f)
        return false;
    if (limit.mode != AURA_JOINT_AXIS_LIMITED)
        return true;
    if (!IsFinite(limit.minLimit) || !IsFinite(limit.maxLimit) || limit.minLimit > limit.maxLimit)
        return false;
    if (axis == 3)
        return limit.minLimit >= -kPi && limit.maxLimit <= kPi;
    if (axis >= 4)
        return pyramid ? (limit.minLimit >= -kPi && limit.maxLimit <= kPi) : InRange(limit.maxLimit, 0.0f, kPi);
    return true;
}

void ApplyAxisLimit(JPH::SixDOFConstraintSettings& settings, uint32_t axis, const AuraJointAxisLimit& limit, bool pyramid)
{
    const auto a = static_cast<JPH::SixDOFConstraintSettings::EAxis>(axis);
    switch (limit.mode)
    {
    case AURA_JOINT_AXIS_FREE:
        settings.MakeFreeAxis(a);
        break;
    case AURA_JOINT_AXIS_LIMITED:
        if (axis >= 4 && !pyramid)
            settings.SetLimitedAxis(a, -limit.maxLimit, limit.maxLimit);
        else
            settings.SetLimitedAxis(a, limit.minLimit, limit.maxLimit);
        break;
    default:
        settings.MakeFixedAxis(a);
        break;
    }
    settings.mMaxFriction[axis] = limit.maxFriction;
}
} // namespace

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
    /* A constraint on a body that is not in the simulation corrupts Jolt's broad phase on the next step. */
    if (!slotA->enabled || !slotB->enabled)
        return AURA_BODY_DISABLED;
    /* A constraint between two bodies that cannot move has no effective mass and is invalid for the solver. */
    if (slotA->body->GetMotionType() != JPH::EMotionType::Dynamic && slotB->body->GetMotionType() != JPH::EMotionType::Dynamic)
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
    uint64_t refHandleA = 0;
    uint64_t refHandleB = 0;
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
        const float halfAngle = desc.maxLimit > 0.0f ? desc.maxLimit : desc.swingLimit;
        if (!InRange(halfAngle, 0.0f, kPi))
            return AURA_INVALID_DEFINITION;
        JPH::ConeConstraintSettings settings;
        settings.mPoint1 = point1;
        settings.mPoint2 = point2;
        settings.mTwistAxis1 = axis(desc.axisA);
        settings.mTwistAxis2 = axis(desc.axisB);
        settings.mHalfConeAngle = halfAngle;
        constraint = settings.Create(bodyA, bodyB);
        break;
    }
    case AURA_JOINT_SWING_TWIST:
    {
        const float planeLimit = desc.planeSwingLimit > 0.0f ? desc.planeSwingLimit : desc.swingLimit;
        if (!InRange(desc.swingLimit, 0.0f, kPi) || !InRange(planeLimit, 0.0f, kPi)
            || !InRange(desc.minLimit, -kPi, kPi) || !InRange(desc.maxLimit, -kPi, kPi) || desc.minLimit > desc.maxLimit
            || !IsFinite(desc.maxFriction) || desc.maxFriction < 0.0f)
            return AURA_INVALID_DEFINITION;
        JPH::SwingTwistConstraintSettings settings;
        settings.mPosition1 = point1;
        settings.mPosition2 = point2;
        settings.mTwistAxis1 = axis(desc.axisA);
        settings.mTwistAxis2 = axis(desc.axisB);
        settings.mPlaneAxis1 = axis(desc.normalAxisA);
        settings.mPlaneAxis2 = axis(desc.normalAxisB);
        settings.mSwingType = desc.pyramidSwing != 0 ? JPH::ESwingType::Pyramid : JPH::ESwingType::Cone;
        settings.mNormalHalfConeAngle = desc.swingLimit;
        settings.mPlaneHalfConeAngle = planeLimit;
        settings.mTwistMinAngle = desc.minLimit;
        settings.mTwistMaxAngle = desc.maxLimit;
        settings.mMaxFrictionTorque = desc.maxFriction;
        constraint = settings.Create(bodyA, bodyB);
        break;
    }
    case AURA_JOINT_PULLEY:
    {
        const float ratio = desc.ratio == 0.0f ? 1.0f : desc.ratio;
        float minLength = desc.distance;
        float maxLength = desc.distance;
        if (desc.enableLimit != 0)
        {
            minLength = desc.minLimit;
            maxLength = desc.maxLimit;
        }
        else if (desc.distance == 0.0f)
        {
            minLength = -1.0f; /* Jolt keeps the creation length. */
            maxLength = -1.0f;
        }
        const bool keepCurrent = desc.enableLimit == 0 && desc.distance == 0.0f;
        if (!InRange(ratio, 1.0e-4f, 1.0e4f) || (!keepCurrent && (!IsFinite(minLength) || !IsFinite(maxLength) || minLength < 0.0f || minLength > maxLength)))
            return AURA_INVALID_DEFINITION;
        JPH::PulleyConstraintSettings settings;
        settings.mBodyPoint1 = point1;
        settings.mBodyPoint2 = point2;
        settings.mFixedPoint1 = ToRVec3(desc.fixedPoint);
        settings.mFixedPoint2 = ToRVec3(desc.fixedPointB);
        settings.mRatio = ratio;
        settings.mMinLength = minLength;
        settings.mMaxLength = maxLength;
        constraint = settings.Create(bodyA, bodyB);
        break;
    }
    case AURA_JOINT_SIX_DOF:
    {
        const bool pyramid = desc.pyramidSwing != 0;
        for (uint32_t i = 0; i < 6; ++i)
            if (!ValidAxisLimit(i, desc.axes[i], pyramid))
                return AURA_INVALID_DEFINITION;
        JPH::SixDOFConstraintSettings settings;
        settings.mPosition1 = point1;
        settings.mPosition2 = point2;
        MakeFrame(desc.axisA, desc.normalAxisA, settings.mAxisX1, settings.mAxisY1);
        MakeFrame(desc.axisB, desc.normalAxisB, settings.mAxisX2, settings.mAxisY2);
        settings.mSwingType = pyramid ? JPH::ESwingType::Pyramid : JPH::ESwingType::Cone;
        for (uint32_t i = 0; i < 6; ++i)
            ApplyAxisLimit(settings, i, desc.axes[i], pyramid);
        constraint = settings.Create(bodyA, bodyB);
        break;
    }
    case AURA_JOINT_GEAR:
    case AURA_JOINT_RACK_AND_PINION:
    {
        const bool gear = desc.type == AURA_JOINT_GEAR;
        refHandleA = RefHandle(desc.jointRefA);
        refHandleB = RefHandle(desc.jointRefB);
        Impl::JointSlot* first = impl_->FindJoint(refHandleA);
        Impl::JointSlot* second = impl_->FindJoint(refHandleB);
        /* Both referenced joints must be live hinges (or hinge + slider) on the bodies this constraint couples. */
        if (first == nullptr || second == nullptr || first == second || first->broken || second->broken
            || first->constraint == nullptr || second->constraint == nullptr
            || first->type != AURA_JOINT_HINGE || second->type != (gear ? AURA_JOINT_HINGE : AURA_JOINT_SLIDER)
            || !Touches(first->bodyA, first->bodyB, desc.bodyA) || !Touches(second->bodyA, second->bodyB, desc.bodyB)
            || !InRange(desc.ratio, 1.0e-4f, 1.0e4f))
            return AURA_INVALID_DEFINITION;
        if (gear)
        {
            JPH::GearConstraintSettings settings;
            settings.mHingeAxis1 = axis(desc.axisA);
            settings.mHingeAxis2 = axis(desc.axisB);
            settings.mRatio = desc.ratio;
            auto* c = static_cast<JPH::GearConstraint*>(settings.Create(bodyA, bodyB));
            if (c != nullptr)
                c->SetConstraints(first->constraint, second->constraint);
            constraint = c;
        }
        else
        {
            JPH::RackAndPinionConstraintSettings settings;
            settings.mHingeAxis = axis(desc.axisA);
            settings.mSliderAxis = axis(desc.axisB);
            settings.mRatio = desc.ratio;
            auto* c = static_cast<JPH::RackAndPinionConstraint*>(settings.Create(bodyA, bodyB));
            if (c != nullptr)
                c->SetConstraints(first->constraint, second->constraint);
            constraint = c;
        }
        break;
    }
    case AURA_JOINT_PATH:
        return AURA_UNSUPPORTED_OPERATION;
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
    slot.type = static_cast<AuraJointType>(desc.type);
    slot.breakForce = 0.0f;
    slot.breakTorque = 0.0f;
    slot.broken = false;
    slot.lastForce = 0.0f;
    slot.lastTorque = 0.0f;
    slot.refA = refHandleA;
    slot.refB = refHandleB;
    *outJoint = Impl::MakeJointHandle(slot, index);
    return AURA_SUCCESS;
}

AuraResultCode JoltWorld::DestroyJoint(uint64_t joint)
{
    Impl::JointSlot* slot = impl_->FindJoint(joint);
    if (slot == nullptr)
        return AURA_INVALID_HANDLE;

    impl_->RemoveJointConstraint(static_cast<size_t>(joint & 0xFFFFFFFFull));

    slot->occupied = false;
    slot->generation += 1;
    impl_->freeJointSlots.push_back(static_cast<int>(joint & 0xFFFFFFFFull));
    return AURA_SUCCESS;
}

bool JoltWorld::HasJoint(uint64_t joint) const
{
    const Impl::JointSlot* slot = impl_->FindJoint(joint);
    return slot != nullptr && !slot->broken;
}

} // namespace aura

namespace aura
{

void JoltWorld::Impl::RemoveJointConstraint(size_t index)
{
    const JointSlot& joint = jointSlots[index];
    const uint64_t handle = MakeJointHandle(joint, static_cast<int>(index));

    /* A gear or rack-and-pinion reads its two joints every step, so it goes first. */
    for (size_t other = 0; other < jointSlots.size(); ++other)
    {
        JointSlot& dependent = jointSlots[other];
        if (other == index || !dependent.occupied || dependent.constraint == nullptr)
            continue;
        if (dependent.refA != handle && dependent.refB != handle)
            continue;
        physics.RemoveConstraint(dependent.constraint);
        dependent.constraint = nullptr;
        dependent.broken = true;
    }

    if (jointSlots[index].constraint != nullptr)
    {
        /* The constraint manager owns the only reference (a dependent's reference was released above). */
        physics.RemoveConstraint(jointSlots[index].constraint);
        jointSlots[index].constraint = nullptr;
    }
}

} // namespace aura
