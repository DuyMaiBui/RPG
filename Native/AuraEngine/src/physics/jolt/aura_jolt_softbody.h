#pragma once

#include <Jolt/Jolt.h>
#include <Jolt/Physics/Body/BodyID.h>
#include <Jolt/Physics/Body/BodyInterface.h>
#include <Jolt/Physics/SoftBody/SoftBodySharedSettings.h>
#include <Jolt/Physics/SoftBody/SoftBodyCreationSettings.h>

#include <cstdint>

namespace aura::jolt_internal
{

/* Owns one Jolt soft body. The BodyInterface is owned by the containing
   PhysicsSystem and must outlive this object. Vertex positions are xyz-packed;
   face indices are triangle-packed. */
class AuraJoltSoftBodyOwner final
{
public:
    static constexpr uint32_t kMaxVertices = 1u << 20;
    static constexpr uint32_t kMaxFaces = 1u << 20;

    AuraJoltSoftBodyOwner(JPH::BodyInterface& bodyInterface,
                          JPH::RVec3Arg position,
                          JPH::QuatArg rotation,
                          JPH::ObjectLayer objectLayer);
    ~AuraJoltSoftBodyOwner();

    AuraJoltSoftBodyOwner(const AuraJoltSoftBodyOwner&) = delete;
    AuraJoltSoftBodyOwner& operator=(const AuraJoltSoftBodyOwner&) = delete;

    /* inverseMass may be null, in which case every vertex receives inverse
       mass 1. Values of zero create fixed vertices. */
    bool Create(const float* vertexPositions,
                uint32_t vertexCount,
                const uint32_t* faceIndices,
                uint32_t faceCount,
                const float* inverseMass = nullptr);
    void Destroy();

    bool IsCreated() const { return created_; }
    JPH::BodyID GetBodyID() const { return bodyID_; }

private:
    JPH::BodyInterface* bodyInterface_;
    JPH::RVec3 position_;
    JPH::Quat rotation_;
    JPH::ObjectLayer objectLayer_;
    JPH::BodyID bodyID_;
    bool created_ = false;
};

} // namespace aura::jolt_internal
