#include "aura_jolt_internal.h"

#include <Jolt/Physics/Constraints/DistanceConstraint.h>
#include <Jolt/Physics/Constraints/FixedConstraint.h>
#include <Jolt/Physics/Constraints/HingeConstraint.h>
#include <Jolt/Physics/Constraints/MotorSettings.h>
#include <Jolt/Physics/Constraints/PointConstraint.h>
#include <Jolt/Physics/Constraints/SliderConstraint.h>

#include <cfloat>
#include <cmath>

namespace aura
{
namespace
{
constexpr float kPi = 3.14159265358979323846f;

bool IsFinite(float value) { return std::isfinite(value); }

float Length2(float a, float b) { return std::sqrt(a * a + b * b); }
float Length3(float a, float b, float c) { return std::sqrt(a * a + b * b + c * c); }

JPH::HingeConstraint* AsHinge(JPH::TwoBodyConstraint* c) { return static_cast<JPH::HingeConstraint*>(c); }
JPH::SliderConstraint* AsSlider(JPH::TwoBodyConstraint* c) { return static_cast<JPH::SliderConstraint*>(c); }
const JPH::HingeConstraint* AsHinge(const JPH::TwoBodyConstraint* c) { return static_cast<const JPH::HingeConstraint*>(c); }
const JPH::SliderConstraint* AsSlider(const JPH::TwoBodyConstraint* c) { return static_cast<const JPH::SliderConstraint*>(c); }

int32_t ToMode(JPH::EMotorState state)
{
    switch (state)
    {
    case JPH::EMotorState::Velocity: return AURA_JOINT_MOTOR_VELOCITY;
    case JPH::EMotorState::Position: return AURA_JOINT_MOTOR_POSITION;
    default: return AURA_JOINT_MOTOR_OFF;
    }
}
} // namespace

void JoltWorld::Impl::RefreshJointsFor(AuraBodyHandle handle)
{
    for (JointSlot& joint : jointSlots)
    {
        if (!joint.occupied || joint.constraint == nullptr)
            continue;
        const bool touchesA = joint.bodyA.index == handle.index && joint.bodyA.generation == handle.generation;
        const bool touchesB = joint.bodyB.index == handle.index && joint.bodyB.generation == handle.generation;
        if (!touchesA && !touchesB)
            continue;

        /* A constraint must not keep solving against a body that left the simulation. */
        const Slot* a = Find(joint.bodyA);
        const Slot* b = Find(joint.bodyB);
        const bool enable = a != nullptr && b != nullptr && a->enabled && b->enabled;
        if (joint.constraint->GetEnabled() != enable)
        {
            joint.constraint->SetEnabled(enable);
            if (enable)
            {
                physics.GetBodyInterface().ActivateBody(a->id);
                physics.GetBodyInterface().ActivateBody(b->id);
            }
        }
    }
}

bool JoltWorld::Impl::JointLoads(const JointSlot& joint, float& force, float& torque, float& motorLoad) const
{
    force = 0.0f;
    torque = 0.0f;
    motorLoad = 0.0f;
    if (joint.constraint == nullptr || lastDelta <= 0.0f)
        return false;

    /* Sleeping joints keep the lambdas of the step they fell asleep in, which is the load they still carry. */
    const Slot* a = Find(joint.bodyA);
    const Slot* b = Find(joint.bodyB);
    if (a == nullptr || b == nullptr)
        return false;

    const float inv = 1.0f / lastDelta;
    switch (joint.type)
    {
    case AURA_JOINT_FIXED:
    {
        const auto* c = static_cast<const JPH::FixedConstraint*>(joint.constraint);
        force = c->GetTotalLambdaPosition().Length() * inv;
        torque = c->GetTotalLambdaRotation().Length() * inv;
        return true;
    }
    case AURA_JOINT_POINT:
        force = static_cast<const JPH::PointConstraint*>(joint.constraint)->GetTotalLambdaPosition().Length() * inv;
        return true;
    case AURA_JOINT_DISTANCE:
    case AURA_JOINT_SPRING:
        force = std::abs(static_cast<const JPH::DistanceConstraint*>(joint.constraint)->GetTotalLambdaPosition()) * inv;
        return true;
    case AURA_JOINT_HINGE:
    {
        const JPH::HingeConstraint* c = AsHinge(joint.constraint);
        const JPH::Vector<2> rotation = c->GetTotalLambdaRotation();
        force = c->GetTotalLambdaPosition().Length() * inv;
        torque = Length3(rotation[0], rotation[1], c->GetTotalLambdaRotationLimits()) * inv;
        motorLoad = std::abs(c->GetTotalLambdaMotor()) * inv;
        return true;
    }
    case AURA_JOINT_SLIDER:
    {
        const JPH::SliderConstraint* c = AsSlider(joint.constraint);
        const JPH::Vector<2> position = c->GetTotalLambdaPosition();
        force = Length3(position[0], position[1], c->GetTotalLambdaPositionLimits()) * inv;
        torque = c->GetTotalLambdaRotation().Length() * inv;
        motorLoad = std::abs(c->GetTotalLambdaMotor()) * inv;
        return true;
    }
    default:
        return false;
    }
}

void JoltWorld::Impl::ProcessJointBreaks()
{
    for (size_t index = 0; index < jointSlots.size(); ++index)
    {
        JointSlot& joint = jointSlots[index];
        if (!joint.occupied || joint.broken || joint.constraint == nullptr)
            continue;
        if (joint.breakForce <= 0.0f && joint.breakTorque <= 0.0f)
            continue;

        float force, torque, motor;
        JointLoads(joint, force, torque, motor);
        const bool overForce = joint.breakForce > 0.0f && force > joint.breakForce;
        const bool overTorque = joint.breakTorque > 0.0f && torque > joint.breakTorque;
        if (!overForce && !overTorque)
            continue;

        joint.lastForce = force;
        joint.lastTorque = torque;
        joint.broken = true;
        /* The constraint manager owns the only reference, so RemoveConstraint frees it. */
        physics.RemoveConstraint(joint.constraint);
        joint.constraint = nullptr;

        JPH::BodyInterface& bi = physics.GetBodyInterface();
        const Slot* a = Find(joint.bodyA);
        const Slot* b = Find(joint.bodyB);
        if (a != nullptr && a->enabled)
            bi.ActivateBody(a->id);
        if (b != nullptr && b->enabled)
            bi.ActivateBody(b->id);
    }
}

AuraResultCode JoltWorld::SetJointMotor(uint64_t joint, const AuraJointMotorDesc& motor)
{
    Impl::JointSlot* slot = impl_->FindJoint(joint);
    if (slot == nullptr || slot->broken || slot->constraint == nullptr)
        return AURA_INVALID_HANDLE;
    if (slot->type != AURA_JOINT_HINGE && slot->type != AURA_JOINT_SLIDER)
        return AURA_UNSUPPORTED_OPERATION;
    if (motor.mode < AURA_JOINT_MOTOR_OFF || motor.mode > AURA_JOINT_MOTOR_POSITION
        || !IsFinite(motor.target) || !IsFinite(motor.maxForce) || !IsFinite(motor.springFrequency)
        || !IsFinite(motor.springDamping) || motor.springFrequency < 0.0f || motor.springDamping < 0.0f)
        return AURA_INVALID_DEFINITION;
    if (motor.mode != AURA_JOINT_MOTOR_OFF && motor.maxForce <= 0.0f)
        return AURA_INVALID_DEFINITION;

    const bool hinge = slot->type == AURA_JOINT_HINGE;
    JPH::MotorSettings& settings = hinge ? AsHinge(slot->constraint)->GetMotorSettings() : AsSlider(slot->constraint)->GetMotorSettings();
    JPH::EMotorState state = JPH::EMotorState::Off;
    if (motor.mode != AURA_JOINT_MOTOR_OFF)
    {
        if (hinge)
            settings.SetTorqueLimit(motor.maxForce);
        else
            settings.SetForceLimit(motor.maxForce);
        if (motor.springFrequency > 0.0f)
            settings.mSpringSettings.mFrequency = motor.springFrequency;
        if (motor.springDamping > 0.0f)
            settings.mSpringSettings.mDamping = motor.springDamping;
        state = motor.mode == AURA_JOINT_MOTOR_VELOCITY ? JPH::EMotorState::Velocity : JPH::EMotorState::Position;
    }

    if (hinge)
    {
        JPH::HingeConstraint* c = AsHinge(slot->constraint);
        c->SetMotorState(state);
        if (motor.mode == AURA_JOINT_MOTOR_VELOCITY)
            c->SetTargetAngularVelocity(motor.target);
        else if (motor.mode == AURA_JOINT_MOTOR_POSITION)
            c->SetTargetAngle(motor.target);
    }
    else
    {
        JPH::SliderConstraint* c = AsSlider(slot->constraint);
        c->SetMotorState(state);
        if (motor.mode == AURA_JOINT_MOTOR_VELOCITY)
            c->SetTargetVelocity(motor.target);
        else if (motor.mode == AURA_JOINT_MOTOR_POSITION)
            c->SetTargetPosition(motor.target);
    }

    JPH::BodyInterface& bi = impl_->physics.GetBodyInterface();
    const Impl::Slot* a = impl_->Find(slot->bodyA);
    const Impl::Slot* b = impl_->Find(slot->bodyB);
    if (a != nullptr && a->enabled)
        bi.ActivateBody(a->id);
    if (b != nullptr && b->enabled)
        bi.ActivateBody(b->id);
    return AURA_SUCCESS;
}

AuraResultCode JoltWorld::SetJointLimits(uint64_t joint, bool enabled, float minLimit, float maxLimit)
{
    Impl::JointSlot* slot = impl_->FindJoint(joint);
    if (slot == nullptr || slot->broken || slot->constraint == nullptr)
        return AURA_INVALID_HANDLE;
    if (slot->type != AURA_JOINT_HINGE && slot->type != AURA_JOINT_SLIDER)
        return AURA_UNSUPPORTED_OPERATION;

    const bool hinge = slot->type == AURA_JOINT_HINGE;
    if (enabled)
    {
        if (!IsFinite(minLimit) || !IsFinite(maxLimit) || minLimit > 0.0f || maxLimit < 0.0f)
            return AURA_INVALID_DEFINITION;
        if (hinge && (minLimit < -kPi || maxLimit > kPi))
            return AURA_INVALID_DEFINITION;
    }

    if (hinge)
    {
        if (enabled)
            AsHinge(slot->constraint)->SetLimits(minLimit, maxLimit);
        else
            AsHinge(slot->constraint)->SetLimits(-kPi, kPi);
    }
    else
    {
        if (enabled)
            AsSlider(slot->constraint)->SetLimits(minLimit, maxLimit);
        else
            AsSlider(slot->constraint)->SetLimits(-FLT_MAX, FLT_MAX);
    }

    /* A resting body would otherwise sleep through the changed limits. */
    JPH::BodyInterface& bi = impl_->physics.GetBodyInterface();
    const Impl::Slot* a = impl_->Find(slot->bodyA);
    const Impl::Slot* b = impl_->Find(slot->bodyB);
    if (a != nullptr && a->enabled)
        bi.ActivateBody(a->id);
    if (b != nullptr && b->enabled)
        bi.ActivateBody(b->id);
    return AURA_SUCCESS;
}

AuraResultCode JoltWorld::SetJointBreakThreshold(uint64_t joint, float maxForce, float maxTorque)
{
    Impl::JointSlot* slot = impl_->FindJoint(joint);
    if (slot == nullptr || slot->broken || slot->constraint == nullptr)
        return AURA_INVALID_HANDLE;

    bool hasTorque = false;
    switch (slot->type)
    {
    case AURA_JOINT_FIXED:
    case AURA_JOINT_HINGE:
    case AURA_JOINT_SLIDER:
        hasTorque = true;
        break;
    case AURA_JOINT_POINT:
    case AURA_JOINT_DISTANCE:
    case AURA_JOINT_SPRING:
        break;
    default:
        return AURA_UNSUPPORTED_OPERATION;
    }

    if (!IsFinite(maxForce) || !IsFinite(maxTorque) || maxForce < 0.0f || maxTorque < 0.0f)
        return AURA_INVALID_DEFINITION;
    if (!hasTorque && maxTorque > 0.0f)
        return AURA_INVALID_DEFINITION;

    slot->breakForce = maxForce;
    slot->breakTorque = maxTorque;
    return AURA_SUCCESS;
}

AuraResultCode JoltWorld::IsJointBroken(uint64_t joint, bool* outBroken) const
{
    const Impl::JointSlot* slot = impl_->FindJoint(joint);
    if (slot == nullptr || outBroken == nullptr)
        return AURA_INVALID_HANDLE;
    *outBroken = slot->broken;
    return AURA_SUCCESS;
}

AuraResultCode JoltWorld::GetJointFeedback(uint64_t joint, AuraJointFeedback* outFeedback) const
{
    const Impl::JointSlot* slot = impl_->FindJoint(joint);
    if (slot == nullptr || outFeedback == nullptr)
        return AURA_INVALID_HANDLE;

    *outFeedback = AuraJointFeedback{};
    outFeedback->isBroken = slot->broken ? 1 : 0;
    if (slot->broken)
    {
        outFeedback->force = slot->lastForce;
        outFeedback->torque = slot->lastTorque;
        return AURA_SUCCESS;
    }

    impl_->JointLoads(*slot, outFeedback->force, outFeedback->torque, outFeedback->motorLoad);
    if (slot->type == AURA_JOINT_HINGE)
    {
        const JPH::HingeConstraint* c = AsHinge(slot->constraint);
        outFeedback->position = c->GetCurrentAngle();
        outFeedback->motorMode = ToMode(c->GetMotorState());
    }
    else if (slot->type == AURA_JOINT_SLIDER)
    {
        const JPH::SliderConstraint* c = AsSlider(slot->constraint);
        outFeedback->position = c->GetCurrentPosition();
        outFeedback->motorMode = ToMode(c->GetMotorState());
    }
    return AURA_SUCCESS;
}

} // namespace aura
