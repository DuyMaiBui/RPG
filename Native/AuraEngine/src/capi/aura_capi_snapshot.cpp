#include "aura_capi_internal.h"

#include <algorithm>
#include <cmath>
#include <cstring>
#include <vector>

namespace
{
constexpr uint32_t kSnapshotMagic = 0x46525541u;
/* v2: header + bodies + characters. v3 appends one BodyExtra per body (exact Box2D rotation, motion type, kinematic
   target pending). v2 buffers stay readable (restored without extras); anything else is rejected. */
constexpr uint16_t kSnapshotVersion = 3u;
constexpr uint16_t kSnapshotVersionNoExtras = 2u;
constexpr uint32_t kSnapshotHeaderSize = 16u;

/* Sanity bounds for restored values. Real simulations stay far inside them; hostile payloads do not. */
constexpr float kMaxPosition = 1.0e6f;
constexpr float kMaxVelocity = 1.0e5f;

struct SnapshotHeader
{
    uint32_t magic;
    uint16_t version;
    uint16_t pad;
    uint32_t bodyCount;
    uint32_t characterCount;
};

bool Bounded(float value, float limit)
{
    return std::isfinite(value) && value >= -limit && value <= limit;
}

bool Bounded(const AuraVec3& v, float limit)
{
    return Bounded(v.x, limit) && Bounded(v.y, limit) && Bounded(v.z, limit);
}

bool ValidQuaternion(const AuraQuat& q)
{
    if (!Bounded(q.x, 4.0f) || !Bounded(q.y, 4.0f) || !Bounded(q.z, 4.0f) || !Bounded(q.w, 4.0f))
        return false;
    const float lengthSquared = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
    return lengthSquared >= 0.25f && lengthSquared <= 4.0f;
}

bool ValidBodyState(const AuraBodyState& state)
{
    return Bounded(state.pose.position, kMaxPosition)
        && ValidQuaternion(state.pose.rotation)
        && Bounded(state.linearVelocity, kMaxVelocity)
        && Bounded(state.angularVelocity, kMaxVelocity)
        && state.isAwake <= 1u
        && (state.flags & ~static_cast<uint32_t>(AURA_BODY_FLAG_DISABLED)) == 0u;
}

bool ValidExtra(const aura::BodyExtra& extra)
{
    if ((extra.flags & ~aura::kBodyExtraKnownFlags) != 0u)
        return false;
    if ((extra.flags & aura::kBodyExtraHasMotionType) != 0u && extra.motionType > static_cast<uint32_t>(AURA_BODY_KINEMATIC))
        return false;
    if ((extra.flags & aura::kBodyExtraHasRotation) != 0u)
    {
        if (!Bounded(extra.rotationA, 1.01f) || !Bounded(extra.rotationB, 1.01f))
            return false;
        const float lengthSquared = extra.rotationA * extra.rotationA + extra.rotationB * extra.rotationB;
        return lengthSquared >= 0.98f && lengthSquared <= 1.02f;
    }
    return true;
}

bool ValidCharacterState(const AuraCharacterState& state)
{
    return Bounded(state.position, kMaxPosition) && Bounded(state.velocity, kMaxVelocity) && state.isGrounded <= 1u;
}
} // namespace

extern "C"
{

AuraResultCode Aura_ComputeStateHash(AuraWorldHandle world, uint64_t* outHash)
{
    auto* instance = aura::ToWorld(world);
    if (instance == nullptr || outHash == nullptr)
        return AURA_INVALID_WORLD;
    *outHash = instance->ComputeStateHash();
    return AURA_SUCCESS;
}

AuraResultCode Aura_SerializeState(AuraWorldHandle world, uint8_t* buffer, uint32_t capacity, uint32_t* outSize)
{
    auto* instance = aura::ToWorld(world);
    if (instance == nullptr || outSize == nullptr)
        return AURA_INVALID_WORLD;

    const uint32_t bodyCount = instance->BodyCount();
    const uint32_t characterCount = instance->CharacterCount();
    const uint32_t size = kSnapshotHeaderSize
        + bodyCount * static_cast<uint32_t>(sizeof(AuraBodyState))
        + characterCount * static_cast<uint32_t>(sizeof(AuraCharacterState))
        + bodyCount * static_cast<uint32_t>(sizeof(aura::BodyExtra));
    *outSize = size;
    if (buffer == nullptr || capacity < size)
        return AURA_CAPACITY_EXCEEDED;

    /* Zero everything first so struct padding is deterministic. */
    std::memset(buffer, 0, size);
    SnapshotHeader header{};
    header.magic = kSnapshotMagic;
    header.version = kSnapshotVersion;
    header.bodyCount = bodyCount;
    header.characterCount = characterCount;
    std::memcpy(buffer, &header, sizeof(header));

    /* Fill aligned scratch arrays and memcpy them in: the caller's buffer has no alignment guarantee. */
    std::vector<AuraBodyState> bodies(bodyCount);
    std::vector<AuraCharacterState> characters(characterCount);
    std::vector<aura::BodyExtra> extras(bodyCount);
    const uint32_t bodiesWritten = instance->CopyBodyStates(bodies.data(), bodyCount);
    const uint32_t charactersWritten = instance->CopyCharacterStates(characters.data(), characterCount);
    const uint32_t extrasWritten = instance->CopyBodyExtras(extras.data(), bodyCount);
    if (bodiesWritten != bodyCount || charactersWritten != characterCount || extrasWritten != bodyCount)
        return AURA_BACKEND_FAILURE;

    uint8_t* cursor = buffer + kSnapshotHeaderSize;
    std::memcpy(cursor, bodies.data(), bodyCount * sizeof(AuraBodyState));
    cursor += bodyCount * sizeof(AuraBodyState);
    std::memcpy(cursor, characters.data(), characterCount * sizeof(AuraCharacterState));
    cursor += characterCount * sizeof(AuraCharacterState);
    std::memcpy(cursor, extras.data(), bodyCount * sizeof(aura::BodyExtra));

    return AURA_SUCCESS;
}

/* Atomic: the whole buffer is validated against the world (header, sizes, counts, every value, handle liveness and
   uniqueness) and copied into aligned scratch memory before anything is applied. A rejected buffer changes nothing.
   Codes: INVALID_WORLD (bad world), INVALID_DEFINITION (malformed buffer: null, size, magic, version, non-finite or
   out-of-range value, bad flag or enum, duplicate body, character count mismatch), CAPACITY_EXCEEDED (more bodies than
   the world holds), INVALID_HANDLE (body handle not live or generation mismatch). */
AuraResultCode Aura_DeserializeState(AuraWorldHandle world, const uint8_t* buffer, uint32_t size)
{
    auto* instance = aura::ToWorld(world);
    if (instance == nullptr)
        return AURA_INVALID_WORLD;
    if (buffer == nullptr || size < kSnapshotHeaderSize)
        return AURA_INVALID_DEFINITION;

    SnapshotHeader header{};
    std::memcpy(&header, buffer, sizeof(header));
    if (header.magic != kSnapshotMagic)
        return AURA_INVALID_DEFINITION;
    const bool hasExtras = header.version == kSnapshotVersion;
    if (!hasExtras && header.version != kSnapshotVersionNoExtras)
        return AURA_INVALID_DEFINITION;

    const uint64_t expected = static_cast<uint64_t>(kSnapshotHeaderSize)
        + static_cast<uint64_t>(header.bodyCount) * sizeof(AuraBodyState)
        + static_cast<uint64_t>(header.characterCount) * sizeof(AuraCharacterState)
        + (hasExtras ? static_cast<uint64_t>(header.bodyCount) * sizeof(aura::BodyExtra) : 0u);
    if (expected != size)
        return AURA_INVALID_DEFINITION;

    if (header.bodyCount > instance->BodyCount())
        return AURA_CAPACITY_EXCEEDED;
    if (header.characterCount != instance->CharacterCount())
        return AURA_INVALID_DEFINITION;

    std::vector<AuraBodyState> bodies(header.bodyCount);
    std::vector<AuraCharacterState> characters(header.characterCount);
    std::vector<aura::BodyExtra> extras(hasExtras ? header.bodyCount : 0u);
    const uint8_t* cursor = buffer + kSnapshotHeaderSize;
    if (header.bodyCount > 0)
        std::memcpy(bodies.data(), cursor, header.bodyCount * sizeof(AuraBodyState));
    cursor += static_cast<size_t>(header.bodyCount) * sizeof(AuraBodyState);
    if (header.characterCount > 0)
        std::memcpy(characters.data(), cursor, header.characterCount * sizeof(AuraCharacterState));
    cursor += static_cast<size_t>(header.characterCount) * sizeof(AuraCharacterState);
    if (hasExtras && header.bodyCount > 0)
        std::memcpy(extras.data(), cursor, header.bodyCount * sizeof(aura::BodyExtra));

    for (const AuraBodyState& state : bodies)
        if (!ValidBodyState(state))
            return AURA_INVALID_DEFINITION;
    for (const aura::BodyExtra& extra : extras)
        if (!ValidExtra(extra))
            return AURA_INVALID_DEFINITION;
    for (const AuraCharacterState& state : characters)
        if (!ValidCharacterState(state))
            return AURA_INVALID_DEFINITION;

    std::vector<uint64_t> keys;
    keys.reserve(bodies.size());
    for (const AuraBodyState& state : bodies)
    {
        AuraBodyState live{};
        if (instance->GetBodyState(state.body, &live) != AURA_SUCCESS)
            return AURA_INVALID_HANDLE;
        keys.push_back((static_cast<uint64_t>(state.body.index) << 32) | state.body.generation);
    }
    std::sort(keys.begin(), keys.end());
    if (std::adjacent_find(keys.begin(), keys.end()) != keys.end())
        return AURA_INVALID_DEFINITION;

    const AuraResultCode bodyResult = instance->ApplyStatesWithExtras(bodies.data(), hasExtras ? extras.data() : nullptr, header.bodyCount);
    if (bodyResult != AURA_SUCCESS)
        return bodyResult;
    if (header.characterCount == 0)
        return AURA_SUCCESS;
    return instance->ApplyCharacterStates(characters.data(), header.characterCount);
}

} // extern "C"
