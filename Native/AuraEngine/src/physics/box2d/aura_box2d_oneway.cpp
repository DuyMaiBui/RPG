#include "aura_box2d_oneway.h"

#include <cmath>

namespace aura
{
namespace oneway
{
namespace
{
constexpr float kPi = 3.14159265358979f;
constexpr uint32_t kAngleSteps = 65535u;
/* Contact normals within this cosine of the platform up count as "from above". */
constexpr float kMinUpDot = 0.5f;
/* A body moving up through the platform faster than this (relative) is passing from below. */
constexpr float kMaxRisingSpeed = 0.05f;

float DecodeAngle(uint32_t bits)
{
    const uint32_t q = (bits >> 1) & kAngleSteps;
    return (static_cast<float>(q) / static_cast<float>(kAngleSteps)) * 2.0f * kPi - kPi;
}

bool Decode(b2ShapeId shape, float* outAngle)
{
    if (!b2Shape_IsValid(shape))
        return false;
    const uintptr_t bits = reinterpret_cast<uintptr_t>(b2Shape_GetUserData(shape));
    if ((bits & 1u) == 0)
        return false;
    *outAngle = DecodeAngle(static_cast<uint32_t>(bits));
    return true;
}
} // namespace

void* EncodeUserData(float localAngle)
{
    float wrapped = std::fmod(localAngle + kPi, 2.0f * kPi);
    if (wrapped < 0.0f)
        wrapped += 2.0f * kPi;
    const uint32_t q = static_cast<uint32_t>(std::lround((wrapped / (2.0f * kPi)) * static_cast<float>(kAngleSteps)));
    return reinterpret_cast<void*>(static_cast<uintptr_t>(1u | ((q & kAngleSteps) << 1)));
}

bool TryGetUp(b2ShapeId shape, b2Vec2* outUp)
{
    float localAngle;
    if (!Decode(shape, &localAngle))
        return false;
    const b2Rot rotation = b2MulRot(b2Body_GetRotation(b2Shape_GetBody(shape)), b2MakeRot(localAngle));
    if (outUp != nullptr)
        *outUp = b2RotateVector(rotation, b2Vec2{ 0.0f, 1.0f });
    return true;
}

bool PreSolve(b2ShapeId shapeA, b2ShapeId shapeB, b2Manifold* manifold, void* context)
{
    (void)context;
    b2Vec2 up;
    float sign;
    b2ShapeId platform;
    b2ShapeId other;
    if (TryGetUp(shapeA, &up))
    {
        platform = shapeA;
        other = shapeB;
        sign = 1.0f;
    }
    else if (TryGetUp(shapeB, &up))
    {
        platform = shapeB;
        other = shapeA;
        sign = -1.0f;
    }
    else
        return true;

    /* The manifold normal points from A to B; make it point from the platform to the other shape. */
    const b2Vec2 normal = b2MulSV(sign, manifold->normal);
    if (b2Dot(normal, up) < kMinUpDot)
        return false;

    const b2Vec2 velocity = b2Sub(b2Body_GetLinearVelocity(b2Shape_GetBody(other)), b2Body_GetLinearVelocity(b2Shape_GetBody(platform)));
    return b2Dot(velocity, up) <= kMaxRisingSpeed;
}

void Install(b2WorldId world)
{
    b2World_SetPreSolveCallback(world, &PreSolve, nullptr);
}

} // namespace oneway
} // namespace aura
