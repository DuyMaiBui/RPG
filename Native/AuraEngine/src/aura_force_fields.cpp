#include "aura_force_fields.h"

#include <algorithm>
#include <cmath>

namespace aura
{
namespace
{
constexpr float kMinDistance = 0.01f;

bool Finite(float v) { return std::isfinite(v); }
bool Finite(const AuraVec3& v) { return Finite(v.x) && Finite(v.y) && Finite(v.z); }

AuraVec3 Sub(const AuraVec3& a, const AuraVec3& b) { return AuraVec3{ a.x - b.x, a.y - b.y, a.z - b.z }; }
AuraVec3 Add(const AuraVec3& a, const AuraVec3& b) { return AuraVec3{ a.x + b.x, a.y + b.y, a.z + b.z }; }
AuraVec3 Mul(const AuraVec3& a, float s) { return AuraVec3{ a.x * s, a.y * s, a.z * s }; }

/* Rotates v by the inverse of unit quaternion q. */
AuraVec3 InverseRotate(const AuraQuat& q, const AuraVec3& v)
{
    const float lenSq = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
    if (lenSq < 1.0e-12f)
        return v;
    const float inv = 1.0f / std::sqrt(lenSq);
    const float qx = -q.x * inv, qy = -q.y * inv, qz = -q.z * inv, qw = q.w * inv;
    const float tx = 2.0f * (qy * v.z - qz * v.y);
    const float ty = 2.0f * (qz * v.x - qx * v.z);
    const float tz = 2.0f * (qx * v.y - qy * v.x);
    return AuraVec3{ v.x + qw * tx + (qy * tz - qz * ty), v.y + qw * ty + (qz * tx - qx * tz), v.z + qw * tz + (qx * ty - qy * tx) };
}

float ShapeExtent(const AuraForceFieldDesc& d, bool planar)
{
    if (d.shape == AURA_FIELD_SHAPE_SPHERE)
        return d.radius;
    const float z = planar ? 0.0f : d.halfExtents.z;
    return std::sqrt(d.halfExtents.x * d.halfExtents.x + d.halfExtents.y * d.halfExtents.y + z * z);
}

bool Contains(const AuraForceFieldDesc& d, const AuraVec3& position, bool planar)
{
    AuraVec3 offset = Sub(position, d.pose.position);
    if (planar)
        offset.z = 0.0f;
    if (d.shape == AURA_FIELD_SHAPE_SPHERE)
        return offset.x * offset.x + offset.y * offset.y + offset.z * offset.z <= d.radius * d.radius;

    const AuraVec3 local = InverseRotate(d.pose.rotation, offset);
    return std::fabs(local.x) <= d.halfExtents.x && std::fabs(local.y) <= d.halfExtents.y
        && (planar || std::fabs(local.z) <= d.halfExtents.z);
}
} // namespace

bool ForceFieldRegistry::Validate(const AuraForceFieldDesc& d)
{
    if (d.shape < AURA_FIELD_SHAPE_SPHERE || d.shape > AURA_FIELD_SHAPE_BOX)
        return false;
    if (d.kind < AURA_FIELD_DIRECTIONAL || d.kind > AURA_FIELD_DRAG)
        return false;
    if (d.mode < AURA_FIELD_MODE_ACCELERATION || d.mode > AURA_FIELD_MODE_FORCE)
        return false;
    if (d.falloff < AURA_FIELD_FALLOFF_NONE || d.falloff > AURA_FIELD_FALLOFF_INVERSE_SQUARE)
        return false;
    if (!Finite(d.pose.position) || !Finite(d.pose.rotation.x) || !Finite(d.pose.rotation.y)
        || !Finite(d.pose.rotation.z) || !Finite(d.pose.rotation.w) || !Finite(d.halfExtents)
        || !Finite(d.vector) || !Finite(d.radius) || !Finite(d.strength) || !Finite(d.minRadius) || !Finite(d.maxRadius))
        return false;
    if (d.shape == AURA_FIELD_SHAPE_SPHERE && d.radius <= 0.0f)
        return false;
    if (d.shape == AURA_FIELD_SHAPE_BOX && (d.halfExtents.x <= 0.0f || d.halfExtents.y <= 0.0f || d.halfExtents.z <= 0.0f))
        return false;
    if (d.minRadius < 0.0f || d.maxRadius < 0.0f || (d.maxRadius > 0.0f && d.minRadius > d.maxRadius))
        return false;
    if (d.kind == AURA_FIELD_DRAG && d.strength < 0.0f)
        return false;
    return true;
}

const ForceFieldRegistry::Slot* ForceFieldRegistry::Find(AuraForceFieldHandle field) const
{
    const uint32_t index = static_cast<uint32_t>(field.opaque & 0xFFFFFFFFull);
    const uint32_t generation = static_cast<uint32_t>(field.opaque >> 32);
    if (index < slots_.size() && slots_[index].occupied && slots_[index].generation == generation)
        return &slots_[index];
    return nullptr;
}

ForceFieldRegistry::Slot* ForceFieldRegistry::Find(AuraForceFieldHandle field)
{
    return const_cast<Slot*>(static_cast<const ForceFieldRegistry*>(this)->Find(field));
}

AuraResultCode ForceFieldRegistry::Create(const AuraForceFieldDesc& desc, AuraForceFieldHandle* outField)
{
    if (outField == nullptr || !Validate(desc))
        return AURA_INVALID_DEFINITION;

    uint32_t index;
    if (!freeSlots_.empty())
    {
        /* Lowest free slot first keeps ids (and therefore evaluation order) deterministic. */
        auto lowest = std::min_element(freeSlots_.begin(), freeSlots_.end());
        index = *lowest;
        freeSlots_.erase(lowest);
    }
    else
    {
        index = static_cast<uint32_t>(slots_.size());
        slots_.push_back(Slot{});
    }

    Slot& slot = slots_[index];
    slot.occupied = true;
    slot.desc = desc;
    ++liveCount_;
    outField->opaque = (static_cast<uint64_t>(slot.generation) << 32) | index;
    return AURA_SUCCESS;
}

AuraResultCode ForceFieldRegistry::Update(AuraForceFieldHandle field, const AuraForceFieldDesc& desc)
{
    Slot* slot = Find(field);
    if (slot == nullptr)
        return AURA_INVALID_HANDLE;
    if (!Validate(desc))
        return AURA_INVALID_DEFINITION;
    slot->desc = desc;
    return AURA_SUCCESS;
}

AuraResultCode ForceFieldRegistry::Destroy(AuraForceFieldHandle field)
{
    Slot* slot = Find(field);
    if (slot == nullptr)
        return AURA_INVALID_HANDLE;
    slot->occupied = false;
    ++slot->generation;
    freeSlots_.push_back(static_cast<uint32_t>(field.opaque & 0xFFFFFFFFull));
    --liveCount_;
    return AURA_SUCCESS;
}

AuraVec3 ForceFieldRegistry::Integrate(const BodyView& body, float dt, bool planar) const
{
    AuraVec3 velocity = body.velocity;
    if (planar)
        velocity.z = 0.0f;
    if (!(body.mass > 0.0f) || !(dt > 0.0f))
        return body.velocity;

    bool touched = false;
    for (const Slot& slot : slots_)
    {
        if (!slot.occupied)
            continue;
        const AuraForceFieldDesc& d = slot.desc;
        if (d.enabled == 0 || body.layer >= 64u || ((d.layerMask >> body.layer) & 1ull) == 0ull)
            continue;
        if (!Contains(d, body.position, planar))
            continue;

        /* Acceleration-mode fields follow the gravity scale, force-mode ones divide by mass. */
        const float toAcceleration = d.mode == AURA_FIELD_MODE_FORCE ? 1.0f / body.mass : body.gravityScale;

        if (d.kind == AURA_FIELD_DIRECTIONAL)
        {
            AuraVec3 v = d.vector;
            if (planar)
                v.z = 0.0f;
            velocity = Add(velocity, Mul(v, toAcceleration * dt));
        }
        else if (d.kind == AURA_FIELD_RADIAL)
        {
            AuraVec3 toCenter = Sub(d.pose.position, body.position);
            if (planar)
                toCenter.z = 0.0f;
            const float dist = std::sqrt(toCenter.x * toCenter.x + toCenter.y * toCenter.y + toCenter.z * toCenter.z);
            if (dist < 1.0e-6f)
                continue;
            if (d.maxRadius > 0.0f && dist > d.maxRadius)
                continue;

            const float effective = std::max(dist, d.minRadius);
            float magnitude = d.strength;
            if (d.falloff == AURA_FIELD_FALLOFF_LINEAR)
            {
                const float reach = d.maxRadius > 0.0f ? d.maxRadius : ShapeExtent(d, planar);
                magnitude = d.strength * std::max(0.0f, 1.0f - effective / reach);
            }
            else if (d.falloff == AURA_FIELD_FALLOFF_INVERSE_SQUARE)
            {
                const float clamped = std::max(effective, kMinDistance);
                magnitude = d.strength / (clamped * clamped);
            }
            velocity = Add(velocity, Mul(toCenter, magnitude / dist * toAcceleration * dt));
        }
        else
        {
            /* Drag: exact solution of dv/dt = k (wind - v), stable for any k * dt. */
            AuraVec3 wind = d.vector;
            if (planar)
                wind.z = 0.0f;
            const float rate = d.mode == AURA_FIELD_MODE_FORCE ? d.strength / body.mass : d.strength;
            const float blend = 1.0f - std::exp(-rate * dt);
            velocity = Add(velocity, Mul(Sub(wind, velocity), blend));
        }
        touched = true;
    }

    if (!touched)
        return body.velocity;
    if (planar)
        velocity.z = body.velocity.z;
    return velocity;
}

} // namespace aura
