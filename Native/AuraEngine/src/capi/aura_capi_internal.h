#pragma once

#include "aura/aura_abi.h"

#include "aura_world.h"

namespace aura
{

inline IWorld* ToWorld(AuraWorldHandle handle)
{
    return reinterpret_cast<IWorld*>(handle.opaque);
}

} // namespace aura
