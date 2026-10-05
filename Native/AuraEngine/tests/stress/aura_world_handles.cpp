// AI-agent kernel regression driver (package K): world handle validation and first-world creation race, C ABI only.
//
// usage: aura_world_handles stale | race [threads=16] [worlds=8] [backend 0=3D(Jolt) 1=2D(Box2D) 2=mixed]
//   stale: every entry point category must answer AURA_INVALID_WORLD for a destroyed, doubly destroyed, zero and
//          garbage world handle, a recycled slot must not revive the old handle, and two threads destroying the same
//          world concurrently must succeed exactly once.
//   race:  N threads create the very first world of the process at the same instant (run it as a fresh process each
//          time), step it and destroy it, repeated. A double Jolt initialisation shows up as a crash or hang.
// Exit code 0 = pass. Build against any libaura (see repro/ for the command line):
//   c++ -std=c++17 -I../../include aura_world_handles.cpp -L<dir> -laura -Wl,-rpath,<dir> -lpthread -o aura_world_handles

#include "aura/aura_abi.h"

#include <atomic>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <functional>
#include <thread>
#include <vector>

namespace
{
int g_failures = 0;

void Expect(AuraResultCode actual, AuraResultCode expected, const char* what)
{
    if (actual != expected)
    {
        std::fprintf(stderr, "FAIL %s: expected %d got %d\n", what, static_cast<int>(expected), static_cast<int>(actual));
        ++g_failures;
    }
}

AuraWorldHandle MakeWorld(AuraPhysicsMode mode)
{
    AuraWorldDesc desc{};
    desc.mode = mode;
    desc.gravity = { 0.0f, -9.81f, 0.0f };
    desc.initialBodyCapacity = 8;
    desc.fixedDeltaTime = 1.0f / 60.0f;
    AuraWorldHandle world{};
    if (Aura_CreateWorld(&desc, &world) != AURA_SUCCESS || world.opaque == 0)
    {
        std::fprintf(stderr, "FAIL CreateWorld\n");
        std::exit(2);
    }
    return world;
}

void CheckAllInvalid(AuraWorldHandle w, const char* label)
{
    std::vector<std::pair<const char*, std::function<AuraResultCode()>>> calls;
    uint32_t count = 0;
    uint8_t flag = 0;
    uint64_t id64 = 0;
    AuraBodyHandle body{ 0, 0 };
    AuraEntityHandle entity{ 0, 0 };
    AuraVec3 vec{ 0, 1, 0 };
    AuraPose pose{};
    pose.rotation = { 0, 0, 0, 1 };
    AuraShapeDesc shape{};
    shape.type = AURA_SHAPE_SPHERE;
    shape.radius = 0.5f;
    shape.density = 1.0f;
    shape.localPose.rotation = { 0, 0, 0, 1 };
    AuraBodyDesc bodyDesc{};
    bodyDesc.type = AURA_BODY_DYNAMIC;
    bodyDesc.shapes = &shape;
    bodyDesc.shapeCount = 1;
    bodyDesc.mass = 1.0f;
    bodyDesc.gravityScale = 1.0f;
    bodyDesc.collisionMask = ~0ull;
    bodyDesc.initialPose = pose;
    AuraQueryFilter filter{};
    filter.layerMask = ~0ull;
    AuraRay ray{};
    ray.origin = { 0, 5, 0 };
    ray.direction = { 0, -1, 0 };
    AuraQueryHit hit{};
    AuraBodyState bodyState{};
    AuraPhysicsEvent events[4];
    AuraContact contacts[4];
    AuraQueryHit hits[4];
    uint8_t buffer[16];
    AuraJointDesc jointDesc{};
    AuraForceFieldDesc fieldDesc{};
    AuraWaterDesc waterDesc{};
    waterDesc.density = 1.0f;
    AuraVec3 gravity{};
    uint64_t hash = 0;

    calls.push_back({ "WorldBodyCount", [&] { return Aura_WorldBodyCount(w, &count); } });
    calls.push_back({ "CreateEntity", [&] { return Aura_CreateEntity(w, &entity); } });
    calls.push_back({ "DestroyEntity", [&] { return Aura_DestroyEntity(w, entity); } });
    calls.push_back({ "IsEntityAlive", [&] { return Aura_IsEntityAlive(w, entity, &flag); } });
    calls.push_back({ "AttachBody", [&] { return Aura_AttachBody(w, entity, &bodyDesc, &body); } });
    calls.push_back({ "DestroyBody", [&] { return Aura_DestroyBody(w, body); } });
    calls.push_back({ "Step", [&] { return Aura_Step(w, 1, 1.0f / 60.0f); } });
    calls.push_back({ "CopyBodyStates", [&] { return Aura_CopyBodyStates(w, &bodyState, 1, &count); } });
    calls.push_back({ "GetBodyState", [&] { return Aura_GetBodyState(w, body, &bodyState); } });
    calls.push_back({ "SetLinearVelocity", [&] { return Aura_SetLinearVelocity(w, body, vec); } });
    calls.push_back({ "SetBodyPose", [&] { return Aura_SetBodyPose(w, body, &pose, 0); } });
    calls.push_back({ "SetKinematicTarget", [&] { return Aura_SetKinematicTarget(w, body, &pose); } });
    calls.push_back({ "PendingEventCount", [&] { return Aura_PendingEventCount(w, &count); } });
    calls.push_back({ "CopyEvents", [&] { return Aura_CopyEvents(w, events, 4, &count); } });
    calls.push_back({ "CopyContacts", [&] { return Aura_CopyContacts(w, contacts, 4, &count); } });
    calls.push_back({ "Raycast", [&] { return Aura_Raycast(w, &ray, 10.0f, &filter, &hit, &flag); } });
    calls.push_back({ "OverlapSphere", [&] { return Aura_OverlapSphere(w, vec, 1.0f, &filter, hits, 4, &count); } });
    calls.push_back({ "CreateJoint", [&] { return Aura_CreateJoint(w, &jointDesc, &id64); } });
    calls.push_back({ "DestroyJoint", [&] { return Aura_DestroyJoint(w, 1); } });
    calls.push_back({ "HasJoint", [&] { return Aura_HasJoint(w, 1, &flag); } });
    calls.push_back({ "IsJointBroken", [&] { return Aura_IsJointBroken(w, 1, &flag); } });
    calls.push_back({ "CreateForceField", [&] { AuraForceFieldHandle f{}; return Aura_CreateForceField(w, &fieldDesc, &f); } });
    calls.push_back({ "CreateWater", [&] { AuraWaterHandle h{}; return Aura_CreateWater(w, &waterDesc, &h); } });
    calls.push_back({ "SetWorldGravity", [&] { return Aura_SetWorldGravity(w, vec); } });
    calls.push_back({ "GetWorldGravity", [&] { return Aura_GetWorldGravity(w, &gravity); } });
    calls.push_back({ "ComputeStateHash", [&] { return Aura_ComputeStateHash(w, &hash); } });
    calls.push_back({ "SerializeState", [&] { return Aura_SerializeState(w, buffer, sizeof(buffer), &count); } });
    calls.push_back({ "DeserializeState", [&] { return Aura_DeserializeState(w, buffer, sizeof(buffer)); } });
    calls.push_back({ "DestroyWorld", [&] { return Aura_DestroyWorld(w); } });

    for (const auto& call : calls)
    {
        char what[96];
        std::snprintf(what, sizeof(what), "%s %s", label, call.first);
        Expect(call.second(), AURA_INVALID_WORLD, what);
    }
}

void Stale()
{
    const uint32_t before = Aura_LiveWorldCount();
    const AuraPhysicsMode modes[] = { AURA_MODE_FULL_3D, AURA_MODE_PLANE_2D };
    for (AuraPhysicsMode mode : modes)
    {
        const AuraWorldHandle world = MakeWorld(mode);
        Expect(Aura_Step(world, 1, 1.0f / 60.0f), AURA_SUCCESS, "step live world");
        Expect(Aura_DestroyWorld(world), AURA_SUCCESS, "destroy live world");
        CheckAllInvalid(world, "destroyed");

        /* The slot is recycled for the next world; the old handle must stay dead. */
        const AuraWorldHandle recycled = MakeWorld(mode);
        if (recycled.opaque == world.opaque)
        {
            std::fprintf(stderr, "FAIL a recycled world reused the exact stale handle value\n");
            ++g_failures;
        }
        CheckAllInvalid(world, "stale-after-recycle");
        Expect(Aura_Step(recycled, 2, 1.0f / 60.0f), AURA_SUCCESS, "recycled world is live");
        Expect(Aura_DestroyWorld(recycled), AURA_SUCCESS, "destroy recycled");
    }

    CheckAllInvalid(AuraWorldHandle{ 0 }, "zero");
    CheckAllInvalid(AuraWorldHandle{ 0xFFFFFFFFFFFFFFFFull }, "all-ones");
    CheckAllInvalid(AuraWorldHandle{ 0x00007FFF12345678ull }, "pointer-like");
    CheckAllInvalid(AuraWorldHandle{ 0xDEADBEEFull << 32 | 1u }, "bad-generation");
    CheckAllInvalid(AuraWorldHandle{ 0x1ull << 32 }, "slot-zero-gen-one");

    /* Concurrent destroy of one world: exactly one caller wins, nobody crashes. */
    for (int round = 0; round < 200; ++round)
    {
        const AuraWorldHandle world = MakeWorld(AURA_MODE_FULL_3D);
        std::atomic<int> winners{ 0 };
        std::atomic<int> invalid{ 0 };
        std::atomic<bool> go{ false };
        std::vector<std::thread> threads;
        for (int t = 0; t < 4; ++t)
            threads.emplace_back([&] {
                while (!go.load()) {}
                const AuraResultCode code = Aura_DestroyWorld(world);
                if (code == AURA_SUCCESS) ++winners; else if (code == AURA_INVALID_WORLD) ++invalid;
            });
        go = true;
        for (auto& thread : threads) thread.join();
        if (winners.load() != 1 || invalid.load() != 3)
        {
            std::fprintf(stderr, "FAIL concurrent destroy: winners=%d invalid=%d\n", winners.load(), invalid.load());
            ++g_failures;
            break;
        }
    }

    if (Aura_LiveWorldCount() != before)
    {
        std::fprintf(stderr, "FAIL live world count %u != %u\n", Aura_LiveWorldCount(), before);
        ++g_failures;
    }
}

void Race(int threadCount, int worldsPerThread, int modeSelect)
{
    std::atomic<int> ready{ 0 };
    std::atomic<bool> go{ false };
    std::atomic<int> bad{ 0 };
    std::vector<std::thread> threads;
    for (int t = 0; t < threadCount; ++t)
        threads.emplace_back([&, t] {
            ++ready;
            while (!go.load()) {}
            for (int i = 0; i < worldsPerThread; ++i)
            {
                AuraWorldDesc desc{};
                desc.mode = modeSelect == 0 ? AURA_MODE_FULL_3D : (modeSelect == 1 ? AURA_MODE_PLANE_2D : ((t + i) % 2 == 0 ? AURA_MODE_FULL_3D : AURA_MODE_PLANE_2D));
                desc.gravity = { 0.0f, -9.81f, 0.0f };
                desc.initialBodyCapacity = 8;
                desc.fixedDeltaTime = 1.0f / 60.0f;
                AuraWorldHandle world{};
                if (Aura_CreateWorld(&desc, &world) != AURA_SUCCESS || world.opaque == 0) { ++bad; continue; }
                if (Aura_Step(world, 1, 1.0f / 60.0f) != AURA_SUCCESS) ++bad;
                if (Aura_DestroyWorld(world) != AURA_SUCCESS) ++bad;
            }
        });
    while (ready.load() < threadCount) {}
    go = true;
    for (auto& thread : threads) thread.join();
    if (bad.load() != 0 || Aura_LiveWorldCount() != 0)
    {
        std::fprintf(stderr, "FAIL race: bad=%d live=%u\n", bad.load(), Aura_LiveWorldCount());
        ++g_failures;
    }
}
} // namespace

int main(int argc, char** argv)
{
    const char* mode = argc > 1 ? argv[1] : "stale";
    if (std::strcmp(mode, "stale") == 0)
        Stale();
    else if (std::strcmp(mode, "race") == 0)
        Race(argc > 2 ? std::atoi(argv[2]) : 16, argc > 3 ? std::atoi(argv[3]) : 8, argc > 4 ? std::atoi(argv[4]) : 0);
    else
    {
        std::fprintf(stderr, "usage: aura_world_handles stale | race [threads] [worlds] [backend]\n");
        return 2;
    }
    std::printf(g_failures == 0 ? "PASS %s\n" : "FAIL %s\n", mode);
    return g_failures == 0 ? 0 : 1;
}
