#include "aura_jolt_vehicle.h"

#include <Jolt/Physics/Vehicle/VehicleCollisionTester.h>
#include <Jolt/Physics/Vehicle/WheeledVehicleController.h>

#include <algorithm>
#include <cmath>

namespace aura::jolt_vehicle
{
namespace
{

bool IsFinite(const JPH::Vec3& value)
{
    return std::isfinite(value.GetX()) && std::isfinite(value.GetY()) && std::isfinite(value.GetZ());
}

bool IsPositiveFinite(float value)
{
    return std::isfinite(value) && value > 0.0f;
}

void SetError(std::string* error, const char* message)
{
    if (error != nullptr)
        *error = message;
}

} // namespace

std::unique_ptr<Vehicle> Vehicle::Create(
    JPH::PhysicsSystem& physics,
    JPH::Body& chassis,
    const VehicleConfig& config,
    std::string* error)
{
    if (chassis.GetMotionType() != JPH::EMotionType::Dynamic)
    {
        SetError(error, "Jolt vehicle chassis must be a dynamic body");
        return nullptr;
    }
    if (!IsFinite(config.up) || !IsFinite(config.forward) ||
        config.up.LengthSq() < 0.99f || config.forward.LengthSq() < 0.99f ||
        !IsPositiveFinite(config.wheelRadius) || !IsPositiveFinite(config.wheelWidth) ||
        !std::isfinite(config.suspensionMinLength) || !std::isfinite(config.suspensionMaxLength) ||
        config.suspensionMinLength < 0.0f || config.suspensionMaxLength <= config.suspensionMinLength ||
        !IsPositiveFinite(config.suspensionFrequency) || !IsPositiveFinite(config.suspensionDamping) ||
        !std::isfinite(config.maxSteerAngle) || config.maxSteerAngle < 0.0f ||
        !std::isfinite(config.maxPitchRollAngle) || config.maxPitchRollAngle <= 0.0f ||
        !std::isfinite(config.maxEngineTorque) || config.maxEngineTorque < 0.0f)
    {
        SetError(error, "invalid Jolt vehicle configuration");
        return nullptr;
    }
    for (const JPH::Vec3& position : config.wheelPositions)
    {
        if (!IsFinite(position))
        {
            SetError(error, "vehicle wheel positions must be finite");
            return nullptr;
        }
    }

    JPH::VehicleConstraintSettings settings;
    settings.mUp = config.up.Normalized();
    settings.mForward = config.forward.Normalized();
    settings.mMaxPitchRollAngle = config.maxPitchRollAngle;

    const JPH::Vec3 flipLeftRight(-1.0f, 1.0f, 1.0f);
    for (unsigned int index = 0; index < 4; ++index)
    {
        JPH::Ref<JPH::WheelSettingsWV> wheel = new JPH::WheelSettingsWV;
        wheel->mPosition = config.wheelPositions[index];
        wheel->mSuspensionDirection = JPH::Vec3(0.0f, -1.0f, 0.0f);
        wheel->mSteeringAxis = JPH::Vec3::sAxisY();
        wheel->mWheelUp = JPH::Vec3::sAxisY();
        wheel->mWheelForward = JPH::Vec3::sAxisZ();
        if ((index & 1u) != 0u)
        {
            wheel->mSuspensionDirection = flipLeftRight * wheel->mSuspensionDirection;
            wheel->mSteeringAxis = flipLeftRight * wheel->mSteeringAxis;
            wheel->mWheelUp = flipLeftRight * wheel->mWheelUp;
            wheel->mWheelForward = flipLeftRight * wheel->mWheelForward;
        }
        wheel->mSuspensionMinLength = config.suspensionMinLength;
        wheel->mSuspensionMaxLength = config.suspensionMaxLength;
        wheel->mSuspensionSpring.mFrequency = config.suspensionFrequency;
        wheel->mSuspensionSpring.mDamping = config.suspensionDamping;
        wheel->mRadius = config.wheelRadius;
        wheel->mWidth = config.wheelWidth;
        wheel->mMaxSteerAngle = index < 2 ? config.maxSteerAngle : 0.0f;
        wheel->mMaxHandBrakeTorque = index < 2 ? 0.0f : 4000.0f;
        settings.mWheels.push_back(static_cast<JPH::WheelSettings*>(wheel));
    }

    JPH::Ref<JPH::WheeledVehicleControllerSettings> controller = new JPH::WheeledVehicleControllerSettings;
    controller->mEngine.mMaxTorque = config.maxEngineTorque;
    controller->mDifferentials.resize(2);
    controller->mDifferentials[0].mLeftWheel = 0;
    controller->mDifferentials[0].mRightWheel = 1;
    controller->mDifferentials[1].mLeftWheel = 2;
    controller->mDifferentials[1].mRightWheel = 3;
    controller->mDifferentials[0].mEngineTorqueRatio = 0.5f;
    controller->mDifferentials[1].mEngineTorqueRatio = 0.5f;
    settings.mController = static_cast<JPH::VehicleControllerSettings*>(controller);

    JPH::Ref<JPH::VehicleConstraint> constraint = new JPH::VehicleConstraint(chassis, settings);
    constraint->SetVehicleCollisionTester(new JPH::VehicleCollisionTesterCastSphere(
        config.wheelObjectLayer, config.wheelRadius, settings.mUp));
    physics.AddConstraint(constraint);
    physics.AddStepListener(constraint);
    return std::unique_ptr<Vehicle>(new Vehicle(physics, constraint));
}

Vehicle::Vehicle(JPH::PhysicsSystem& physics, JPH::Ref<JPH::VehicleConstraint> constraint)
    : physics_(physics), constraint_(std::move(constraint))
{
}

Vehicle::~Vehicle()
{
    if (constraint_ == nullptr)
        return;
    physics_.RemoveStepListener(constraint_);
    physics_.RemoveConstraint(constraint_);
    constraint_ = nullptr;
}

void Vehicle::SetDriverInput(float forward, float steering, float brake, float handBrake)
{
    JPH::WheeledVehicleController* controller = static_cast<JPH::WheeledVehicleController*>(constraint_->GetController());
    controller->SetDriverInput(
        std::clamp(forward, -1.0f, 1.0f),
        std::clamp(steering, -1.0f, 1.0f),
        std::clamp(brake, 0.0f, 1.0f),
        std::clamp(handBrake, 0.0f, 1.0f));
}

bool Vehicle::GetWheelState(unsigned int index, WheelState& outState) const
{
    if (index >= constraint_->GetWheels().size())
        return false;
    const JPH::Wheel* wheel = constraint_->GetWheel(index);
    outState.hasContact = wheel->HasContact();
    outState.suspensionLength = wheel->GetSuspensionLength();
    outState.steerAngle = wheel->GetSteerAngle();
    outState.angularVelocity = wheel->GetAngularVelocity();
    if (outState.hasContact)
        outState.contactNormal = wheel->GetContactNormal();
    return true;
}

} // namespace aura::jolt_vehicle
