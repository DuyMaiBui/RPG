#include "aura_jolt_softbody.h"

#include <cmath>
#include <new>

namespace aura::jolt_internal
{

AuraJoltSoftBodyOwner::AuraJoltSoftBodyOwner(JPH::BodyInterface& bodyInterface,
                                             JPH::RVec3Arg position,
                                             JPH::QuatArg rotation,
                                             JPH::ObjectLayer objectLayer)
    : bodyInterface_(&bodyInterface),
      position_(position),
      rotation_(rotation),
      objectLayer_(objectLayer)
{
}

AuraJoltSoftBodyOwner::~AuraJoltSoftBodyOwner()
{
    Destroy();
}

bool AuraJoltSoftBodyOwner::Create(const float* vertexPositions,
                                   uint32_t vertexCount,
                                   const uint32_t* faceIndices,
                                   uint32_t faceCount,
                                   const float* inverseMass)
{
    if (created_ || bodyInterface_ == nullptr || vertexPositions == nullptr
        || faceIndices == nullptr || vertexCount < 3 || vertexCount > kMaxVertices
        || faceCount == 0 || faceCount > kMaxFaces)
        return false;

    for (uint32_t vertex = 0; vertex < vertexCount; ++vertex)
    {
        const float* position = vertexPositions + vertex * 3u;
        if (!std::isfinite(position[0]) || !std::isfinite(position[1]) || !std::isfinite(position[2]))
            return false;
        if (inverseMass != nullptr && (!std::isfinite(inverseMass[vertex]) || inverseMass[vertex] < 0.0f))
            return false;
    }

    for (uint32_t face = 0; face < faceCount; ++face)
    {
        const uint32_t* indices = faceIndices + face * 3u;
        if (indices[0] >= vertexCount || indices[1] >= vertexCount || indices[2] >= vertexCount
            || indices[0] == indices[1] || indices[0] == indices[2] || indices[1] == indices[2])
            return false;

        for (uint32_t edge = 0; edge < 3; ++edge)
        {
            const float* first = vertexPositions + indices[edge] * 3u;
            const float* second = vertexPositions + indices[(edge + 1u) % 3u] * 3u;
            const float dx = second[0] - first[0];
            const float dy = second[1] - first[1];
            const float dz = second[2] - first[2];
            if (!std::isfinite(dx) || !std::isfinite(dy) || !std::isfinite(dz)
                || dx * dx + dy * dy + dz * dz <= 0.0f)
                return false;
        }
    }

    try
    {
        JPH::Ref<JPH::SoftBodySharedSettings> shared = new JPH::SoftBodySharedSettings();
        shared->mVertices.reserve(vertexCount);
        shared->mFaces.reserve(faceCount);

        for (uint32_t vertex = 0; vertex < vertexCount; ++vertex)
        {
            const float* position = vertexPositions + vertex * 3u;
            const float mass = inverseMass != nullptr ? inverseMass[vertex] : 1.0f;
            shared->mVertices.emplace_back(JPH::Float3(position[0], position[1], position[2]),
                                            JPH::Float3(0.0f, 0.0f, 0.0f), mass);
        }
        for (uint32_t face = 0; face < faceCount; ++face)
        {
            const uint32_t* indices = faceIndices + face * 3u;
            shared->AddFace(JPH::SoftBodySharedSettings::Face(indices[0], indices[1], indices[2]));
        }

        JPH::SoftBodySharedSettings::VertexAttributes attributes;
        shared->CreateConstraints(&attributes, 1, JPH::SoftBodySharedSettings::EBendType::Distance);
        JPH::SoftBodyCreationSettings settings(shared.GetPtr(), position_, rotation_, objectLayer_);
        const JPH::BodyID id = bodyInterface_->CreateAndAddSoftBody(settings, JPH::EActivation::Activate);
        if (id.IsInvalid())
            return false;

        bodyID_ = id;
        created_ = true;
        return true;
    }
    catch (const std::bad_alloc&)
    {
        return false;
    }
}

void AuraJoltSoftBodyOwner::Destroy()
{
    if (!created_ || bodyInterface_ == nullptr)
        return;

    if (bodyInterface_->IsAdded(bodyID_))
        bodyInterface_->RemoveBody(bodyID_);
    bodyInterface_->DestroyBody(bodyID_);
    bodyID_ = JPH::BodyID();
    created_ = false;
}

} // namespace aura::jolt_internal
