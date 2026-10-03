#include "aura_jolt_internal.h"

namespace aura
{

AuraResultCode JoltWorld::CreateSoftBody(const AuraSoftBodyDesc& desc, AuraSoftBodyHandle* outSoftBody)
{
    if (outSoftBody == nullptr || desc.vertexPositions == nullptr || desc.faceIndices == nullptr)
        return AURA_INVALID_DEFINITION;

    int index;
    if (!impl_->freeSoftBodySlots.empty())
    {
        index = impl_->freeSoftBodySlots.back();
        impl_->freeSoftBodySlots.pop_back();
    }
    else
    {
        index = static_cast<int>(impl_->softBodySlots.size());
        impl_->softBodySlots.emplace_back();
    }

    Impl::SoftBodySlot& slot = impl_->softBodySlots[index];
    slot.softBody = std::make_unique<aura::jolt_internal::AuraJoltSoftBodyOwner>(
        impl_->physics.GetBodyInterface(), impl_->physics.GetBodyLockInterface(),
        ToRVec3(desc.initialPose.position), ToQuat(desc.initialPose.rotation), desc.objectLayer);
    if (!slot.softBody->Create(desc.vertexPositions, desc.vertexCount, desc.faceIndices, desc.faceCount, desc.inverseMass))
    {
        slot.softBody.reset();
        impl_->freeSoftBodySlots.push_back(index);
        return AURA_INVALID_DEFINITION;
    }

    slot.occupied = true;
    *outSoftBody = Impl::MakeSoftBodyHandle(slot, index);
    return AURA_SUCCESS;
}

AuraResultCode JoltWorld::DestroySoftBody(AuraSoftBodyHandle softBody)
{
    Impl::SoftBodySlot* slot = impl_->FindSoftBody(softBody);
    if (slot == nullptr)
        return AURA_INVALID_HANDLE;
    const int index = static_cast<int>(static_cast<uint32_t>(softBody.opaque));
    slot->softBody.reset();
    slot->occupied = false;
    ++slot->generation;
    impl_->freeSoftBodySlots.push_back(index);
    return AURA_SUCCESS;
}

AuraResultCode JoltWorld::GetSoftBodyState(AuraSoftBodyHandle softBody, float* vertexPositions, uint32_t vertexCapacity, AuraSoftBodyState* outState) const
{
    if (outState == nullptr)
        return AURA_INVALID_DEFINITION;
    const Impl::SoftBodySlot* slot = impl_->FindSoftBody(softBody);
    if (slot == nullptr)
        return AURA_INVALID_HANDLE;
    const uint32_t vertexCount = slot->softBody->VertexCount();
    outState->softBody = softBody;
    outState->vertexCount = vertexCount;
    outState->vertexPositions = vertexPositions;
    if (vertexPositions == nullptr || vertexCapacity < vertexCount)
        return AURA_CAPACITY_EXCEEDED;
    return slot->softBody->CopyVertexPositions(vertexPositions, vertexCapacity) ? AURA_SUCCESS : AURA_BACKEND_FAILURE;
}

} // namespace aura
