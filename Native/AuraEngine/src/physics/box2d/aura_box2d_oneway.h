#pragma once

#include <box2d/box2d.h>

#include <cstdint>

namespace aura
{
namespace oneway
{

/* One-way platform support for the Box2D backend. A one-way shape is solid only
   against bodies that approach it from its "up" side (the shape's local +Y,
   rotated by the shape's localPose and the owning body). Everything else passes
   through. The flag lives in the shape user data, packed into 32 bits so it is
   valid on 32-bit targets as well: bit 0 = one-way, bits 1..16 = quantised
   local angle of the platform. */

/* User data value for a one-way shape whose up direction is local +Y rotated by localAngle (radians). */
void* EncodeUserData(float localAngle);

/* True when the shape is flagged one-way; outUp receives its world-space up direction. */
bool TryGetUp(b2ShapeId shape, b2Vec2* outUp);

/* Pre-solve callback (b2PreSolveFcn): disables contacts a body makes with a one-way shape from the wrong side. */
bool PreSolve(b2ShapeId shapeA, b2ShapeId shapeB, b2Manifold* manifold, void* context);

/* Installs the pre-solve callback on the world. */
void Install(b2WorldId world);

} // namespace oneway
} // namespace aura
