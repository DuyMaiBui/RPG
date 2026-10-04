#pragma once

#include "aura/aura_types.h"

#include <box2d/box2d.h>

#include <cstdint>
#include <vector>

namespace aura
{

/* Kinematic capsule character mover for the Box2D (Plane2D) backend. The
   character is virtual (it has no Box2D body, like Jolt's CharacterVirtual): it
   is moved with shape casts against the world, slides along walls, walks slopes
   up to a maximum angle, steps up small ledges, rides moving platforms, treats
   one-way platforms as solid only from above and shoves light dynamic bodies.

   Vertical convention matches the Jolt path: a positive vertical component of
   the desired translation is an explicit up command (jump) that overrides
   gravity for that call; otherwise the character stays glued to the ground or
   integrates gravity while airborne. The pose position is the capsule centre. */
class Box2DCharacters
{
public:
    AuraResultCode Create(const uint64_t* collisionMatrix, const AuraCharacterDesc& desc, uint64_t* outCharacter);
    AuraResultCode Destroy(uint64_t character);
    AuraResultCode GetState(uint64_t character, AuraCharacterState* outState) const;
    AuraResultCode Move(b2WorldId world, const b2Vec2& gravity, float defaultDelta, uint64_t character, const AuraVec3& desiredTranslation, float deltaTime);

    uint32_t Count() const;
    uint32_t CopyStates(AuraCharacterState* buffer, uint32_t capacity) const;
    AuraResultCode ApplyStates(const AuraCharacterState* states, uint32_t count);

private:
    struct Character
    {
        bool occupied = false;
        uint32_t generation = 0;
        float radius = 0.4f;
        float halfSegment = 0.5f;
        float walkableCos = 0.64f;
        float stepHeight = 0.0f;
        float mass = 70.0f;
        b2QueryFilter filter{};
        b2Vec2 position{ 0.0f, 0.0f };
        b2Vec2 velocity{ 0.0f, 0.0f };
        bool grounded = false;
    };

    class Mover;

    Character* Find(uint64_t handle);
    const Character* Find(uint64_t handle) const;

    std::vector<Character> slots_;
    std::vector<int> freeSlots_;
};

} // namespace aura
