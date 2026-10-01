#pragma once

#include "aura/aura_types.h"
#include "aura_world.h"

#include <cstdint>
#include <vector>

namespace aura
{

struct Shape
{
    AuraShapeType type = AURA_SHAPE_SPHERE;
    AuraPose local{};
    bool trigger = false;
    float friction = 0.5f;
    float restitution = 0.0f;
    float density = 1000.0f;
    AuraVec3 halfExtents{ 0.0f, 0.0f, 0.0f };
    float radius = 0.5f;
    float height = 2.0f;
    int32_t meshAsset = -1;
};

struct Body
{
    uint32_t generation = 0;
    bool occupied = false;
    AuraBodyType type = AURA_BODY_STATIC;
    uint32_t layer = 0;
    uint64_t mask = ~0ull;
    int32_t group = 0;
    float invMass = 0.0f;
    float gravityScale = 1.0f;
    float friction = 0.5f;
    float restitution = 0.0f;
    AuraPose pose{};
    AuraVec3 velocity{ 0.0f, 0.0f, 0.0f };
    AuraVec3 angularVelocity{ 0.0f, 0.0f, 0.0f };
    bool awake = true;
    std::vector<Shape> shapes;
};

struct Contact
{
    int a = 0;
    int b = 0;
    AuraVec3 point{ 0.0f, 0.0f, 0.0f };
    AuraVec3 normal{ 1.0f, 0.0f, 0.0f };
    float depth = 0.0f;
    float impulse = 0.0f;
    bool trigger = false;
};

class ReferenceWorld : public IWorld
{
public:
    explicit ReferenceWorld(const AuraWorldDesc& desc);

    AuraResultCode CreateBody(const AuraBodyDesc& desc, AuraBodyHandle* outBody);
    AuraResultCode DestroyBody(AuraBodyHandle body);
    bool HasBody(AuraBodyHandle body) const;
    AuraResultCode SetKinematicTarget(AuraBodyHandle body, const AuraPose& pose);
    AuraResultCode GetBodyState(AuraBodyHandle body, AuraBodyState* outState) const;
    uint32_t CopyBodyStates(AuraBodyState* buffer, uint32_t capacity) const;
    uint32_t BodyCount() const;

    void Step(float deltaTime);

    uint32_t PendingEventCount() const { return static_cast<uint32_t>(events_.size()); }
    uint32_t CopyEvents(AuraPhysicsEvent* buffer, uint32_t capacity);
    uint32_t CopyContacts(AuraContact* buffer, uint32_t capacity) const;

    AuraResultCode SetSurfaceVelocity(AuraBodyHandle body, const AuraVec3& velocity);

    bool Raycast(const AuraRay& ray, float maxDistance, const AuraQueryFilter& filter, AuraQueryHit* outHit);
    uint32_t RaycastAll(const AuraRay& ray, float maxDistance, const AuraQueryFilter& filter, AuraQueryHit* buffer, uint32_t capacity);
    uint32_t OverlapSphere(const AuraVec3& center, float radius, const AuraQueryFilter& filter, AuraQueryHit* buffer, uint32_t capacity);
    bool SphereCast(const AuraVec3& origin, float radius, const AuraVec3& direction, float maxDistance, const AuraQueryFilter& filter, AuraQueryHit* outHit);

    uint64_t ComputeStateHash() const;
    AuraResultCode ApplyStates(const AuraBodyState* states, uint32_t count);

    AuraResultCode CreateJoint(const AuraJointDesc& desc, uint64_t* outJoint);
    AuraResultCode DestroyJoint(uint64_t joint);
    bool HasJoint(uint64_t joint) const;

    AuraResultCode CreateCharacter(const AuraCharacterDesc& desc, uint64_t* outCharacter);
    AuraResultCode DestroyCharacter(uint64_t character);
    AuraResultCode GetCharacterState(uint64_t character, AuraCharacterState* outState) const;
    AuraResultCode MoveCharacter(uint64_t character, const AuraVec3& desiredTranslation, float deltaTime);

    uint32_t CharacterCount() const;
    uint32_t CopyCharacterStates(AuraCharacterState* buffer, uint32_t capacity) const;
    AuraResultCode ApplyCharacterStates(const AuraCharacterState* states, uint32_t count);

private:
    const Body* Find(AuraBodyHandle body) const;
    Body* Find(AuraBodyHandle body);
    bool Allowed(const Body& a, const Body& b) const;
    bool PassesFilter(const Body& body, const AuraQueryFilter& filter) const;

    void Integrate(float deltaTime);
    void Detect();
    void Resolve();
    void EmitEvents();

    AuraPhysicsMode mode_;
    AuraVec3 gravity_;
    uint32_t capacity_;
    uint64_t matrix_[64];
    std::vector<Body> bodies_;
    std::vector<int> freeList_;
    std::vector<Contact> contacts_;
    std::vector<Contact> previous_;
    std::vector<AuraPhysicsEvent> events_;
};

} // namespace aura
