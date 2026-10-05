#pragma once

#include "aura/aura_types.h"

#include <algorithm>
#include <tuple>
#include <vector>

namespace aura
{

/* Event ordering contract shared by every backend: after a step the pending events are sorted by
   (type, bodyA.index, bodyA.generation, bodyB.index, bodyB.generation). The key does not depend on thread
   timing, on hash-map iteration order or on the number of worker threads, so a replay of the same inputs yields the
   same event sequence. Events that compare equal are bit-identical (only type and the two bodies are filled), so the
   unstable sort cannot make them distinguishable. */
inline void SortEventsDeterministic(std::vector<AuraPhysicsEvent>& events)
{
    std::sort(events.begin(), events.end(), [](const AuraPhysicsEvent& l, const AuraPhysicsEvent& r)
    {
        return std::make_tuple(l.type, l.bodyA.index, l.bodyA.generation, l.bodyB.index, l.bodyB.generation)
            < std::make_tuple(r.type, r.bodyA.index, r.bodyA.generation, r.bodyB.index, r.bodyB.generation);
    });
}

} // namespace aura
