#pragma once

#include <cstdint>

namespace aura
{

/* Snapshot format v3: backend state per body that AuraBodyState cannot carry. The extras array follows the
   character states in the snapshot and has one record per body, in CopyBodyStates order.
   Box2D stores the exact b2Rot (rotationA = cos, rotationB = sin) because the quaternion in AuraBodyState is a
   lossy sin/cos(angle/2) round trip. */
struct BodyExtra
{
    float rotationA;
    float rotationB;
    uint32_t motionType; /* AuraBodyType, meaningful with kHasMotionType */
    uint32_t flags;
};

inline constexpr uint32_t kBodyExtraKinematicPending = 1u;
inline constexpr uint32_t kBodyExtraHasRotation = 2u;
inline constexpr uint32_t kBodyExtraHasMotionType = 4u;
inline constexpr uint32_t kBodyExtraKnownFlags = kBodyExtraKinematicPending | kBodyExtraHasRotation | kBodyExtraHasMotionType;

} // namespace aura
