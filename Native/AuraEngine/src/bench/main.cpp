#include "aura/aura_abi.h"

#include <chrono>
#include <cmath>
#include <cstdint>
#include <cstdio>
#include <cstdlib>

namespace
{
constexpr int kDefaultBodies = 512;
constexpr int kDefaultTicks = 600;
constexpr int kRaycasts = 1000;
constexpr float kFixedDelta = 1.0f / 60.0f;

bool AttachDynamicBox(AuraWorldHandle world, const AuraShapeDesc& shape, AuraVec3 position, AuraBodyHandle* outBody)
{
    AuraBodyDesc desc{};
    desc.type = AURA_BODY_DYNAMIC;
    desc.layer = 0;
    desc.collisionMask = ~0ull;
    desc.mass = 1.0f;
    desc.gravityScale = 1.0f;
    desc.friction = 0.5f;
    desc.shapes = &shape;
    desc.shapeCount = 1;
    desc.initialPose.position = position;
    desc.initialPose.rotation = AuraQuat{ 0.0f, 0.0f, 0.0f, 1.0f };

    AuraEntityHandle entity{};
    if (Aura_CreateEntity(world, &entity) != AURA_SUCCESS)
        return false;
    return Aura_AttachBody(world, entity, &desc, outBody) == AURA_SUCCESS;
}

bool SpawnPyramid(AuraWorldHandle world, const AuraShapeDesc& shape, int count)
{
    int levels = 0;
    while (true)
    {
        const long next = static_cast<long>(levels + 1) * (levels + 2) * (2 * (levels + 1) + 1) / 6;
        if (next > count)
            break;
        ++levels;
    }

    int placed = 0;
    for (int level = 0; level < levels; ++level)
    {
        const int side = levels - level;
        const float y = 0.5f + static_cast<float>(level);
        for (int row = 0; row < side; ++row)
        {
            for (int col = 0; col < side; ++col)
            {
                const AuraVec3 position{ static_cast<float>(col) - (side - 1) * 0.5f,
                                         y,
                                         static_cast<float>(row) - (side - 1) * 0.5f };
                AuraBodyHandle body{};
                if (!AttachDynamicBox(world, shape, position, &body))
                    return false;
                ++placed;
            }
        }
    }

    const float topY = 0.5f + static_cast<float>(levels);
    for (; placed < count; ++placed)
    {
        const AuraVec3 position{ static_cast<float>(placed) - levels, topY, 0.0f };
        AuraBodyHandle body{};
        if (!AttachDynamicBox(world, shape, position, &body))
            return false;
    }

    return true;
}

bool CreateGround(AuraWorldHandle world)
{
    AuraShapeDesc shape{};
    shape.type = AURA_SHAPE_BOX;
    shape.halfExtents = AuraVec3{ 50.0f, 0.5f, 50.0f };
    shape.friction = 0.5f;

    AuraBodyDesc desc{};
    desc.type = AURA_BODY_STATIC;
    desc.layer = 0;
    desc.collisionMask = ~0ull;
    desc.mass = 1.0f;
    desc.friction = 0.5f;
    desc.shapes = &shape;
    desc.shapeCount = 1;
    desc.initialPose.position = AuraVec3{ 0.0f, -0.5f, 0.0f };
    desc.initialPose.rotation = AuraQuat{ 0.0f, 0.0f, 0.0f, 1.0f };

    AuraEntityHandle entity{};
    AuraBodyHandle body{};
    if (Aura_CreateEntity(world, &entity) != AURA_SUCCESS)
        return false;
    return Aura_AttachBody(world, entity, &desc, &body) == AURA_SUCCESS;
}
} // namespace

int main(int argc, char** argv)
{
    int bodyCount = kDefaultBodies;
    if (argc > 1)
    {
        const long parsed = std::strtol(argv[1], nullptr, 10);
        if (parsed > 0 && parsed <= 20000)
            bodyCount = static_cast<int>(parsed);
    }

    if (Aura_CheckAbi(AURA_ENGINE_ABI_VERSION) != AURA_SUCCESS)
    {
        std::printf("ABI mismatch\nAURA_BENCH_FAIL\n");
        return 1;
    }

    AuraWorldDesc worldDesc{};
    worldDesc.mode = AURA_MODE_FULL_3D;
    worldDesc.gravity = AuraVec3{ 0.0f, -9.81f, 0.0f };
    worldDesc.initialBodyCapacity = static_cast<uint32_t>(bodyCount + 8);
    worldDesc.fixedDeltaTime = kFixedDelta;

    AuraWorldHandle world{};
    if (Aura_CreateWorld(&worldDesc, &world) != AURA_SUCCESS)
    {
        std::printf("AURA_BENCH_FAIL\n");
        return 1;
    }

    AuraShapeDesc boxShape{};
    boxShape.type = AURA_SHAPE_BOX;
    boxShape.halfExtents = AuraVec3{ 0.5f, 0.5f, 0.5f };
    boxShape.friction = 0.5f;

    const bool built = CreateGround(world) && SpawnPyramid(world, boxShape, bodyCount);

    uint32_t totalBodies = 0;
    Aura_WorldBodyCount(world, &totalBodies);

    if (!built || totalBodies != static_cast<uint32_t>(bodyCount + 1))
    {
        std::printf("AURA_BENCH_FAIL\n");
        Aura_DestroyWorld(world);
        return 1;
    }

    const auto stepStart = std::chrono::steady_clock::now();
    for (int tick = 0; tick < kDefaultTicks; ++tick)
    {
        if (Aura_Step(world, static_cast<AuraTick>(tick), kFixedDelta) != AURA_SUCCESS)
        {
            std::printf("AURA_BENCH_FAIL\n");
            Aura_DestroyWorld(world);
            return 1;
        }
    }
    const auto stepEnd = std::chrono::steady_clock::now();
    const double stepSeconds = std::chrono::duration<double>(stepEnd - stepStart).count();
    const double stepMs = stepSeconds * 1000.0;
    const double bodiesPerSecond = stepSeconds > 0.0
        ? static_cast<double>(bodyCount) * kDefaultTicks / stepSeconds
        : 0.0;

    AuraQueryFilter filter{};
    filter.layerMask = ~0ull;

    int raycastHits = 0;
    const auto rayStart = std::chrono::steady_clock::now();
    for (int i = 0; i < kRaycasts; ++i)
    {
        const float angle = static_cast<float>(i) * 0.05f;
        AuraRay ray{};
        ray.origin = AuraVec3{ std::cos(angle) * 30.0f, 40.0f, std::sin(angle) * 30.0f };
        ray.direction = AuraVec3{ -std::cos(angle), -1.0f, -std::sin(angle) };
        AuraQueryHit hit{};
        uint8_t hasHit = 0;
        if (Aura_Raycast(world, &ray, 200.0f, &filter, &hit, &hasHit) != AURA_SUCCESS)
        {
            std::printf("AURA_BENCH_FAIL\n");
            Aura_DestroyWorld(world);
            return 1;
        }
        if (hasHit)
            ++raycastHits;
    }
    const auto rayEnd = std::chrono::steady_clock::now();
    const double raySeconds = std::chrono::duration<double>(rayEnd - rayStart).count();
    const double raysPerSecond = raySeconds > 0.0 ? kRaycasts / raySeconds : 0.0;

    std::printf("bench bodies=%d ticks=%d step_ms=%.3f step_bodies_per_sec=%.0f raycasts=%d raycast_rays_per_sec=%.0f\n",
                bodyCount, kDefaultTicks, stepMs, bodiesPerSecond, kRaycasts, raysPerSecond);
    std::printf("bench_details raycast_hits=%d total_bodies=%u\n", raycastHits, totalBodies);

    Aura_DestroyWorld(world);

    const bool ok = raycastHits > 0;
    std::printf(ok ? "AURA_BENCH_OK\n" : "AURA_BENCH_FAIL\n");
    return ok ? 0 : 1;
}
