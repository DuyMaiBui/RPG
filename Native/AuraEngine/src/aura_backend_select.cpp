#include "aura_world.h"
#include "aura_reference_world.h"

#if defined(AURA_USE_JOLT)
#include "physics/jolt/aura_jolt_world.h"
#endif
#if defined(AURA_USE_BOX2D)
#include "physics/box2d/aura_box2d_world.h"
#endif

namespace aura
{

IWorld* CreateWorldImpl(const AuraWorldDesc& desc)
{
    if (desc.mode == AURA_MODE_PLANE_2D)
    {
#if defined(AURA_USE_BOX2D)
        return new Box2DWorld(desc);
#endif
    }

#if defined(AURA_USE_JOLT)
    if (desc.mode != AURA_MODE_PLANE_2D)
        return new JoltWorld(desc);
#endif

    return new ReferenceWorld(desc);
}

} // namespace aura
