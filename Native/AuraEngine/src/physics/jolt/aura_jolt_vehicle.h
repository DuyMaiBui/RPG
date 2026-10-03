#pragma once

#include <Jolt/Jolt.h>
#include <Jolt/Physics/Body/Body.h>
#include <Jolt/Physics/PhysicsSystem.h>
#include <Jolt/Physics/Vehicle/VehicleConstraint.h>

#include <array>
#include <memory>
#include <string>
#include <utility>

namespace aura::jolt_vehicle
{

struct VehicleConfig
{
    JPH::Vec3 up = JPH::Vec3::sAxisY();
    JPH::Vec3 forward = JPH::Vec3::sAxisZ();
    JPH::Vec3 wheelPositions[4] = {
        JPH::Vec3(0.85f, -0.55f, 1.15f),
        JPH::Vec3(-0.85f, -0.55f, 1.15f),
        JPH::Vec3(0.85f, -0.55f, -1.15f),
        JPH::Vec3(-0.85f, -0.55f, -1.15f)
    };
    float wheelRadius = 0.35f;
    float wheelWidth = 0.22f;
    float suspensionMinLength = 0.25f;
    float suspensionMaxLength = 0.45f;
    float suspensionFrequency = 2.0f;
    float suspensionDamping = 0.7f;
    float maxSteerAngle = JPH::DegreesToRadians(35.0f);
    float maxPitchRollAngle = JPH::DegreesToRadians(60.0f);
    float maxEngineTorque = 500.0f;
    JPH::ObjectLayer wheelObjectLayer = 0;
};

struct WheelState
{
    bool hasContact = false;
    float suspensionLength = 0.0f;
    float steerAngle = 0.0f;
    float angularVelocity = 0.0f;
    JPH::Vec3 contactNormal = JPH::Vec3::sAxisY();
};

class Vehicle final
{
public:
    static std::unique_ptr<Vehicle> Create(
        JPH::PhysicsSystem& physics,
        JPH::Body& chassis,
        const VehicleConfig& config,
        std::string* error = nullptr);

    ~Vehicle();

    Vehicle(const Vehicle&) = delete;
    Vehicle& operator=(const Vehicle&) = delete;

    void SetDriverInput(float forward, float steering, float brake, float handBrake);
    bool GetWheelState(unsigned int index, WheelState& outState) const;
    JPH::VehicleConstraint& Constraint() { return *constraint_; }
    const JPH::VehicleConstraint& Constraint() const { return *constraint_; }

private:
    Vehicle(JPH::PhysicsSystem& physics, JPH::Ref<JPH::VehicleConstraint> constraint);

    JPH::PhysicsSystem& physics_;
    JPH::Ref<JPH::VehicleConstraint> constraint_;
};

} // namespace aura::jolt_vehicle
