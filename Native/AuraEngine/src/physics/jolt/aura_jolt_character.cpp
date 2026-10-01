#include "aura_jolt_internal.h"

namespace aura
{

/* Jolt CharacterVirtual capsule controller. Slope/step/jump/push refinements
   belong in this translation unit. */
AuraResultCode JoltWorld::CreateCharacter(const AuraCharacterDesc& desc, uint64_t* outCharacter)
{
    if (outCharacter == nullptr)
        return AURA_INVALID_HANDLE;

    const float radius = desc.radius > 0.0f ? desc.radius : 0.5f;
    const float height = desc.height > 0.0f ? desc.height : 1.8f;
    const float halfHeight = std::max(0.0f, height * 0.5f - radius);
    JPH::RefConst<JPH::Shape> shape = new JPH::CapsuleShape(halfHeight, radius);
    if (shape == nullptr)
        return AURA_OUT_OF_MEMORY;

    JPH::CharacterVirtualSettings settings;
    settings.mShape = shape;
    settings.mUp = JPH::Vec3::sAxisY();
    settings.mMaxSlopeAngle = desc.maxSlopeAngle > 0.0f ? desc.maxSlopeAngle : JPH::DegreesToRadians(50.0f);
    settings.mMass = desc.mass > 0.0f ? desc.mass : 70.0f;

    JPH::CharacterVirtual* character = new JPH::CharacterVirtual(&settings, ToRVec3(desc.pose.position), ToQuat(desc.pose.rotation), &impl_->physics);
    if (character == nullptr)
        return AURA_OUT_OF_MEMORY;

    int index;
    if (!impl_->freeCharacterSlots.empty())
    {
        index = impl_->freeCharacterSlots.back();
        impl_->freeCharacterSlots.pop_back();
    }
    else
    {
        index = static_cast<int>(impl_->characterSlots.size());
        impl_->characterSlots.emplace_back();
    }

    Impl::CharacterSlot& slot = impl_->characterSlots[index];
    slot.occupied = true;
    slot.character = character;
    slot.layer = desc.layer;
    *outCharacter = Impl::MakeCharacterHandle(slot, index);
    return AURA_SUCCESS;
}

AuraResultCode JoltWorld::DestroyCharacter(uint64_t character)
{
    Impl::CharacterSlot* slot = impl_->FindCharacter(character);
    if (slot == nullptr)
        return AURA_INVALID_HANDLE;

    slot->character = nullptr;
    slot->occupied = false;
    slot->generation += 1;
    impl_->freeCharacterSlots.push_back(static_cast<int>(character & 0xFFFFFFFFull));
    return AURA_SUCCESS;
}

AuraResultCode JoltWorld::GetCharacterState(uint64_t character, AuraCharacterState* outState) const
{
    const Impl::CharacterSlot* slot = impl_->FindCharacter(character);
    if (slot == nullptr || outState == nullptr || slot->character == nullptr)
        return AURA_INVALID_HANDLE;

    const JPH::RVec3 position = slot->character->GetPosition();
    const JPH::Vec3 velocity = slot->character->GetLinearVelocity();
    outState->position = AuraVec3{ static_cast<float>(position.GetX()), static_cast<float>(position.GetY()), static_cast<float>(position.GetZ()) };
    outState->velocity = AuraVec3{ velocity.GetX(), velocity.GetY(), velocity.GetZ() };
    outState->isGrounded = slot->character->IsSupported() ? 1 : 0;
    return AURA_SUCCESS;
}

AuraResultCode JoltWorld::MoveCharacter(uint64_t character, const AuraVec3& desiredTranslation, float deltaTime)
{
    Impl::CharacterSlot* slot = impl_->FindCharacter(character);
    if (slot == nullptr || slot->character == nullptr)
        return AURA_INVALID_HANDLE;

    const float dt = deltaTime > 0.0f ? deltaTime : impl_->lastDelta;
    JPH::CharacterVirtual* virtualCharacter = slot->character;
    const JPH::Vec3 gravity = ToVec3(impl_->gravity);

    JPH::Vec3 velocity = virtualCharacter->GetLinearVelocity();
    const JPH::Vec3 desired = ToVec3(desiredTranslation) / dt;
    velocity.SetX(desired.GetX());
    velocity.SetZ(desired.GetZ());

    /* A positive vertical component is an explicit up command (jump), so it
       overrides gravity for that step. Otherwise clamp downward motion when
       supported and integrate gravity while airborne. */
    if (desired.GetY() > 0.0f)
        velocity.SetY(desired.GetY());
    else if (virtualCharacter->IsSupported())
        velocity.SetY(std::min(velocity.GetY(), 0.0f));
    else
        velocity.SetY(velocity.GetY() + gravity.GetY() * dt);

    virtualCharacter->SetLinearVelocity(velocity);

    JPH::CharacterVirtual::ExtendedUpdateSettings updateSettings;
    updateSettings.mStickToFloorStepDown = JPH::Vec3(0.0f, -0.5f, 0.0f);
    updateSettings.mWalkStairsStepUp = JPH::Vec3(0.0f, 0.6f, 0.0f);
    updateSettings.mWalkStairsMinStepForward = 0.35f;
    updateSettings.mWalkStairsStepForwardTest = 0.2f;
    virtualCharacter->ExtendedUpdate(
        dt,
        gravity,
        updateSettings,
        impl_->physics.GetDefaultBroadPhaseLayerFilter(slot->layer),
        impl_->physics.GetDefaultLayerFilter(slot->layer),
        JPH::BodyFilter(),
        JPH::ShapeFilter(),
        impl_->tempAllocator);
    return AURA_SUCCESS;
}

uint32_t JoltWorld::CharacterCount() const
{
    uint32_t count = 0;
    for (const Impl::CharacterSlot& slot : impl_->characterSlots)
        if (slot.occupied && slot.character != nullptr)
            ++count;
    return count;
}

uint32_t JoltWorld::CopyCharacterStates(AuraCharacterState* buffer, uint32_t capacity) const
{
    uint32_t written = 0;
    for (const Impl::CharacterSlot& slot : impl_->characterSlots)
    {
        if (written >= capacity)
            break;
        if (!slot.occupied || slot.character == nullptr)
            continue;

        const JPH::RVec3 position = slot.character->GetPosition();
        const JPH::Vec3 velocity = slot.character->GetLinearVelocity();
        AuraCharacterState& state = buffer[written++];
        state.position = AuraVec3{ static_cast<float>(position.GetX()), static_cast<float>(position.GetY()), static_cast<float>(position.GetZ()) };
        state.velocity = AuraVec3{ velocity.GetX(), velocity.GetY(), velocity.GetZ() };
        state.isGrounded = slot.character->IsSupported() ? 1 : 0;
    }
    return written;
}

AuraResultCode JoltWorld::ApplyCharacterStates(const AuraCharacterState* states, uint32_t count)
{
    uint32_t applied = 0;
    for (Impl::CharacterSlot& slot : impl_->characterSlots)
    {
        if (applied >= count)
            break;
        if (!slot.occupied || slot.character == nullptr)
            continue;

        const AuraCharacterState& state = states[applied++];
        slot.character->SetPosition(ToRVec3(state.position));
        slot.character->SetLinearVelocity(ToVec3(state.velocity));
    }
    return AURA_SUCCESS;
}

} // namespace aura
