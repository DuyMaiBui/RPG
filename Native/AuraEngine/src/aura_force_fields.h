#pragma once

#include "aura/aura_types.h"

#include <cstdint>
#include <vector>

namespace aura
{

/* Backend-neutral force field storage and evaluation. Both kernels own one
   registry and, once per step before integration, feed it every dynamic enabled
   body in ascending body-slot order; fields are evaluated in ascending slot
   (= creation) order, so the result is deterministic. */
class ForceFieldRegistry
{
public:
    /* Everything the evaluator needs to know about one body. */
    struct BodyView
    {
        AuraVec3 position;     /* centre of mass */
        AuraVec3 velocity;
        float mass;
        float gravityScale;
        uint32_t layer;
    };

    AuraResultCode Create(const AuraForceFieldDesc& desc, AuraForceFieldHandle* outField);
    AuraResultCode Update(AuraForceFieldHandle field, const AuraForceFieldDesc& desc);
    AuraResultCode Destroy(AuraForceFieldHandle field);

    bool Empty() const { return liveCount_ == 0; }

    /* Returns the new velocity of the body after dt seconds of every field that affects it
       (equal to body.velocity when no field does). planar zeroes z (2D worlds). */
    AuraVec3 Integrate(const BodyView& body, float dt, bool planar) const;

    static bool Validate(const AuraForceFieldDesc& desc);

private:
    struct Slot
    {
        bool occupied = false;
        /* Starts at 1 so a valid handle is never zero. */
        uint32_t generation = 1;
        AuraForceFieldDesc desc{};
    };

    const Slot* Find(AuraForceFieldHandle field) const;
    Slot* Find(AuraForceFieldHandle field);

    std::vector<Slot> slots_;
    std::vector<uint32_t> freeSlots_;
    uint32_t liveCount_ = 0;
};

} // namespace aura
