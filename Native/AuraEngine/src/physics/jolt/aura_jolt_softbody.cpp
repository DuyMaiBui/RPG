#include "aura_jolt_softbody.h"

#include <cmath>
#include <new>
#include <Jolt/Physics/Body/BodyLock.h>
#include <Jolt/Physics/SoftBody/SoftBodyMotionProperties.h>

namespace aura::jolt_internal
{

AuraJoltSoftBodyOwner::AuraJoltSoftBodyOwner(JPH::BodyInterface& bodyInterface,
                                             const JPH::BodyLockInterface& lockInterface,
                                             JPH::RVec3Arg position,
                                             JPH::QuatArg rotation,
                                             JPH::ObjectLayer objectLayer)
    : bodyInterface_(&bodyInterface),
      lockInterface_(&lockInterface),
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
        shared->Optimize();
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

uint32_t AuraJoltSoftBodyOwner::VertexCount() const
{
    if (!created_ || lockInterface_ == nullptr)
        return 0;
    const JPH::BodyLockRead lock(*lockInterface_, bodyID_);
    if (!lock.Succeeded())
        return 0;
    const auto* motion = static_cast<const JPH::SoftBodyMotionProperties*>(lock.GetBody().GetMotionProperties());
    return motion == nullptr ? 0u : static_cast<uint32_t>(motion->GetVertices().size());
}

bool AuraJoltSoftBodyOwner::CopyVertexPositions(float* output, uint32_t capacity) const
{
    if (output == nullptr || !created_ || lockInterface_ == nullptr)
        return false;
    const JPH::BodyLockRead lock(*lockInterface_, bodyID_);
    if (!lock.Succeeded())
        return false;
    const auto* motion = static_cast<const JPH::SoftBodyMotionProperties*>(lock.GetBody().GetMotionProperties());
    if (motion == nullptr || capacity < motion->GetVertices().size())
        return false;
    for (uint32_t index = 0; index < motion->GetVertices().size(); ++index)
    {
        const JPH::Vec3 position = motion->GetVertices()[index].mPosition;
        output[index * 3u] = position.GetX();
        output[index * 3u + 1u] = position.GetY();
        output[index * 3u + 2u] = position.GetZ();
    }
    return true;
}

} // namespace aura::jolt_internal
