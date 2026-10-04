#include "aura_box2d_character.h"

#include "aura_box2d_oneway.h"

#include <algorithm>
#include <cfloat>
#include <cmath>

namespace aura
{
namespace
{
constexpr float kPi = 3.14159265358979f;
/* Gap kept between the capsule and the surfaces it touches. */
constexpr float kSkin = 0.01f;
/* Distance probed below the feet to decide whether the character is supported. */
constexpr float kGroundProbe = 0.05f;
constexpr float kMinStick = 0.1f;
constexpr float kEpsilon = 1e-5f;
constexpr int kMaxSlideIterations = 4;
constexpr int kMaxPlanes = 16;
/* One-way surfaces are solid when the hit normal is within this cosine of their up. */
constexpr float kOneWaySolidDot = 0.5f;

float Dot(const b2Vec2& a, const b2Vec2& b) { return a.x * b.x + a.y * b.y; }
float Length(const b2Vec2& a) { return std::sqrt(Dot(a, a)); }
b2Vec2 Add(const b2Vec2& a, const b2Vec2& b) { return b2Vec2{ a.x + b.x, a.y + b.y }; }
b2Vec2 Scale(const b2Vec2& a, float s) { return b2Vec2{ a.x * s, a.y * s }; }
b2Vec2 MulAdd(const b2Vec2& a, float s, const b2Vec2& b) { return b2Vec2{ a.x + s * b.x, a.y + s * b.y }; }

struct CastHit
{
    bool hit = false;
    float fraction = 1.0f;
    b2Vec2 normal{ 0.0f, 1.0f };
    b2Vec2 point{ 0.0f, 0.0f };
    b2ShapeId shape = b2_nullShapeId;
};

struct CastContext
{
    b2Vec2 direction;
    CastHit* result;
};

float CastCallback(b2ShapeId shapeId, b2Vec2 point, b2Vec2 normal, float fraction, void* context)
{
    auto* ctx = static_cast<CastContext*>(context);
    if (b2Shape_IsSensor(shapeId))
        return -1.0f;
    /* Already overlapping shapes are resolved by depenetration, never blocked on. */
    if (fraction <= 0.0f)
        return -1.0f;

    b2Vec2 up;
    if (oneway::TryGetUp(shapeId, &up))
    {
        if (Dot(normal, up) < kOneWaySolidDot || Dot(ctx->direction, up) > 0.0f)
            return -1.0f;
    }

    if (fraction < ctx->result->fraction || !ctx->result->hit)
    {
        ctx->result->hit = true;
        ctx->result->fraction = fraction;
        ctx->result->normal = normal;
        ctx->result->point = point;
        ctx->result->shape = shapeId;
    }
    return fraction;
}

struct PlaneContext
{
    b2CollisionPlane planes[kMaxPlanes];
    int count = 0;
};

bool PlaneCallback(b2ShapeId shapeId, const b2PlaneResult* result, void* context)
{
    auto* ctx = static_cast<PlaneContext*>(context);
    if (b2Shape_IsSensor(shapeId) || oneway::TryGetUp(shapeId, nullptr) || ctx->count >= kMaxPlanes)
        return ctx->count < kMaxPlanes;
    b2CollisionPlane& plane = ctx->planes[ctx->count++];
    plane.plane = result->plane;
    plane.pushLimit = FLT_MAX;
    plane.push = 0.0f;
    plane.clipVelocity = true;
    return true;
}
} // namespace

/* Per-call working object so the casting helpers can share the world and the
   character without widening the public class. */
class Box2DCharacters::Mover
{
public:
    Mover(b2WorldId world, Character& character, float dt)
        : world_(world), c_(character), dt_(dt)
    {
    }

    void Run(const b2Vec2& gravity, const b2Vec2& want, float dt);

private:
    CastHit Cast(const b2Vec2& from, const b2Vec2& translation) const;
    void Depenetrate();
    bool ProbeGround(float distance, float* outGap);
    void Slide(b2Vec2 delta, bool canStep, b2Vec2* velocity, bool* hitCeiling);
    bool TryStepUp(b2Vec2* position, float dx) const;
    void Push(const CastHit& hit, const b2Vec2& velocity) const;

    b2WorldId world_;
    Character& c_;
    float dt_;
    b2BodyId groundBody_ = b2_nullBodyId;
    b2Vec2 groundNormal_{ 0.0f, 1.0f };
    b2Vec2 groundPoint_{ 0.0f, 0.0f };
};

CastHit Box2DCharacters::Mover::Cast(const b2Vec2& from, const b2Vec2& translation) const
{
    CastHit hit;
    const float length = Length(translation);
    if (length < kEpsilon)
        return hit;

    const b2Vec2 points[2] = { b2Vec2{ from.x, from.y + c_.halfSegment }, b2Vec2{ from.x, from.y - c_.halfSegment } };
    const b2ShapeProxy proxy = b2MakeProxy(points, 2, c_.radius);
    CastContext ctx{ Scale(translation, 1.0f / length), &hit };
    b2World_CastShape(world_, &proxy, translation, c_.filter, &CastCallback, &ctx);
    return hit;
}

void Box2DCharacters::Mover::Depenetrate()
{
    for (int iteration = 0; iteration < 3; ++iteration)
    {
        const b2Capsule capsule{ b2Vec2{ c_.position.x, c_.position.y + c_.halfSegment }, b2Vec2{ c_.position.x, c_.position.y - c_.halfSegment }, c_.radius };
        PlaneContext ctx;
        b2World_CollideMover(world_, &capsule, c_.filter, &PlaneCallback, &ctx);

        bool penetrating = false;
        for (int i = 0; i < ctx.count; ++i)
            if (b2PlaneSeparation(ctx.planes[i].plane, b2Vec2{ 0.0f, 0.0f }) < 0.0f)
                penetrating = true;
        if (!penetrating)
            return;

        const b2PlaneSolverResult solved = b2SolvePlanes(c_.position, ctx.planes, ctx.count);
        c_.position = solved.position;
    }
}

bool Box2DCharacters::Mover::ProbeGround(float distance, float* outGap)
{
    groundBody_ = b2_nullBodyId;
    groundNormal_ = b2Vec2{ 0.0f, 1.0f };

    /* Start the cast slightly above the feet: depenetration leaves a sliver of overlap that a cast
       from the exact position would ignore. */
    const float lift = kSkin * 2.0f;
    const CastHit hit = Cast(b2Vec2{ c_.position.x, c_.position.y + lift }, b2Vec2{ 0.0f, -(distance + lift) });
    if (!hit.hit || hit.normal.y < c_.walkableCos)
        return false;

    groundBody_ = b2Shape_GetBody(hit.shape);
    groundNormal_ = hit.normal;
    groundPoint_ = hit.point;
    if (outGap != nullptr)
        *outGap = hit.fraction * (distance + lift) - lift;
    return true;
}

void Box2DCharacters::Mover::Push(const CastHit& hit, const b2Vec2& velocity) const
{
    const b2BodyId body = b2Shape_GetBody(hit.shape);
    if (b2Body_GetType(body) != b2_dynamicBody)
        return;

    const b2Vec2 into = Scale(hit.normal, -1.0f);
    const float approach = Dot(velocity, into) - Dot(b2Body_GetLinearVelocity(body), into);
    if (approach <= 0.0f)
        return;

    /* A body lighter than the character picks up the full approach speed; heavier ones get a share. */
    const float mass = std::min(b2Body_GetMass(body), c_.mass);
    b2Body_ApplyLinearImpulseToCenter(body, Scale(into, approach * mass), true);
}

bool Box2DCharacters::Mover::TryStepUp(b2Vec2* position, float dx) const
{
    const float height = c_.stepHeight;
    const CastHit up = Cast(*position, b2Vec2{ 0.0f, height });
    const float rise = up.hit ? std::max(0.0f, up.fraction * height - kSkin) : height;
    if (rise < kSkin)
        return false;
    b2Vec2 raised = b2Vec2{ position->x, position->y + rise };

    /* Move forward at least part of a radius so the round capsule bottom clears the ledge edge. */
    const float wanted = std::max(std::fabs(dx), c_.radius * 0.75f);
    const float sign = dx < 0.0f ? -1.0f : 1.0f;
    const CastHit forward = Cast(raised, b2Vec2{ sign * wanted, 0.0f });
    const float travel = forward.hit ? std::max(0.0f, forward.fraction * wanted - kSkin) : wanted;
    if (travel < std::min(std::fabs(dx), c_.radius * 0.75f) * 0.99f)
        return false;
    const b2Vec2 over = b2Vec2{ raised.x + sign * travel, raised.y };

    const float drop = rise + kSkin * 2.0f;
    const CastHit down = Cast(over, b2Vec2{ 0.0f, -drop });
    if (!down.hit || down.normal.y < c_.walkableCos)
        return false;
    const float fall = std::max(0.0f, down.fraction * drop - kSkin);
    const b2Vec2 landed = b2Vec2{ over.x, over.y - fall };
    if (landed.y < position->y + kSkin)
        return false;

    *position = landed;
    return true;
}

void Box2DCharacters::Mover::Slide(b2Vec2 delta, bool canStep, b2Vec2* velocity, bool* hitCeiling)
{
    b2Vec2 remaining = delta;
    for (int iteration = 0; iteration < kMaxSlideIterations; ++iteration)
    {
        const float length = Length(remaining);
        if (length < kEpsilon)
            return;

        const CastHit hit = Cast(c_.position, remaining);
        if (!hit.hit)
        {
            c_.position = Add(c_.position, remaining);
            return;
        }

        const b2Vec2 direction = Scale(remaining, 1.0f / length);
        const float cosIncidence = std::max(0.1f, -Dot(direction, hit.normal));
        const float travel = std::min(hit.fraction * length, std::max(0.0f, hit.fraction * length - kSkin / cosIncidence));
        c_.position = MulAdd(c_.position, travel, direction);
        remaining = Scale(direction, length - travel);

        Push(hit, Scale(delta, 1.0f / dt_));

        const b2Vec2 normal = hit.normal;
        const bool walkable = normal.y >= c_.walkableCos;
        if (!walkable && canStep && c_.stepHeight > 0.0f && normal.y > -0.7f && std::fabs(remaining.x) > kEpsilon)
        {
            const float before = c_.position.x;
            if (TryStepUp(&c_.position, remaining.x))
            {
                remaining = b2Vec2{ remaining.x - (c_.position.x - before), 0.0f };
                if (remaining.x * (c_.position.x - before) < 0.0f || std::fabs(remaining.x) < kEpsilon)
                    return;
                canStep = false;
                continue;
            }
        }

        const float into = Dot(remaining, normal);
        if (into < 0.0f)
            remaining = MulAdd(remaining, -into, normal);

        if (!walkable)
        {
            const float speedInto = Dot(*velocity, normal);
            if (speedInto < 0.0f)
                *velocity = MulAdd(*velocity, -speedInto, normal);
            if (normal.y < -0.5f)
                *hitCeiling = true;
            /* Too steep to climb: never slide upward along it. */
            if (normal.y > 0.0f && remaining.y > 0.0f)
                remaining = b2Vec2{ 0.0f, 0.0f };
        }
    }
}

void Box2DCharacters::Mover::Run(const b2Vec2& gravity, const b2Vec2& want, float dt)
{
    Depenetrate();

    const bool jump = want.y > 0.0f;
    /* Still rising from an earlier jump: not supported, whatever is under the feet (a one-way platform
       the feet just cleared, for instance). */
    const bool wasGrounded = c_.velocity.y <= 0.01f && ProbeGround(kGroundProbe, nullptr);
    b2Vec2 velocity = b2Vec2{ want.x, c_.velocity.y };
    if (jump)
        velocity.y = want.y;
    else if (wasGrounded)
        velocity.y = 0.0f;
    else
        velocity.y += gravity.y * dt;

    b2Vec2 platform{ 0.0f, 0.0f };
    if (wasGrounded && b2Body_IsValid(groundBody_))
    {
        if (b2Body_GetType(groundBody_) != b2_staticBody)
            platform = b2Body_GetWorldPointVelocity(groundBody_, groundPoint_);
        /* Weight on a dynamic ground body. */
        if (b2Body_GetType(groundBody_) == b2_dynamicBody)
            b2Body_ApplyLinearImpulse(groundBody_, b2Vec2{ 0.0f, gravity.y * c_.mass * dt }, groundPoint_, true);
    }

    b2Vec2 delta = Scale(velocity, dt);
    if (wasGrounded && !jump && groundNormal_.y > kEpsilon)
        delta.y = -delta.x * groundNormal_.x / groundNormal_.y; /* follow the slope at constant horizontal speed */
    delta = MulAdd(delta, dt, platform);

    bool hitCeiling = false;
    Slide(delta, wasGrounded && !jump, &velocity, &hitCeiling);
    if (hitCeiling && velocity.y > 0.0f)
        velocity.y = 0.0f;

    float gap = 0.0f;
    const bool snap = wasGrounded && !jump;
    const float probe = snap ? std::max(c_.stepHeight, kMinStick) + kSkin : kGroundProbe;
    bool grounded = false;
    if (velocity.y <= 0.0f || snap)
    {
        grounded = ProbeGround(probe, &gap);
        if (grounded && (snap || velocity.y <= 0.0f))
            c_.position.y -= std::max(0.0f, gap - kSkin);
    }

    c_.grounded = grounded;
    if (grounded)
        velocity.y = 0.0f;
    c_.velocity = velocity;
}

AuraResultCode Box2DCharacters::Create(const uint64_t* collisionMatrix, const AuraCharacterDesc& desc, uint64_t* outCharacter)
{
    if (outCharacter == nullptr)
        return AURA_INVALID_HANDLE;

    int index;
    if (!freeSlots_.empty())
    {
        index = freeSlots_.back();
        freeSlots_.pop_back();
    }
    else
    {
        index = static_cast<int>(slots_.size());
        slots_.emplace_back();
    }

    Character& c = slots_[index];
    const uint32_t generation = c.generation;
    c = Character{};
    c.generation = generation;
    c.occupied = true;
    c.radius = std::max(0.05f, desc.radius > 0.0f ? desc.radius : 0.4f);
    const float height = desc.height > 0.0f ? desc.height : 1.8f;
    c.halfSegment = std::max(0.0f, height * 0.5f - c.radius);
    const float slope = desc.maxSlopeAngle > 0.0f ? std::min(desc.maxSlopeAngle, kPi * 0.5f - 0.01f) : kPi * 50.0f / 180.0f;
    c.walkableCos = std::cos(slope) - 0.001f;
    c.stepHeight = std::max(0.0f, desc.stepHeight);
    c.mass = desc.mass > 0.0f ? desc.mass : 70.0f;
    const uint64_t mask = desc.collisionMask != 0 ? desc.collisionMask : ~0ull;
    c.filter = b2QueryFilter{ 1ull << (desc.layer & 63), mask & collisionMatrix[desc.layer & 63] };
    c.position = b2Vec2{ desc.pose.position.x, desc.pose.position.y };
    *outCharacter = (static_cast<uint64_t>(c.generation) << 32) | static_cast<uint32_t>(index);
    return AURA_SUCCESS;
}

Box2DCharacters::Character* Box2DCharacters::Find(uint64_t handle)
{
    const uint32_t index = static_cast<uint32_t>(handle & 0xFFFFFFFFull);
    const uint32_t generation = static_cast<uint32_t>(handle >> 32);
    if (index < slots_.size() && slots_[index].occupied && slots_[index].generation == generation)
        return &slots_[index];
    return nullptr;
}

const Box2DCharacters::Character* Box2DCharacters::Find(uint64_t handle) const
{
    return const_cast<Box2DCharacters*>(this)->Find(handle);
}

AuraResultCode Box2DCharacters::Destroy(uint64_t character)
{
    Character* c = Find(character);
    if (c == nullptr)
        return AURA_INVALID_HANDLE;
    c->occupied = false;
    c->generation += 1;
    freeSlots_.push_back(static_cast<int>(character & 0xFFFFFFFFull));
    return AURA_SUCCESS;
}

AuraResultCode Box2DCharacters::GetState(uint64_t character, AuraCharacterState* outState) const
{
    const Character* c = Find(character);
    if (c == nullptr || outState == nullptr)
        return AURA_INVALID_HANDLE;
    outState->position = AuraVec3{ c->position.x, c->position.y, 0.0f };
    outState->velocity = AuraVec3{ c->velocity.x, c->velocity.y, 0.0f };
    outState->isGrounded = c->grounded ? 1 : 0;
    return AURA_SUCCESS;
}

AuraResultCode Box2DCharacters::Move(b2WorldId world, const b2Vec2& gravity, float defaultDelta, uint64_t character, const AuraVec3& desiredTranslation, float deltaTime)
{
    Character* c = Find(character);
    if (c == nullptr)
        return AURA_INVALID_HANDLE;

    const float dt = deltaTime > 0.0f ? deltaTime : defaultDelta;
    Mover mover(world, *c, dt);
    mover.Run(gravity, b2Vec2{ desiredTranslation.x / dt, desiredTranslation.y / dt }, dt);
    return AURA_SUCCESS;
}

uint32_t Box2DCharacters::Count() const
{
    uint32_t count = 0;
    for (const Character& c : slots_)
        if (c.occupied)
            ++count;
    return count;
}

uint32_t Box2DCharacters::CopyStates(AuraCharacterState* buffer, uint32_t capacity) const
{
    uint32_t written = 0;
    for (const Character& c : slots_)
    {
        if (written >= capacity)
            break;
        if (!c.occupied)
            continue;
        AuraCharacterState& state = buffer[written++];
        state.position = AuraVec3{ c.position.x, c.position.y, 0.0f };
        state.velocity = AuraVec3{ c.velocity.x, c.velocity.y, 0.0f };
        state.isGrounded = c.grounded ? 1 : 0;
    }
    return written;
}

AuraResultCode Box2DCharacters::ApplyStates(const AuraCharacterState* states, uint32_t count)
{
    uint32_t applied = 0;
    for (Character& c : slots_)
    {
        if (applied >= count)
            break;
        if (!c.occupied)
            continue;
        const AuraCharacterState& state = states[applied++];
        c.position = b2Vec2{ state.position.x, state.position.y };
        c.velocity = b2Vec2{ state.velocity.x, state.velocity.y };
        c.grounded = state.isGrounded != 0;
    }
    return AURA_SUCCESS;
}

} // namespace aura
