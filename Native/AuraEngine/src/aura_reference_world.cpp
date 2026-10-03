#include "aura_reference_world.h"

#include <algorithm>
#include <cmath>

namespace aura
{
namespace
{
constexpr float kEpsilon = 1e-5f;
constexpr float kMaxSpeed = 1000.0f;

float Dot(const AuraVec3& a, const AuraVec3& b) { return a.x * b.x + a.y * b.y + a.z * b.z; }
AuraVec3 Sub(const AuraVec3& a, const AuraVec3& b) { return { a.x - b.x, a.y - b.y, a.z - b.z }; }
AuraVec3 Add(const AuraVec3& a, const AuraVec3& b) { return { a.x + b.x, a.y + b.y, a.z + b.z }; }
AuraVec3 Scale(const AuraVec3& a, float s) { return { a.x * s, a.y * s, a.z * s }; }
float Length(const AuraVec3& a) { return std::sqrt(Dot(a, a)); }
AuraVec3 Normalize(const AuraVec3& a)
{
    const float length = Length(a);
    return length <= kEpsilon ? AuraVec3{ 1.0f, 0.0f, 0.0f } : Scale(a, 1.0f / length);
}

float Component(const AuraVec3& v, int axis)
{
    return axis == 0 ? v.x : (axis == 1 ? v.y : v.z);
}

AuraVec3 Rotate(const AuraQuat& q, const AuraVec3& v)
{
    const float x2 = q.x + q.x, y2 = q.y + q.y, z2 = q.z + q.z;
    const float xx = q.x * x2, yy = q.y * y2, zz = q.z * z2;
    const float xy = q.x * y2, xz = q.x * z2, yz = q.y * z2;
    const float wx = q.w * x2, wy = q.w * y2, wz = q.w * z2;

    return {
        (1.0f - (yy + zz)) * v.x + (xy - wz) * v.y + (xz + wy) * v.z,
        (xy + wz) * v.x + (1.0f - (xx + zz)) * v.y + (yz - wx) * v.z,
        (xz - wy) * v.x + (yz + wx) * v.y + (1.0f - (xx + yy)) * v.z,
    };
}

AuraVec3 TransformPoint(const AuraPose& pose, const AuraVec3& local)
{
    return Add(pose.position, Rotate(pose.rotation, local));
}

AuraVec3 ShapeCenter(const Body& body, const Shape& shape) { return TransformPoint(body.pose, shape.local.position); }

AuraVec3 RotatedExtents(const AuraQuat& q, const AuraVec3& h)
{
    const float x = q.x, y = q.y, z = q.z, w = q.w;
    const float m00 = 1 - 2 * (y * y + z * z), m01 = 2 * (x * y - z * w), m02 = 2 * (x * z + y * w);
    const float m10 = 2 * (x * y + z * w), m11 = 1 - 2 * (x * x + z * z), m12 = 2 * (y * z - x * w);
    const float m20 = 2 * (x * z - y * w), m21 = 2 * (y * z + x * w), m22 = 1 - 2 * (x * x + y * y);
    return {
        std::fabs(m00) * h.x + std::fabs(m01) * h.y + std::fabs(m02) * h.z,
        std::fabs(m10) * h.x + std::fabs(m11) * h.y + std::fabs(m12) * h.z,
        std::fabs(m20) * h.x + std::fabs(m21) * h.y + std::fabs(m22) * h.z,
    };
}

bool SphereSphere(const AuraVec3& ca, float ra, const AuraVec3& cb, float rb, AuraVec3& normal, float& depth, AuraVec3& point)
{
    const AuraVec3 delta = Sub(cb, ca);
    const float distance = Length(delta);
    const float sum = ra + rb;
    if (distance >= sum)
        return false;

    normal = distance > kEpsilon ? Scale(delta, 1.0f / distance) : AuraVec3{ 1.0f, 0.0f, 0.0f };
    depth = sum - distance;
    point = Add(ca, Scale(normal, ra));
    return true;
}

bool SphereBox(const AuraVec3& sphere, float radius, const AuraVec3& boxCenter, const AuraVec3& extents, AuraVec3& normal, float& depth, AuraVec3& point)
{
    const AuraVec3 local = Sub(sphere, boxCenter);
    AuraVec3 closest{ std::clamp(local.x, -extents.x, extents.x), std::clamp(local.y, -extents.y, extents.y), std::clamp(local.z, -extents.z, extents.z) };
    const AuraVec3 delta = Sub(local, closest);
    const float distance = Length(delta);
    if (distance > radius + kEpsilon)
        return false;

    if (distance > kEpsilon)
    {
        normal = Scale(delta, 1.0f / distance);
        depth = radius - distance;
        point = Add(boxCenter, closest);
        return true;
    }

    const float ox = extents.x - std::fabs(local.x);
    const float oy = extents.y - std::fabs(local.y);
    const float oz = extents.z - std::fabs(local.z);
    if (ox <= oy && ox <= oz)
        normal = { local.x >= 0 ? 1.0f : -1.0f, 0.0f, 0.0f };
    else if (oy <= oz)
        normal = { 0.0f, local.y >= 0 ? 1.0f : -1.0f, 0.0f };
    else
        normal = { 0.0f, 0.0f, local.z >= 0 ? 1.0f : -1.0f };
    depth = radius + std::min(ox, std::min(oy, oz));
    point = Sub(sphere, Scale(normal, radius));
    return true;
}

bool BoxBox(const AuraVec3& ca, const AuraVec3& ea, const AuraVec3& cb, const AuraVec3& eb, AuraPhysicsMode mode, AuraVec3& normal, float& depth, AuraVec3& point)
{
    const AuraVec3 delta = Sub(cb, ca);
    const float ox = ea.x + eb.x - std::fabs(delta.x);
    const float oy = ea.y + eb.y - std::fabs(delta.y);
    point = Add(ca, Scale(delta, 0.5f));

    if (mode == AURA_MODE_PLANE_2D)
    {
        if (ox <= 0 || oy <= 0)
            return false;
        normal = ox <= oy ? AuraVec3{ delta.x >= 0 ? 1.0f : -1.0f, 0.0f, 0.0f } : AuraVec3{ 0.0f, delta.y >= 0 ? 1.0f : -1.0f, 0.0f };
        depth = std::min(ox, oy);
        return true;
    }

    const float oz = ea.z + eb.z - std::fabs(delta.z);
    if (ox <= 0 || oy <= 0 || oz <= 0)
        return false;
    if (ox <= oy && ox <= oz)
        normal = { delta.x >= 0 ? 1.0f : -1.0f, 0.0f, 0.0f };
    else if (oy <= oz)
        normal = { 0.0f, delta.y >= 0 ? 1.0f : -1.0f, 0.0f };
    else
        normal = { 0.0f, 0.0f, delta.z >= 0 ? 1.0f : -1.0f };
    depth = std::min(ox, std::min(oy, oz));
    return true;
}

bool ShapeContact(const Body& a, const Shape& sa, const Body& b, const Shape& sb, AuraPhysicsMode mode, AuraVec3& normal, float& depth, AuraVec3& point)
{
    const bool aSphere = sa.type == AURA_SHAPE_SPHERE;
    const bool bSphere = sb.type == AURA_SHAPE_SPHERE;
    const bool aBox = sa.type == AURA_SHAPE_BOX;
    const bool bBox = sb.type == AURA_SHAPE_BOX;

    if (aSphere && bSphere)
        return SphereSphere(ShapeCenter(a, sa), sa.radius, ShapeCenter(b, sb), sb.radius, normal, depth, point);
    if (aSphere && bBox)
    {
        if (!SphereBox(ShapeCenter(a, sa), sa.radius, ShapeCenter(b, sb), RotatedExtents(b.pose.rotation, sb.halfExtents), normal, depth, point))
            return false;
        normal = Scale(normal, -1.0f);
        return true;
    }
    if (aBox && bSphere)
        return SphereBox(ShapeCenter(b, sb), sb.radius, ShapeCenter(a, sa), RotatedExtents(a.pose.rotation, sa.halfExtents), normal, depth, point);
    if (aBox && bBox)
        return BoxBox(ShapeCenter(a, sa), RotatedExtents(a.pose.rotation, sa.halfExtents), ShapeCenter(b, sb), RotatedExtents(b.pose.rotation, sb.halfExtents), mode, normal, depth, point);
    return false;
}
} // namespace

ReferenceWorld::ReferenceWorld(const AuraWorldDesc& desc)
    : mode_(desc.mode),
      gravity_(desc.mode == AURA_MODE_PLANE_2D ? AuraVec3{ desc.gravity.x, desc.gravity.y, 0.0f } : desc.gravity),
      capacity_(desc.initialBodyCapacity == 0 ? 256 : desc.initialBodyCapacity)
{
    for (int i = 0; i < 64; ++i)
        matrix_[i] = ~0ull;
    if (desc.collisionMasks != nullptr)
    {
        for (uint32_t i = 0; i < desc.collisionMaskCount && i < 64; ++i)
            matrix_[i] = desc.collisionMasks[i];
    }
    bodies_.reserve(capacity_);
}

AuraResultCode ReferenceWorld::CreateBody(const AuraBodyDesc& desc, AuraBodyHandle* outBody)
{
    if (outBody == nullptr)
        return AURA_INVALID_DEFINITION;

    Body body;
    body.occupied = true;
    body.type = desc.type;
    body.layer = desc.layer;
    body.mask = desc.collisionMask;
    body.group = desc.groupIndex;
    body.invMass = desc.type == AURA_BODY_DYNAMIC && desc.mass > 0.0f ? 1.0f / desc.mass : 0.0f;
    body.gravityScale = desc.gravityScale;
    body.friction = desc.friction;
    body.restitution = desc.restitution;
    body.pose = desc.initialPose;
    body.velocity = desc.initialLinearVelocity;
    body.angularVelocity = desc.initialAngularVelocity;
    body.awake = desc.type != AURA_BODY_STATIC;

    for (uint32_t i = 0; i < desc.shapeCount; ++i)
    {
        const AuraShapeDesc& s = desc.shapes[i];
        if (s.type == AURA_SHAPE_HEIGHT_FIELD || s.type == AURA_SHAPE_TRIANGLE_MESH || s.type == AURA_SHAPE_CONVEX_MESH)
            return AURA_UNSUPPORTED_SHAPE;
        Shape shape;
        shape.type = s.type;
        shape.local = s.localPose;
        shape.trigger = s.isTrigger != 0;
        shape.friction = s.friction;
        shape.restitution = s.restitution;
        shape.density = s.density;
        shape.halfExtents = s.halfExtents;
        shape.radius = s.radius;
        shape.height = s.height;
        shape.meshAsset = s.meshAsset;
        body.shapes.push_back(shape);
    }

    int index;
    if (!freeList_.empty())
    {
        index = freeList_.back();
        freeList_.pop_back();
        body.generation = bodies_[index].generation;
        bodies_[index] = std::move(body);
    }
    else
    {
        index = static_cast<int>(bodies_.size());
        bodies_.push_back(std::move(body));
    }

    outBody->index = static_cast<uint32_t>(index);
    outBody->generation = bodies_[index].generation;
    return AURA_SUCCESS;
}

AuraResultCode ReferenceWorld::DestroyBody(AuraBodyHandle body)
{
    Body* found = Find(body);
    if (found == nullptr)
        return AURA_INVALID_HANDLE;
    found->occupied = false;
    found->generation += 1;
    freeList_.push_back(static_cast<int>(body.index));
    return AURA_SUCCESS;
}

bool ReferenceWorld::HasBody(AuraBodyHandle body) const { return Find(body) != nullptr; }

AuraResultCode ReferenceWorld::SetKinematicTarget(AuraBodyHandle body, const AuraPose& pose)
{
    Body* found = Find(body);
    if (found == nullptr)
        return AURA_INVALID_HANDLE;
    found->pose = pose;
    if (mode_ == AURA_MODE_PLANE_2D)
        found->pose.position.z = 0.0f;
    found->velocity = { 0.0f, 0.0f, 0.0f };
    found->awake = true;
    return AURA_SUCCESS;
}

AuraResultCode ReferenceWorld::GetBodyState(AuraBodyHandle body, AuraBodyState* outState) const
{
    const Body* found = Find(body);
    if (found == nullptr || outState == nullptr)
        return AURA_INVALID_HANDLE;
    outState->body = body;
    outState->entity = { 0, 0 };
    outState->pose = found->pose;
    outState->linearVelocity = found->velocity;
    outState->angularVelocity = found->angularVelocity;
    outState->isAwake = found->awake ? 1 : 0;
    outState->flags = 0;
    return AURA_SUCCESS;
}

uint32_t ReferenceWorld::CopyBodyStates(AuraBodyState* buffer, uint32_t capacity) const
{
    uint32_t written = 0;
    for (uint32_t i = 0; i < bodies_.size() && written < capacity; ++i)
    {
        if (!bodies_[i].occupied)
            continue;
        buffer[written].body = { i, bodies_[i].generation };
        buffer[written].entity = { 0, 0 };
        buffer[written].pose = bodies_[i].pose;
        buffer[written].linearVelocity = bodies_[i].velocity;
        buffer[written].angularVelocity = bodies_[i].angularVelocity;
        buffer[written].isAwake = bodies_[i].awake ? 1 : 0;
        buffer[written].flags = 0;
        ++written;
    }
    return written;
}

uint32_t ReferenceWorld::BodyCount() const
{
    uint32_t count = 0;
    for (const Body& body : bodies_)
        if (body.occupied)
            ++count;
    return count;
}

void ReferenceWorld::Step(float deltaTime)
{
    if (waterActive_)
        ApplyWaterStep(AuraWaterHandle{ 1 }, deltaTime);
    Integrate(deltaTime);
    Detect();
    Resolve();
    EmitEvents();
}

AuraResultCode ReferenceWorld::CreateWater(const AuraWaterDesc& desc, AuraWaterHandle* outWater)
{
    if (outWater == nullptr || desc.density <= 0.0f)
        return AURA_INVALID_DEFINITION;
    water_ = desc;
    waterActive_ = true;
    *outWater = AuraWaterHandle{ 1 };
    return AURA_SUCCESS;
}

AuraResultCode ReferenceWorld::DestroyWater(AuraWaterHandle water)
{
    if (!waterActive_ || water.opaque != 1)
        return AURA_INVALID_HANDLE;
    waterActive_ = false;
    return AURA_SUCCESS;
}

AuraResultCode ReferenceWorld::SetWaterParameters(AuraWaterHandle water, const AuraWaterDesc& desc)
{
    if (!waterActive_ || water.opaque != 1 || desc.density <= 0.0f)
        return AURA_INVALID_HANDLE;
    water_ = desc;
    return AURA_SUCCESS;
}

AuraResultCode ReferenceWorld::ApplyWaterStep(AuraWaterHandle water, float deltaTime)
{
    if (!waterActive_ || water.opaque != 1 || deltaTime < 0.0f)
        return AURA_INVALID_HANDLE;
    for (Body& body : bodies_)
    {
        if (!body.occupied || body.type != AURA_BODY_DYNAMIC || body.invMass <= 0.0f || body.shapes.empty())
            continue;
        float volume = 0.0f;
        float halfHeight = 0.0f;
        for (const Shape& shape : body.shapes)
        {
            if (shape.type == AURA_SHAPE_BOX)
            {
                volume += 8.0f * shape.halfExtents.x * shape.halfExtents.y * shape.halfExtents.z;
                halfHeight = std::max(halfHeight, shape.halfExtents.y);
            }
            else if (shape.type == AURA_SHAPE_SPHERE)
            {
                volume += 4.1887902f * shape.radius * shape.radius * shape.radius;
                halfHeight = std::max(halfHeight, shape.radius);
            }
        }
        if (volume <= 0.0f || halfHeight <= 0.0f)
            continue;
        const float submerged = std::clamp((water_.surfaceHeight - (body.pose.position.y - halfHeight)) / (2.0f * halfHeight), 0.0f, 1.0f);
        const AuraVec3 force = Scale(gravity_, -water_.density * volume * submerged);
        body.velocity = Add(body.velocity, Scale(force, body.invMass * deltaTime));
        if (water_.linearDrag > 0.0f)
            body.velocity = Scale(body.velocity, std::max(0.0f, 1.0f - water_.linearDrag * deltaTime * submerged));
    }
    return AURA_SUCCESS;
}

void ReferenceWorld::Integrate(float deltaTime)
{
    for (Body& body : bodies_)
    {
        if (!body.occupied || body.type != AURA_BODY_DYNAMIC)
            continue;
        body.velocity = Add(body.velocity, Scale(gravity_, body.gravityScale * deltaTime));
        if (mode_ == AURA_MODE_PLANE_2D)
            body.velocity.z = 0.0f;
        const float speed = Length(body.velocity);
        if (speed > kMaxSpeed)
            body.velocity = Scale(Normalize(body.velocity), kMaxSpeed);
        body.pose.position = Add(body.pose.position, Scale(body.velocity, deltaTime));
        if (mode_ == AURA_MODE_PLANE_2D)
            body.pose.position.z = 0.0f;
    }
}

bool ReferenceWorld::Allowed(const Body& a, const Body& b) const
{
    if (((matrix_[a.layer] >> b.layer) & 1ull) == 0ull)
        return false;
    if (((a.mask >> b.layer) & 1ull) == 0ull || ((b.mask >> a.layer) & 1ull) == 0ull)
        return false;
    if (a.group != 0 && a.group == b.group)
        return a.group > 0;
    return true;
}

void ReferenceWorld::Detect()
{
    contacts_.clear();
    for (size_t i = 0; i < bodies_.size(); ++i)
    {
        const Body& a = bodies_[i];
        if (!a.occupied)
            continue;
        for (size_t j = i + 1; j < bodies_.size(); ++j)
        {
            const Body& b = bodies_[j];
            if (!b.occupied || !Allowed(a, b))
                continue;

            bool found = false;
            Contact best;
            for (const Shape& sa : a.shapes)
            {
                for (const Shape& sb : b.shapes)
                {
                    AuraVec3 normal;
                    float depth;
                    AuraVec3 point;
                    if (!ShapeContact(a, sa, b, sb, mode_, normal, depth, point))
                        continue;
                    const bool trigger = sa.trigger || sb.trigger;
                    if (!found || (trigger && !best.trigger) || depth > best.depth)
                    {
                        best.a = static_cast<int>(i);
                        best.b = static_cast<int>(j);
                        best.normal = normal;
                        best.depth = depth;
                        best.point = point;
                        best.trigger = trigger;
                        found = true;
                    }
                }
            }

            if (found)
                contacts_.push_back(best);
        }
    }
}

void ReferenceWorld::Resolve()
{
    for (Contact& contact : contacts_)
    {
        if (contact.trigger)
            continue;

        Body& a = bodies_[contact.a];
        Body& b = bodies_[contact.b];
        const float wa = a.invMass;
        const float wb = b.invMass;
        const float total = wa + wb;
        if (total <= 0.0f)
            continue;

        a.pose.position = Sub(a.pose.position, Scale(contact.normal, contact.depth * (wa / total)));
        b.pose.position = Add(b.pose.position, Scale(contact.normal, contact.depth * (wb / total)));

        const AuraVec3 relative = Sub(b.velocity, a.velocity);
        const float normalSpeed = Dot(relative, contact.normal);
        if (normalSpeed < 0.0f)
        {
            const float restitution = (a.restitution + b.restitution) * 0.5f;
            const float impulse = -(1.0f + restitution) * normalSpeed / total;
            contact.impulse = impulse;
            a.velocity = Sub(a.velocity, Scale(contact.normal, impulse * wa));
            b.velocity = Add(b.velocity, Scale(contact.normal, impulse * wb));
        }
    }
}

void ReferenceWorld::EmitEvents()
{
    events_.clear();

    for (const Contact& contact : contacts_)
    {
        bool existed = false;
        for (const Contact& previous : previous_)
        {
            if ((previous.a == contact.a && previous.b == contact.b))
            {
                existed = true;
                break;
            }
        }

        if (!existed)
        {
            AuraPhysicsEvent event{};
            event.type = contact.trigger ? 2 : 0;
            event.bodyA = { static_cast<uint32_t>(contact.a), bodies_[contact.a].generation };
            event.bodyB = { static_cast<uint32_t>(contact.b), bodies_[contact.b].generation };
            event.point = contact.point;
            event.normal = contact.normal;
            event.impulse = contact.impulse;
            events_.push_back(event);
        }
    }

    for (const Contact& previous : previous_)
    {
        bool present = false;
        for (const Contact& contact : contacts_)
        {
            if (contact.a == previous.a && contact.b == previous.b)
            {
                present = true;
                break;
            }
        }

        if (!present)
        {
            AuraPhysicsEvent event{};
            event.type = previous.trigger ? 3 : 1;
            event.bodyA = { static_cast<uint32_t>(previous.a), bodies_[previous.a].generation };
            event.bodyB = { static_cast<uint32_t>(previous.b), bodies_[previous.b].generation };
            event.point = previous.point;
            event.normal = previous.normal;
            events_.push_back(event);
        }
    }

    previous_ = contacts_;
}

uint32_t ReferenceWorld::CopyEvents(AuraPhysicsEvent* buffer, uint32_t capacity)
{
    const uint32_t count = std::min(static_cast<uint32_t>(events_.size()), capacity);
    for (uint32_t i = 0; i < count; ++i)
        buffer[i] = events_[i];
    events_.erase(events_.begin(), events_.begin() + count);
    return count;
}

uint32_t ReferenceWorld::CopyContacts(AuraContact* buffer, uint32_t capacity) const
{
    uint32_t written = 0;
    for (const Contact& contact : contacts_)
    {
        if (written >= capacity)
            break;
        if (contact.trigger || contact.depth <= 0.0f)
            continue;

        AuraContact& out = buffer[written++];
        out = AuraContact{};
        out.bodyA = AuraBodyHandle{ static_cast<uint32_t>(contact.a), bodies_[contact.a].generation };
        out.bodyB = AuraBodyHandle{ static_cast<uint32_t>(contact.b), bodies_[contact.b].generation };
        out.point = contact.point;
        out.normal = contact.normal;
        out.penetration = contact.depth;
        out.impulse = contact.impulse;
    }
    return written;
}

AuraResultCode ReferenceWorld::SetSurfaceVelocity(AuraBodyHandle body, const AuraVec3& velocity)
{
    (void)body;
    (void)velocity;
    return AURA_UNSUPPORTED_QUERY;
}

bool ReferenceWorld::PassesFilter(const Body& body, const AuraQueryFilter& filter) const
{
    if (((filter.layerMask >> body.layer) & 1ull) == 0ull)
        return false;
    if ((filter.flags & 4) != 0 && filter.ignoredBody.index == static_cast<uint32_t>(&body - bodies_.data()))
        return false;
    return true;
}

bool ReferenceWorld::Raycast(const AuraRay& ray, float maxDistance, const AuraQueryFilter& filter, AuraQueryHit* outHit)
{
    if (outHit == nullptr)
        return false;

    float best = maxDistance;
    bool found = false;
    const AuraVec3 direction = Normalize(ray.direction);
    const int axes = mode_ == AURA_MODE_PLANE_2D ? 2 : 3;

    for (size_t i = 0; i < bodies_.size(); ++i)
    {
        const Body& body = bodies_[i];
        if (!body.occupied || !PassesFilter(body, filter))
            continue;
        for (const Shape& shape : body.shapes)
        {
            const AuraVec3 center = ShapeCenter(body, shape);
            float distance;
            AuraVec3 point;
            AuraVec3 normal;
            bool hit = false;

            if (shape.type == AURA_SHAPE_SPHERE)
            {
                const AuraVec3 oc = Sub(ray.origin, center);
                const float b = Dot(oc, direction);
                const float c = Dot(oc, oc) - shape.radius * shape.radius;
                const float disc = b * b - c;
                if (disc >= 0.0f)
                {
                    const float root = std::sqrt(disc);
                    float t = -b - root;
                    if (t < 0.0f)
                        t = -b + root;
                    if (t >= 0.0f && t <= best)
                    {
                        distance = t;
                        point = Add(ray.origin, Scale(direction, t));
                        normal = Normalize(Sub(point, center));
                        hit = true;
                    }
                }
            }
            else if (shape.type == AURA_SHAPE_BOX)
            {
                const AuraVec3 extents = RotatedExtents(body.pose.rotation, shape.halfExtents);
                float tmin = 0.0f;
                float tmax = best;
                int axis = 0;
                float sign = 0.0f;
                bool valid = true;
                for (int axisIndex = 0; axisIndex < axes && valid; ++axisIndex)
                {
                    const float origin = Component(ray.origin, axisIndex);
                    const float dir = Component(direction, axisIndex);
                    const float half = Component(extents, axisIndex);
                    const float position = Component(center, axisIndex);
                    const float low = position - half;
                    const float high = position + half;
                    if (std::fabs(dir) < kEpsilon)
                    {
                        valid = origin >= low && origin <= high;
                        continue;
                    }
                    const float inv = 1.0f / dir;
                    float t1 = (low - origin) * inv;
                    float t2 = (high - origin) * inv;
                    float entrySign = -1.0f;
                    if (t1 > t2)
                    {
                        std::swap(t1, t2);
                        entrySign = 1.0f;
                    }
                    if (t1 > tmin)
                    {
                        tmin = t1;
                        axis = axisIndex;
                        sign = entrySign;
                    }
                    if (t2 < tmax)
                        tmax = t2;
                    if (tmin > tmax)
                        valid = false;
                }

                if (valid && tmin >= 0.0f && tmin <= best)
                {
                    distance = tmin;
                    point = Add(ray.origin, Scale(direction, tmin));
                    normal = axis == 0 ? AuraVec3{ sign, 0, 0 } : (axis == 1 ? AuraVec3{ 0, sign, 0 } : AuraVec3{ 0, 0, sign });
                    hit = true;
                }
            }

            if (hit && distance < best)
            {
                best = distance;
                outHit->entity = { 0, 0 };
                outHit->body = { static_cast<uint32_t>(i), body.generation };
                outHit->shape = static_cast<uint32_t>(i);
                outHit->distance = distance;
                outHit->point = point;
                outHit->normal = normal;
                found = true;
            }
        }
    }

    return found;
}

uint32_t ReferenceWorld::RaycastAll(const AuraRay& ray, float maxDistance, const AuraQueryFilter& filter, AuraQueryHit* buffer, uint32_t capacity)
{
    uint32_t count = 0;
    const AuraVec3 direction = Normalize(ray.direction);
    const int axes = mode_ == AURA_MODE_PLANE_2D ? 2 : 3;

    for (size_t i = 0; i < bodies_.size() && count < capacity; ++i)
    {
        const Body& body = bodies_[i];
        if (!body.occupied || !PassesFilter(body, filter))
            continue;
        for (const Shape& shape : body.shapes)
        {
            if (shape.type == AURA_SHAPE_SPHERE)
            {
                const AuraVec3 center = ShapeCenter(body, shape);
                const AuraVec3 oc = Sub(ray.origin, center);
                const float b = Dot(oc, direction);
                const float c = Dot(oc, oc) - shape.radius * shape.radius;
                const float disc = b * b - c;
                if (disc < 0.0f)
                    continue;
                const float root = std::sqrt(disc);
                float t = -b - root;
                if (t < 0.0f)
                    t = -b + root;
                if (t < 0.0f || t > maxDistance)
                    continue;
                const AuraVec3 point = Add(ray.origin, Scale(direction, t));
                buffer[count].entity = { 0, 0 };
                buffer[count].body = { static_cast<uint32_t>(i), body.generation };
                buffer[count].shape = static_cast<uint32_t>(i);
                buffer[count].distance = t;
                buffer[count].point = point;
                buffer[count].normal = Normalize(Sub(point, center));
                ++count;
            }
            else if (shape.type == AURA_SHAPE_BOX && count < capacity)
            {
                const AuraVec3 extents = RotatedExtents(body.pose.rotation, shape.halfExtents);
                const AuraVec3 center = ShapeCenter(body, shape);
                float tmin = 0.0f;
                float tmax = maxDistance;
                int axis = 0;
                float sign = 0.0f;
                bool valid = true;
                for (int axisIndex = 0; axisIndex < axes && valid; ++axisIndex)
                {
                    const float origin = Component(ray.origin, axisIndex);
                    const float dir = Component(direction, axisIndex);
                    const float half = Component(extents, axisIndex);
                    const float position = Component(center, axisIndex);
                    if (std::fabs(dir) < kEpsilon)
                    {
                        valid = origin >= position - half && origin <= position + half;
                        continue;
                    }
                    const float inv = 1.0f / dir;
                    float t1 = (position - half - origin) * inv;
                    float t2 = (position + half - origin) * inv;
                    float entrySign = -1.0f;
                    if (t1 > t2)
                    {
                        std::swap(t1, t2);
                        entrySign = 1.0f;
                    }
                    if (t1 > tmin)
                    {
                        tmin = t1;
                        axis = axisIndex;
                        sign = entrySign;
                    }
                    if (t2 < tmax)
                        tmax = t2;
                    if (tmin > tmax)
                        valid = false;
                }
                if (!valid || tmin < 0.0f)
                    continue;
                const AuraVec3 point = Add(ray.origin, Scale(direction, tmin));
                buffer[count].entity = { 0, 0 };
                buffer[count].body = { static_cast<uint32_t>(i), body.generation };
                buffer[count].shape = static_cast<uint32_t>(i);
                buffer[count].distance = tmin;
                buffer[count].point = point;
                buffer[count].normal = axis == 0 ? AuraVec3{ sign, 0, 0 } : (axis == 1 ? AuraVec3{ 0, sign, 0 } : AuraVec3{ 0, 0, sign });
                ++count;
            }
        }
    }

    return count;
}

uint32_t ReferenceWorld::OverlapSphere(const AuraVec3& center, float radius, const AuraQueryFilter& filter, AuraQueryHit* buffer, uint32_t capacity)
{
    uint32_t count = 0;
    for (size_t i = 0; i < bodies_.size() && count < capacity; ++i)
    {
        const Body& body = bodies_[i];
        if (!body.occupied || !PassesFilter(body, filter))
            continue;
        for (const Shape& shape : body.shapes)
        {
            const AuraVec3 shapeCenter = ShapeCenter(body, shape);
            bool overlaps = false;
            float distance = 0.0f;
            if (shape.type == AURA_SHAPE_SPHERE)
            {
                distance = Length(Sub(center, shapeCenter));
                overlaps = distance <= radius + shape.radius;
            }
            else if (shape.type == AURA_SHAPE_BOX)
            {
                const AuraVec3 extents = RotatedExtents(body.pose.rotation, shape.halfExtents);
                const AuraVec3 local = Sub(center, shapeCenter);
                const AuraVec3 closest{ std::clamp(local.x, -extents.x, extents.x), std::clamp(local.y, -extents.y, extents.y), std::clamp(local.z, -extents.z, extents.z) };
                distance = Length(Sub(local, closest));
                overlaps = distance <= radius;
            }

            if (!overlaps)
                continue;
            buffer[count].entity = { 0, 0 };
            buffer[count].body = { static_cast<uint32_t>(i), body.generation };
            buffer[count].shape = static_cast<uint32_t>(i);
            buffer[count].distance = distance;
            buffer[count].point = shapeCenter;
            buffer[count].normal = { 0, 0, 0 };
            ++count;
        }
    }
    return count;
}

bool ReferenceWorld::SphereCast(const AuraVec3& origin, float radius, const AuraVec3& direction, float maxDistance, const AuraQueryFilter& filter, AuraQueryHit* outHit)
{
    if (outHit == nullptr || maxDistance <= 0.0f)
        return false;

    const AuraVec3 dir = Normalize(direction);
    constexpr int steps = 64;
    for (int step = 0; step <= steps; ++step)
    {
        const float t = maxDistance * (static_cast<float>(step) / steps);
        const AuraVec3 center = Add(origin, Scale(dir, t));
        AuraQueryHit hits[8];
        const uint32_t count = OverlapSphere(center, radius, filter, hits, 8);
        if (count > 0)
        {
            *outHit = hits[0];
            outHit->distance = t;
            outHit->point = center;
            outHit->normal = Scale(dir, -1.0f);
            return true;
        }
    }
    return false;
}

uint64_t ReferenceWorld::ComputeStateHash() const
{
    uint64_t hash = 14695981039346656037ull;
    auto combine = [&hash](uint64_t value)
    {
        hash ^= value;
        hash *= 1099511628211ull;
    };

    for (uint32_t i = 0; i < bodies_.size(); ++i)
    {
        const Body& body = bodies_[i];
        if (!body.occupied)
            continue;
        combine(i);
        combine(static_cast<uint32_t>(body.pose.position.x * 1000.0f));
        combine(static_cast<uint32_t>(body.pose.position.y * 1000.0f));
        combine(static_cast<uint32_t>(body.pose.position.z * 1000.0f));
    }
    return hash;
}

AuraResultCode ReferenceWorld::ApplyStates(const AuraBodyState* states, uint32_t count)
{
    for (uint32_t i = 0; i < count; ++i)
    {
        const AuraBodyState& state = states[i];
        if (state.body.index >= bodies_.size())
            return AURA_INVALID_HANDLE;
        Body& body = bodies_[state.body.index];
        if (!body.occupied || body.generation != state.body.generation)
            return AURA_INVALID_HANDLE;
        body.pose = state.pose;
        body.velocity = state.linearVelocity;
        body.angularVelocity = state.angularVelocity;
        body.awake = state.isAwake != 0;
    }
    return AURA_SUCCESS;
}

AuraResultCode ReferenceWorld::CreateJoint(const AuraJointDesc& desc, uint64_t* outJoint)
{
    (void)desc;
    (void)outJoint;
    return AURA_UNSUPPORTED_QUERY;
}

AuraResultCode ReferenceWorld::DestroyJoint(uint64_t joint)
{
    (void)joint;
    return AURA_INVALID_HANDLE;
}

bool ReferenceWorld::HasJoint(uint64_t joint) const
{
    (void)joint;
    return false;
}

AuraResultCode ReferenceWorld::CreateCharacter(const AuraCharacterDesc& desc, uint64_t* outCharacter)
{
    (void)desc;
    (void)outCharacter;
    return AURA_UNSUPPORTED_QUERY;
}

AuraResultCode ReferenceWorld::DestroyCharacter(uint64_t character)
{
    (void)character;
    return AURA_INVALID_HANDLE;
}

AuraResultCode ReferenceWorld::GetCharacterState(uint64_t character, AuraCharacterState* outState) const
{
    (void)character;
    (void)outState;
    return AURA_INVALID_HANDLE;
}

AuraResultCode ReferenceWorld::MoveCharacter(uint64_t character, const AuraVec3& desiredTranslation, float deltaTime)
{
    (void)character;
    (void)desiredTranslation;
    (void)deltaTime;
    return AURA_INVALID_HANDLE;
}

uint32_t ReferenceWorld::CharacterCount() const
{
    return 0;
}

uint32_t ReferenceWorld::CopyCharacterStates(AuraCharacterState* buffer, uint32_t capacity) const
{
    (void)buffer;
    (void)capacity;
    return 0;
}

AuraResultCode ReferenceWorld::ApplyCharacterStates(const AuraCharacterState* states, uint32_t count)
{
    (void)states;
    (void)count;
    return AURA_UNSUPPORTED_QUERY;
}

const Body* ReferenceWorld::Find(AuraBodyHandle body) const
{
    if (body.index < bodies_.size() && bodies_[body.index].occupied && bodies_[body.index].generation == body.generation)
        return &bodies_[body.index];
    return nullptr;
}

Body* ReferenceWorld::Find(AuraBodyHandle body)
{
    if (body.index < bodies_.size() && bodies_[body.index].occupied && bodies_[body.index].generation == body.generation)
        return &bodies_[body.index];
    return nullptr;
}

} // namespace aura
