#include "aura_capi_internal.h"

#include <cstring>

namespace
{
constexpr uint32_t kSnapshotMagic = 0x46525541u;
constexpr uint16_t kSnapshotVersion = 2u;
constexpr uint32_t kSnapshotHeaderSize = 16u;

struct SnapshotHeader
{
    uint32_t magic;
    uint16_t version;
    uint16_t pad;
    uint32_t bodyCount;
    uint32_t characterCount;
};
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
        + characterCount * static_cast<uint32_t>(sizeof(AuraCharacterState));
    *outSize = size;
    if (buffer == nullptr || capacity < size)
        return AURA_CAPACITY_EXCEEDED;

    SnapshotHeader header{};
    header.magic = kSnapshotMagic;
    header.version = kSnapshotVersion;
    header.bodyCount = bodyCount;
    header.characterCount = characterCount;
    std::memcpy(buffer, &header, sizeof(header));

    auto* bodyBuffer = reinterpret_cast<AuraBodyState*>(buffer + kSnapshotHeaderSize);
    const uint32_t written = instance->CopyBodyStates(bodyBuffer, bodyCount);
    auto* characterBuffer = reinterpret_cast<AuraCharacterState*>(
        buffer + kSnapshotHeaderSize + written * sizeof(AuraBodyState));
    instance->CopyCharacterStates(characterBuffer, characterCount);

    return AURA_SUCCESS;
}

AuraResultCode Aura_DeserializeState(AuraWorldHandle world, const uint8_t* buffer, uint32_t size)
{
    auto* instance = aura::ToWorld(world);
    if (instance == nullptr || buffer == nullptr)
        return AURA_INVALID_WORLD;
    if (size < kSnapshotHeaderSize)
        return AURA_INVALID_DEFINITION;

    SnapshotHeader header{};
    std::memcpy(&header, buffer, sizeof(header));
    if (header.magic != kSnapshotMagic || header.version != kSnapshotVersion)
        return AURA_INVALID_DEFINITION;

    const uint64_t expected = static_cast<uint64_t>(kSnapshotHeaderSize)
        + static_cast<uint64_t>(header.bodyCount) * sizeof(AuraBodyState)
        + static_cast<uint64_t>(header.characterCount) * sizeof(AuraCharacterState);
    if (expected != size)
        return AURA_INVALID_DEFINITION;

    const auto* bodyBuffer = reinterpret_cast<const AuraBodyState*>(buffer + kSnapshotHeaderSize);
    const AuraResultCode bodyResult = instance->ApplyStates(bodyBuffer, header.bodyCount);
    if (bodyResult != AURA_SUCCESS)
        return bodyResult;

    const auto* characterBuffer = reinterpret_cast<const AuraCharacterState*>(
        buffer + kSnapshotHeaderSize + header.bodyCount * sizeof(AuraBodyState));
    instance->ApplyCharacterStates(characterBuffer, header.characterCount);

    return AURA_SUCCESS;
}

} // extern "C"
