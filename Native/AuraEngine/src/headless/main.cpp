#include "aura/aura_abi.h"

#include <cmath>
#include <cstdint>
#include <cstdio>
#include <vector>

namespace
{
bool RunPlane2D()
{
    AuraWorldDesc worldDesc{};
    worldDesc.mode = AURA_MODE_PLANE_2D;
    worldDesc.gravity = AuraVec3{ 0.0f, -9.81f, 0.0f };
    worldDesc.initialBodyCapacity = 8;
    worldDesc.fixedDeltaTime = 1.0f / 60.0f;

    AuraWorldHandle world{};
    if (Aura_CreateWorld(&worldDesc, &world) != AURA_SUCCESS)
        return false;

    AuraShapeDesc groundShape{};
    groundShape.type = AURA_SHAPE_BOX;
    groundShape.halfExtents = AuraVec3{ 10.0f, 0.5f, 1.0f };
    groundShape.friction = 0.5f;

    AuraShapeDesc ballShape{};
    ballShape.type = AURA_SHAPE_SPHERE;
    ballShape.radius = 0.5f;
    ballShape.friction = 0.5f;

    AuraBodyDesc groundDesc{};
    groundDesc.type = AURA_BODY_STATIC;
    groundDesc.layer = 0;
    groundDesc.collisionMask = ~0ull;
    groundDesc.mass = 1.0f;
    groundDesc.shapes = &groundShape;
    groundDesc.shapeCount = 1;
    groundDesc.initialPose.position = AuraVec3{ 0.0f, -0.5f, 0.0f };
    groundDesc.initialPose.rotation = AuraQuat{ 0.0f, 0.0f, 0.0f, 1.0f };

    AuraBodyDesc ballDesc{};
    ballDesc.type = AURA_BODY_DYNAMIC;
    ballDesc.layer = 0;
    ballDesc.collisionMask = ~0ull;
    ballDesc.mass = 1.0f;
    ballDesc.gravityScale = 1.0f;
    ballDesc.friction = 0.5f;
    ballDesc.shapes = &ballShape;
    ballDesc.shapeCount = 1;
    ballDesc.initialPose.position = AuraVec3{ 0.0f, 3.0f, 0.0f };
    ballDesc.initialPose.rotation = AuraQuat{ 0.0f, 0.0f, 0.0f, 1.0f };

    AuraEntityHandle entity{};
    AuraBodyHandle ground{};
    AuraBodyHandle ball{};
    Aura_CreateEntity(world, &entity);
    Aura_AttachBody(world, entity, &groundDesc, &ground);
    Aura_CreateEntity(world, &entity);
    Aura_AttachBody(world, entity, &ballDesc, &ball);

    for (int tick = 0; tick < 300; ++tick)
        Aura_Step(world, static_cast<AuraTick>(tick), 1.0f / 60.0f);

    AuraBodyState state{};
    Aura_GetBodyState(world, ball, &state);
    std::printf("[2d] ball y=%.4f\n", state.pose.position.y);

    AuraRay ray{};
    ray.origin = AuraVec3{ 0.0f, 6.0f, 0.0f };
    ray.direction = AuraVec3{ 0.0f, -1.0f, 0.0f };
    AuraQueryFilter filter{};
    filter.layerMask = ~0ull;
    AuraQueryHit hit{};
    uint8_t hasHit = 0;
    Aura_Raycast(world, &ray, 100.0f, &filter, &hit, &hasHit);
    std::printf("[2d] raycast hit=%u distance=%.4f\n", static_cast<unsigned>(hasHit), hit.distance);

    Aura_DestroyWorld(world);
    return std::fabs(state.pose.position.y - 0.5f) <= 0.25f && hasHit != 0;
}

/* Character controller smoke test. Only the Jolt backend implements
   characters; the reference/Box2D backends report it as unsupported, in which
   case the test is skipped so the headless binary stays green everywhere. */
bool RunCharacter()
{
    AuraWorldDesc worldDesc{};
    worldDesc.mode = AURA_MODE_FULL_3D;
    worldDesc.gravity = AuraVec3{ 0.0f, -9.81f, 0.0f };
    worldDesc.initialBodyCapacity = 8;
    worldDesc.fixedDeltaTime = 1.0f / 60.0f;

    AuraWorldHandle world{};
    if (Aura_CreateWorld(&worldDesc, &world) != AURA_SUCCESS)
        return false;

    AuraShapeDesc groundShape{};
    groundShape.type = AURA_SHAPE_BOX;
    groundShape.halfExtents = AuraVec3{ 10.0f, 0.5f, 10.0f };

    AuraShapeDesc stepShape{};
    stepShape.type = AURA_SHAPE_BOX;
    stepShape.halfExtents = AuraVec3{ 0.5f, 0.15f, 0.5f };

    AuraBodyDesc groundDesc{};
    groundDesc.type = AURA_BODY_STATIC;
    groundDesc.collisionMask = ~0ull;
    groundDesc.shapes = &groundShape;
    groundDesc.shapeCount = 1;
    groundDesc.initialPose.position = AuraVec3{ 0.0f, -0.5f, 0.0f };
    groundDesc.initialPose.rotation = AuraQuat{ 0.0f, 0.0f, 0.0f, 1.0f };

    AuraBodyDesc stepDesc = groundDesc;
    stepDesc.shapes = &stepShape;
    stepDesc.initialPose.position = AuraVec3{ 2.0f, 0.15f, 0.0f };

    AuraEntityHandle entity{};
    AuraBodyHandle ground{};
    Aura_CreateEntity(world, &entity);
    Aura_AttachBody(world, entity, &groundDesc, &ground);
    Aura_CreateEntity(world, &entity);
    Aura_AttachBody(world, entity, &stepDesc, &ground);

    const float dt = 1.0f / 60.0f;
    AuraCharacterDesc characterDesc{};
    characterDesc.pose.position = AuraVec3{ 0.0f, 1.5f, 0.0f };
    characterDesc.pose.rotation = AuraQuat{ 0.0f, 0.0f, 0.0f, 1.0f };
    characterDesc.radius = 0.4f;
    characterDesc.height = 1.8f;
    characterDesc.mass = 70.0f;
    characterDesc.maxSlopeAngle = 50.0f * 3.14159265f / 180.0f;
    characterDesc.collisionMask = ~0ull;

    uint64_t character = 0;
    const AuraResultCode created = Aura_CreateCharacter(world, &characterDesc, &character);
    if (created != AURA_SUCCESS)
    {
        std::printf("[char] unsupported (code=%d) skipped\n", static_cast<int>(created));
        Aura_DestroyWorld(world);
        return true;
    }

    for (int tick = 0; tick < 60; ++tick)
    {
        Aura_MoveCharacter(world, character, AuraVec3{ 0.0f, 0.0f, 0.0f }, dt);
        Aura_Step(world, static_cast<AuraTick>(tick), dt);
    }

    AuraCharacterState state{};
    Aura_GetCharacterState(world, character, &state);
    const float restingY = state.position.y;

    /* Walk into the 0.3 m step. Stair walking should lift the capsule; the
       peak during the crossing is sampled because the character steps back
       down after clearing the far edge. */
    float walkPeakY = restingY;
    for (int tick = 0; tick < 180; ++tick)
    {
        Aura_MoveCharacter(world, character, AuraVec3{ 3.0f * dt, 0.0f, 0.0f }, dt);
        Aura_Step(world, static_cast<AuraTick>(tick), dt);
        Aura_GetCharacterState(world, character, &state);
        walkPeakY = state.position.y > walkPeakY ? state.position.y : walkPeakY;
    }
    Aura_GetCharacterState(world, character, &state);
    const float steppedY = state.position.y;
    const bool climbed = walkPeakY > restingY + 0.15f;

    /* Jump: an explicit upward command then ballistic flight. */
    Aura_MoveCharacter(world, character, AuraVec3{ 0.0f, 5.0f * dt, 0.0f }, dt);
    Aura_Step(world, 0, dt);
    float peakY = steppedY;
    for (int tick = 0; tick < 30; ++tick)
    {
        Aura_MoveCharacter(world, character, AuraVec3{ 0.0f, 0.0f, 0.0f }, dt);
        Aura_Step(world, static_cast<AuraTick>(tick), dt);
        Aura_GetCharacterState(world, character, &state);
        peakY = state.position.y > peakY ? state.position.y : peakY;
    }
    const bool jumped = peakY > steppedY + 0.10f;

    Aura_GetCharacterState(world, character, &state);
    const float savedX = state.position.x;
    const float savedY = state.position.y;

    uint32_t snapshotSize = 0;
    Aura_SerializeState(world, nullptr, 0, &snapshotSize);
    bool snapshotOk = snapshotSize > 0;
    if (snapshotOk)
    {
        std::vector<uint8_t> snapshot(snapshotSize);
        uint32_t written = 0;
        snapshotOk = Aura_SerializeState(world, snapshot.data(), snapshotSize, &written) == AURA_SUCCESS;
        if (snapshotOk)
        {
            for (int tick = 0; tick < 30; ++tick)
            {
                Aura_MoveCharacter(world, character, AuraVec3{ 4.0f * dt, 0.0f, 0.0f }, dt);
                Aura_Step(world, static_cast<AuraTick>(tick), dt);
            }

            snapshotOk = Aura_DeserializeState(world, snapshot.data(), written) == AURA_SUCCESS;
            Aura_GetCharacterState(world, character, &state);
            snapshotOk = snapshotOk
                && std::fabs(state.position.x - savedX) < 0.01f
                && std::fabs(state.position.y - savedY) < 0.01f;
        }
    }

    std::printf("[char] rest_y=%.4f walk_peak_y=%.4f stepped_y=%.4f jump_peak_y=%.4f climbed=%u jumped=%u snapshot=%u\n",
        restingY, walkPeakY, steppedY, peakY, static_cast<unsigned>(climbed), static_cast<unsigned>(jumped), static_cast<unsigned>(snapshotOk));

    Aura_DestroyWorld(world);
    return climbed && jumped && snapshotOk;
}

/* Height field: a flat 4x4 field at y = 1 must catch a falling box. Only the
   Jolt backend implements it; other backends report it unsupported and the
   check is skipped. */
bool RunHeightField()
{
    const uint32_t resolution = 4;
    const float samples[resolution * resolution] = {
        1.0f, 1.0f, 1.0f, 1.0f,
        1.0f, 1.0f, 1.0f, 1.0f,
        1.0f, 1.0f, 1.0f, 1.0f,
        1.0f, 1.0f, 1.0f, 1.0f,
    };

    AuraWorldDesc worldDesc{};
    worldDesc.mode = AURA_MODE_FULL_3D;
    worldDesc.gravity = AuraVec3{ 0.0f, -9.81f, 0.0f };
    worldDesc.initialBodyCapacity = 8;
    worldDesc.fixedDeltaTime = 1.0f / 60.0f;

    AuraWorldHandle world{};
    if (Aura_CreateWorld(&worldDesc, &world) != AURA_SUCCESS)
        return false;

    AuraShapeDesc fieldShape{};
    fieldShape.type = AURA_SHAPE_HEIGHT_FIELD;
    fieldShape.halfExtents = AuraVec3{ 2.0f, 1.0f, 2.0f };
    fieldShape.vertices = samples;
    fieldShape.vertexCount = resolution * resolution;

    AuraShapeDesc boxShape{};
    boxShape.type = AURA_SHAPE_BOX;
    boxShape.halfExtents = AuraVec3{ 0.5f, 0.5f, 0.5f };

    AuraBodyDesc fieldDesc{};
    fieldDesc.type = AURA_BODY_STATIC;
    fieldDesc.collisionMask = ~0ull;
    fieldDesc.shapes = &fieldShape;
    fieldDesc.shapeCount = 1;
    fieldDesc.initialPose.rotation = AuraQuat{ 0.0f, 0.0f, 0.0f, 1.0f };

    AuraBodyDesc boxDesc{};
    boxDesc.type = AURA_BODY_DYNAMIC;
    boxDesc.collisionMask = ~0ull;
    boxDesc.mass = 1.0f;
    boxDesc.gravityScale = 1.0f;
    boxDesc.shapes = &boxShape;
    boxDesc.shapeCount = 1;
    boxDesc.initialPose.position = AuraVec3{ 3.0f, 5.0f, 3.0f };
    boxDesc.initialPose.rotation = AuraQuat{ 0.0f, 0.0f, 0.0f, 1.0f };

    AuraEntityHandle entity{};
    AuraBodyHandle field{};
    Aura_CreateEntity(world, &entity);
    const AuraResultCode created = Aura_AttachBody(world, entity, &fieldDesc, &field);
    if (created != AURA_SUCCESS)
    {
        std::printf("[hf] unsupported (code=%d) skipped\n", static_cast<int>(created));
        Aura_DestroyWorld(world);
        return true;
    }

    AuraBodyHandle box{};
    Aura_CreateEntity(world, &entity);
    Aura_AttachBody(world, entity, &boxDesc, &box);

    for (int tick = 0; tick < 240; ++tick)
        Aura_Step(world, static_cast<AuraTick>(tick), 1.0f / 60.0f);

    AuraBodyState state{};
    Aura_GetBodyState(world, box, &state);
    const bool ok = std::fabs(state.pose.position.y - 1.5f) < 0.25f;
    std::printf("[hf] box_y=%.4f ok=%u\n", state.pose.position.y, static_cast<unsigned>(ok));

    Aura_DestroyWorld(world);
    return ok;
}

/* Raycast filtering: layerMask selects the object layer and flags bit 2 plus
   ignoredBody excludes one body. Both the reference and Jolt backends must
   agree. */
bool RunQueryFilter()
{
    AuraWorldDesc worldDesc{};
    worldDesc.mode = AURA_MODE_FULL_3D;
    worldDesc.gravity = AuraVec3{ 0.0f, -9.81f, 0.0f };
    worldDesc.initialBodyCapacity = 8;
    worldDesc.fixedDeltaTime = 1.0f / 60.0f;

    AuraWorldHandle world{};
    if (Aura_CreateWorld(&worldDesc, &world) != AURA_SUCCESS)
        return false;

    AuraShapeDesc groundShape{};
    groundShape.type = AURA_SHAPE_BOX;
    groundShape.halfExtents = AuraVec3{ 10.0f, 0.5f, 10.0f };

    AuraShapeDesc platformShape{};
    platformShape.type = AURA_SHAPE_BOX;
    platformShape.halfExtents = AuraVec3{ 0.5f, 0.5f, 0.5f };

    AuraBodyDesc groundDesc{};
    groundDesc.type = AURA_BODY_STATIC;
    groundDesc.layer = 0;
    groundDesc.collisionMask = ~0ull;
    groundDesc.shapes = &groundShape;
    groundDesc.shapeCount = 1;
    groundDesc.initialPose.position = AuraVec3{ 0.0f, -0.5f, 0.0f };
    groundDesc.initialPose.rotation = AuraQuat{ 0.0f, 0.0f, 0.0f, 1.0f };

    AuraBodyDesc platformDesc = groundDesc;
    platformDesc.layer = 1;
    platformDesc.shapes = &platformShape;
    platformDesc.initialPose.position = AuraVec3{ 0.0f, 3.0f, 0.0f };

    AuraEntityHandle entity{};
    AuraBodyHandle ground{};
    AuraBodyHandle platform{};
    Aura_CreateEntity(world, &entity);
    Aura_AttachBody(world, entity, &groundDesc, &ground);
    Aura_CreateEntity(world, &entity);
    Aura_AttachBody(world, entity, &platformDesc, &platform);

    AuraRay ray{};
    ray.origin = AuraVec3{ 0.0f, 10.0f, 0.0f };
    ray.direction = AuraVec3{ 0.0f, -1.0f, 0.0f };

    AuraQueryHit hit{};
    uint8_t has = 0;

    AuraQueryFilter groundOnly{};
    groundOnly.layerMask = 1ull << 0;
    Aura_Raycast(world, &ray, 100.0f, &groundOnly, &hit, &has);
    const float groundDistance = hit.distance;
    const bool hitGround = has != 0 && hit.body.index == ground.index;

    AuraQueryFilter platformOnly{};
    platformOnly.layerMask = 1ull << 1;
    Aura_Raycast(world, &ray, 100.0f, &platformOnly, &hit, &has);
    const float platformDistance = hit.distance;
    const bool hitPlatform = has != 0 && hit.body.index == platform.index;

    AuraQueryFilter ignorePlatform{};
    ignorePlatform.layerMask = ~0ull;
    ignorePlatform.flags = 4;
    ignorePlatform.ignoredBody = platform;
    Aura_Raycast(world, &ray, 100.0f, &ignorePlatform, &hit, &has);
    const float ignoredDistance = hit.distance;
    const bool hitGroundOnly = has != 0 && hit.body.index == ground.index;

    AuraQueryHit overlaps[8]{};
    AuraQueryFilter overlapGround{};
    overlapGround.layerMask = 1ull << 0;
    uint32_t overlapGroundCount = 0;
    Aura_OverlapSphere(world, AuraVec3{ 0.0f, 3.0f, 0.0f }, 0.6f, &overlapGround, overlaps, 8, &overlapGroundCount);

    AuraQueryFilter overlapPlatform{};
    overlapPlatform.layerMask = 1ull << 1;
    uint32_t overlapPlatformCount = 0;
    Aura_OverlapSphere(world, AuraVec3{ 0.0f, 3.0f, 0.0f }, 0.6f, &overlapPlatform, overlaps, 8, &overlapPlatformCount);

    const bool ok = hitGround && hitPlatform && hitGroundOnly
        && std::fabs(groundDistance - 10.0f) < 0.05f
        && std::fabs(platformDistance - 6.5f) < 0.05f
        && std::fabs(ignoredDistance - 10.0f) < 0.05f
        && overlapGroundCount == 0 && overlapPlatformCount == 1;

    std::printf("[query] ground_d=%.3f platform_d=%.3f ignored_d=%.3f layer0=%u layer1=%u ignore=%u overlap0=%u overlap1=%u\n",
        groundDistance, platformDistance, ignoredDistance,
        static_cast<unsigned>(hitGround), static_cast<unsigned>(hitPlatform), static_cast<unsigned>(hitGroundOnly),
        overlapGroundCount, overlapPlatformCount);

    Aura_DestroyWorld(world);
    return ok;
}
} // namespace

int main()
{
    bool ok = true;

    if (Aura_CheckAbi(AURA_ENGINE_ABI_VERSION) != AURA_SUCCESS)
    {
        std::printf("ABI mismatch\n");
        return 1;
    }

    AuraShapeDesc groundShape{};
    groundShape.type = AURA_SHAPE_BOX;
    groundShape.halfExtents = AuraVec3{ 10.0f, 0.5f, 10.0f };
    groundShape.friction = 0.5f;

    AuraShapeDesc ballShape{};
    ballShape.type = AURA_SHAPE_SPHERE;
    ballShape.radius = 0.5f;
    ballShape.friction = 0.5f;

    AuraWorldDesc worldDesc{};
    worldDesc.mode = AURA_MODE_FULL_3D;
    worldDesc.gravity = AuraVec3{ 0.0f, -9.81f, 0.0f };
    worldDesc.initialBodyCapacity = 8;
    worldDesc.fixedDeltaTime = 1.0f / 60.0f;

    AuraWorldHandle world{};
    if (Aura_CreateWorld(&worldDesc, &world) != AURA_SUCCESS)
        return 1;

    AuraBodyDesc groundDesc{};
    groundDesc.type = AURA_BODY_STATIC;
    groundDesc.layer = 0;
    groundDesc.collisionMask = ~0ull;
    groundDesc.mass = 1.0f;
    groundDesc.friction = 0.5f;
    groundDesc.shapes = &groundShape;
    groundDesc.shapeCount = 1;
    groundDesc.initialPose.position = AuraVec3{ 0.0f, -0.5f, 0.0f };
    groundDesc.initialPose.rotation = AuraQuat{ 0.0f, 0.0f, 0.0f, 1.0f };

    AuraBodyDesc ballDesc{};
    ballDesc.type = AURA_BODY_DYNAMIC;
    ballDesc.layer = 0;
    ballDesc.collisionMask = ~0ull;
    ballDesc.mass = 1.0f;
    ballDesc.gravityScale = 1.0f;
    ballDesc.friction = 0.5f;
    ballDesc.shapes = &ballShape;
    ballDesc.shapeCount = 1;
    ballDesc.initialPose.position = AuraVec3{ 0.0f, 5.0f, 0.0f };
    ballDesc.initialPose.rotation = AuraQuat{ 0.0f, 0.0f, 0.0f, 1.0f };

    AuraEntityHandle entity{};
    AuraBodyHandle ground{};
    Aura_CreateEntity(world, &entity);
    if (Aura_AttachBody(world, entity, &groundDesc, &ground) != AURA_SUCCESS)
        return 1;

    AuraBodyHandle ball{};
    Aura_CreateEntity(world, &entity);
    if (Aura_AttachBody(world, entity, &ballDesc, &ball) != AURA_SUCCESS)
        return 1;

    for (int tick = 0; tick < 300; ++tick)
        Aura_Step(world, static_cast<AuraTick>(tick), 1.0f / 60.0f);

    AuraBodyState state{};
    Aura_GetBodyState(world, ball, &state);
    std::printf("ball y=%.4f\n", state.pose.position.y);
    ok = ok && std::fabs(state.pose.position.y - 0.5f) <= 0.25f;

    AuraRay ray{};
    ray.origin = AuraVec3{ 0.0f, 10.0f, 0.0f };
    ray.direction = AuraVec3{ 0.0f, -1.0f, 0.0f };
    AuraQueryFilter filter{};
    filter.layerMask = ~0ull;
    AuraQueryHit hit{};
    uint8_t hasHit = 0;
    Aura_Raycast(world, &ray, 100.0f, &filter, &hit, &hasHit);
    std::printf("raycast hit=%u distance=%.4f\n", static_cast<unsigned>(hasHit), hit.distance);
    ok = ok && hasHit != 0;

    uint32_t bodyCount = 0;
    Aura_WorldBodyCount(world, &bodyCount);
    std::printf("bodies=%u\n", bodyCount);
    ok = ok && bodyCount == 2;

    uint64_t hash = 0;
    Aura_ComputeStateHash(world, &hash);
    std::printf("state hash=0x%016llx\n", static_cast<unsigned long long>(hash));

    uint32_t snapshotSize = 0;
    Aura_SerializeState(world, nullptr, 0, &snapshotSize);
    if (snapshotSize > 0)
    {
        std::vector<uint8_t> snapshot(snapshotSize);
        uint32_t written = 0;
        const AuraResultCode ser = Aura_SerializeState(world, snapshot.data(), snapshotSize, &written);
        const AuraResultCode des = ser == AURA_SUCCESS
            ? Aura_DeserializeState(world, snapshot.data(), written)
            : ser;
        std::printf("snapshot size=%u written=%u serialize=%d deserialize=%d\n",
            snapshotSize, written, static_cast<int>(ser), static_cast<int>(des));
        ok = ok && ser == AURA_SUCCESS && des == AURA_SUCCESS && written == snapshotSize;
    }

    Aura_DestroyWorld(world);

    ok = ok && RunPlane2D();
    ok = ok && RunHeightField();
    ok = ok && RunQueryFilter();
    ok = ok && RunCharacter();

    std::printf(ok ? "AURA_HEADLESS_OK\n" : "AURA_HEADLESS_FAIL\n");
    return ok ? 0 : 1;
}
