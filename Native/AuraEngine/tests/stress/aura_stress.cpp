// Thread-safety / undefined-behaviour stress driver for libaura, built by run_stress.sh with
// -fsanitize=thread or -fsanitize=undefined. It only uses the public C ABI (like src/headless/main.cpp) and drives
// the Jolt contact path (multi-threaded job system), Box2D, conveyors (surface velocity), force fields, characters,
// joints and body churn on hundreds of bodies, optionally from several caller threads on separate worlds.
//
// usage: aura_stress [steps=400] [seed=1] [mode=all|jolt|box2d|multiworld]

#include "aura/aura_abi.h"

#include <cmath>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <string>
#include <thread>
#include <vector>

namespace
{
struct Rng
{
    uint64_t s;
    explicit Rng(uint64_t seed) : s(seed * 0x9E3779B97F4A7C15ull + 1) {}
    uint64_t Next() { s ^= s << 13; s ^= s >> 7; s ^= s << 17; return s; }
    float F(float lo, float hi) { return lo + (hi - lo) * static_cast<float>((Next() >> 40) * (1.0 / 16777216.0)); }
    int I(int n) { return static_cast<int>(Next() % static_cast<uint64_t>(n)); }
};

const AuraQuat kIdentity{ 0.0f, 0.0f, 0.0f, 1.0f };
const AuraBodyHandle kNoBody{ 0xFFFFFFFFu, 0xFFFFFFFFu };

struct Failure
{
    int count = 0;
    void Check(AuraResultCode code, const char* what)
    {
        if (code != AURA_SUCCESS && std::getenv("AURA_STRESS_VERBOSE") != nullptr)
            std::fprintf(stderr, "  %s -> %d\n", what, static_cast<int>(code));
    }
};

AuraShapeDesc Shape(AuraShapeType type, float a, float b, float c)
{
    AuraShapeDesc s{};
    s.type = type;
    s.localPose.rotation = kIdentity;
    s.friction = 0.6f;
    s.restitution = 0.1f;
    s.density = 1.0f;
    s.halfExtents = AuraVec3{ a, b, c };
    s.radius = a;
    s.height = b * 2.0f;
    s.topRadius = a * 0.5f;
    s.planeNormal = AuraVec3{ 0.0f, 1.0f, 0.0f };
    s.shapeFilterMask = 0xFFFFFFFFu;
    return s;
}

AuraBodyHandle Spawn(AuraWorldHandle world, AuraBodyType type, const AuraShapeDesc& shape, AuraVec3 pos, bool is2D, uint32_t layer = 0)
{
    AuraBodyDesc d{};
    d.type = type;
    d.layer = layer;
    d.collisionMask = ~0ull;
    d.mass = 1.0f;
    d.gravityScale = 1.0f;
    d.friction = 0.6f;
    d.restitution = 0.1f;
    d.density = 1.0f;
    d.initialPose.position = is2D ? AuraVec3{ pos.x, pos.y, 0.0f } : pos;
    d.initialPose.rotation = kIdentity;
    d.shapes = &shape;
    d.shapeCount = 1;
    d.allowSleeping = 1;
    AuraEntityHandle e{};
    AuraBodyHandle h{ kNoBody };
    Aura_AttachBody(world, e, &d, &h);
    return h;
}

AuraJointDesc BaseJoint(AuraJointType type, AuraBodyHandle a, AuraBodyHandle b)
{
    AuraJointDesc j{};
    j.type = type;
    j.bodyA = a;
    j.bodyB = b;
    j.anchorA = AuraVec3{ 0.6f, 0.0f, 0.0f };
    j.anchorB = AuraVec3{ -0.6f, 0.0f, 0.0f };
    j.axisA = AuraVec3{ 0.0f, 0.0f, 1.0f };
    j.axisB = AuraVec3{ 0.0f, 0.0f, 1.0f };
    j.normalAxisA = AuraVec3{ 0.0f, 1.0f, 0.0f };
    j.normalAxisB = AuraVec3{ 0.0f, 1.0f, 0.0f };
    j.distance = 1.2f;
    j.minLimit = -0.5f;
    j.maxLimit = 0.5f;
    j.swingLimit = 0.6f;
    j.springFrequency = 2.0f;
    j.springDamping = 0.5f;
    j.ratio = 1.0f;
    j.jointRefA = AuraJointRef{ 0xFFFFFFFFu, 0xFFFFFFFFu };
    j.jointRefB = j.jointRefA;
    return j;
}

AuraForceFieldDesc Field(Rng& rng, int kind, bool is2D)
{
    AuraForceFieldDesc f{};
    f.shape = rng.I(2);
    f.kind = kind;
    f.mode = rng.I(2);
    f.falloff = rng.I(3);
    f.pose.position = AuraVec3{ rng.F(-8, 8), rng.F(1, 6), is2D ? 0.0f : rng.F(-8, 8) };
    f.pose.rotation = kIdentity;
    f.halfExtents = AuraVec3{ rng.F(2, 6), rng.F(2, 6), rng.F(2, 6) };
    f.radius = rng.F(2, 8);
    f.vector = AuraVec3{ rng.F(-6, 6), rng.F(-2, 6), is2D ? 0.0f : rng.F(-6, 6) };
    f.strength = rng.F(0.5f, 6.0f);
    f.minRadius = 0.5f;
    f.maxRadius = rng.I(2) ? 0.0f : rng.F(3, 10);
    f.layerMask = ~0ull;
    f.enabled = 1;
    return f;
}

// One world with a few hundred bodies and every contact-path feature the kernel supports for the mode.
void RunScenario(bool is2D, int steps, uint64_t seed, int bodyCount, const char* label)
{
    Rng rng(seed);
    Failure fail;
    AuraWorldDesc wd{};
    wd.mode = is2D ? AURA_MODE_PLANE_2D : AURA_MODE_FULL_3D;
    wd.gravity = AuraVec3{ 0.0f, -9.81f, 0.0f };
    wd.initialBodyCapacity = 64;
    wd.fixedDeltaTime = 1.0f / 60.0f;
    AuraWorldHandle world{};
    if (Aura_CreateWorld(&wd, &world) != AURA_SUCCESS || world.opaque == 0)
    {
        std::fprintf(stderr, "[%s] CreateWorld failed\n", label);
        return;
    }

    AuraShapeDesc ground = Shape(AURA_SHAPE_BOX, 40.0f, 1.0f, 40.0f);
    Spawn(world, AURA_BODY_STATIC, ground, AuraVec3{ 0.0f, -1.0f, 0.0f }, is2D);
    AuraShapeDesc wall = Shape(AURA_SHAPE_BOX, 1.0f, 12.0f, 40.0f);
    Spawn(world, AURA_BODY_STATIC, wall, AuraVec3{ -22.0f, 8.0f, 0.0f }, is2D);
    Spawn(world, AURA_BODY_STATIC, wall, AuraVec3{ 22.0f, 8.0f, 0.0f }, is2D);

    // Conveyor belts: static and kinematic bodies with a surface velocity, re-driven while bodies slide over them.
    std::vector<AuraBodyHandle> belts;
    for (int i = 0; i < 6; ++i)
    {
        AuraShapeDesc beltShape = Shape(AURA_SHAPE_BOX, 5.0f, 0.3f, 4.0f);
        const bool kinematic = (i % 2) == 1;
        AuraBodyHandle belt = Spawn(world, kinematic ? AURA_BODY_KINEMATIC : AURA_BODY_STATIC, beltShape,
                                    AuraVec3{ -15.0f + 6.0f * static_cast<float>(i), 2.0f + 0.3f * static_cast<float>(i % 3), 0.0f }, is2D);
        belts.push_back(belt);
        Aura_SetSurfaceVelocity(world, belt, AuraVec3{ rng.F(-3, 3), 0.0f, 0.0f });
    }

    std::vector<AuraBodyHandle> bodies;
    for (int i = 0; i < bodyCount; ++i)
    {
        const int kind = rng.I(is2D ? 3 : 4);
        AuraShapeDesc s = kind == 0 ? Shape(AURA_SHAPE_BOX, 0.4f, 0.4f, 0.4f)
                        : kind == 1 ? Shape(AURA_SHAPE_SPHERE, 0.4f, 0.4f, 0.4f)
                        : kind == 2 ? Shape(AURA_SHAPE_CAPSULE, 0.3f, 0.4f, 0.3f)
                                    : Shape(AURA_SHAPE_CYLINDER, 0.4f, 0.4f, 0.4f);
        const float x = -18.0f + 1.1f * static_cast<float>(i % 32);
        const float y = 4.0f + 1.0f * static_cast<float>(i / 32) + rng.F(0.0f, 0.4f);
        const float z = is2D ? 0.0f : rng.F(-3.0f, 3.0f);
        bodies.push_back(Spawn(world, AURA_BODY_DYNAMIC, s, AuraVec3{ x, y, z }, is2D, static_cast<uint32_t>(rng.I(4))));
    }

    // Joints: hinge chain, distance, fixed, slider, spring, rope/wheel (2D), cone/six-dof (3D), with breakable thresholds.
    std::vector<uint64_t> joints;
    for (int i = 0; i + 1 < 40 && i + 1 < static_cast<int>(bodies.size()); ++i)
    {
        AuraJointType types3[] = { AURA_JOINT_HINGE, AURA_JOINT_DISTANCE, AURA_JOINT_FIXED, AURA_JOINT_SLIDER, AURA_JOINT_SPRING, AURA_JOINT_POINT, AURA_JOINT_CONE, AURA_JOINT_SIX_DOF, AURA_JOINT_SWING_TWIST };
        AuraJointType types2[] = { AURA_JOINT_HINGE, AURA_JOINT_DISTANCE, AURA_JOINT_FIXED, AURA_JOINT_SLIDER, AURA_JOINT_SPRING, AURA_JOINT_POINT, AURA_JOINT_ROPE, AURA_JOINT_WHEEL, AURA_JOINT_MOUSE };
        const AuraJointType type = is2D ? types2[i % 9] : types3[i % 9];
        AuraJointDesc j = BaseJoint(type, bodies[static_cast<size_t>(i)], bodies[static_cast<size_t>(i + 1)]);
        if (type == AURA_JOINT_MOUSE)
        {
            j.bodyA = kNoBody;
            j.anchorB = AuraVec3{ 0.0f, 6.0f, 0.0f };
            j.maxMotorForce = 400.0f;
        }

        if (type == AURA_JOINT_SIX_DOF)
            for (int a = 0; a < 6; ++a) { j.axes[a].mode = AURA_JOINT_AXIS_LIMITED; j.axes[a].minLimit = -0.3f; j.axes[a].maxLimit = 0.3f; }
        uint64_t handle = 0;
        if (Aura_CreateJoint(world, &j, &handle) == AURA_SUCCESS)
        {
            joints.push_back(handle);
            Aura_SetJointBreakThreshold(world, handle, rng.F(20.0f, 300.0f), rng.F(20.0f, 300.0f));
        }
    }

    std::vector<uint64_t> characters;
    for (int i = 0; i < 8; ++i)
    {
        AuraCharacterDesc c{};
        c.pose.position = AuraVec3{ -10.0f + 3.0f * static_cast<float>(i), 8.0f, is2D ? 0.0f : rng.F(-3, 3) };
        c.pose.rotation = kIdentity;
        c.radius = 0.4f;
        c.height = 1.6f;
        c.mass = 70.0f;
        c.maxSlopeAngle = 0.8f;
        c.layer = 1;
        c.collisionMask = ~0ull;
        c.stepHeight = 0.3f;
        uint64_t h = 0;
        if (Aura_CreateCharacter(world, &c, &h) == AURA_SUCCESS)
            characters.push_back(h);
    }

    std::vector<AuraForceFieldHandle> fields;
    for (int i = 0; i < 4; ++i)
    {
        AuraForceFieldDesc f = Field(rng, i % 3, is2D);
        AuraForceFieldHandle h{};
        if (Aura_CreateForceField(world, &f, &h) == AURA_SUCCESS)
            fields.push_back(h);
    }

    std::vector<AuraContact> contacts(512);
    std::vector<AuraPhysicsEvent> events(512);
    std::vector<AuraBodyState> states(1024);
    std::vector<AuraQueryHit> hits(32);
    AuraQueryFilter filter{};
    filter.layerMask = ~0ull;
    filter.shapeFilterMask = 0xFFFFFFFFu;

    for (int step = 0; step < steps; ++step)
    {
        fail.Check(Aura_Step(world, static_cast<AuraTick>(step), 1.0f / 60.0f), "Step");

        if (step % 15 == 0)
            for (AuraBodyHandle belt : belts)
                Aura_SetSurfaceVelocity(world, belt, AuraVec3{ rng.F(-4, 4), 0.0f, is2D ? 0.0f : rng.F(-1, 1) });
        if (step % 10 == 0 && !fields.empty())
        {
            AuraForceFieldDesc f = Field(rng, rng.I(3), is2D);
            Aura_UpdateForceField(world, fields[static_cast<size_t>(rng.I(static_cast<int>(fields.size())))], &f);
        }

        if (step % 50 == 25 && !fields.empty())
        {
            Aura_DestroyForceField(world, fields.back());
            fields.pop_back();
            AuraForceFieldDesc f = Field(rng, rng.I(3), is2D);
            AuraForceFieldHandle h{};
            if (Aura_CreateForceField(world, &f, &h) == AURA_SUCCESS)
                fields.push_back(h);
        }

        for (uint64_t c : characters)
            Aura_MoveCharacter(world, c, AuraVec3{ rng.F(-3, 3), -2.0f, is2D ? 0.0f : rng.F(-3, 3) }, 1.0f / 60.0f);

        uint32_t count = 0;
        Aura_CopyContacts(world, contacts.data(), static_cast<uint32_t>(contacts.size()), &count);
        Aura_CopyEvents(world, events.data(), static_cast<uint32_t>(events.size()), &count);
        Aura_CopyBodyStates(world, states.data(), static_cast<uint32_t>(states.size()), &count);

        AuraRay ray{};
        ray.origin = AuraVec3{ rng.F(-15, 15), 20.0f, 0.0f };
        ray.direction = AuraVec3{ 0.0f, -1.0f, 0.0f };
        Aura_RaycastAll(world, &ray, 50.0f, &filter, hits.data(), static_cast<uint32_t>(hits.size()), &count);
        Aura_OverlapSphere(world, AuraVec3{ rng.F(-10, 10), 4.0f, 0.0f }, 3.0f, &filter, hits.data(), static_cast<uint32_t>(hits.size()), &count);

        if (step % 5 == 0 && !bodies.empty())
        {
            const size_t k = static_cast<size_t>(rng.I(static_cast<int>(bodies.size())));
            Aura_AddImpulse(world, bodies[k], AuraVec3{ rng.F(-3, 3), rng.F(0, 6), is2D ? 0.0f : rng.F(-3, 3) });
        }

        if (step % 20 == 10 && bodies.size() > 60)
        {
            // Churn: destroy bodies (some carry joints), toggle others, and recreate, so slot reuse races would show.
            for (int n = 0; n < 4; ++n)
            {
                const size_t k = static_cast<size_t>(rng.I(static_cast<int>(bodies.size())));
                Aura_DestroyBody(world, bodies[k]);
                AuraShapeDesc s = Shape(AURA_SHAPE_SPHERE, 0.4f, 0.4f, 0.4f);
                bodies[k] = Spawn(world, AURA_BODY_DYNAMIC, s, AuraVec3{ rng.F(-10, 10), 12.0f, is2D ? 0.0f : rng.F(-3, 3) }, is2D);
            }

            const size_t k = static_cast<size_t>(rng.I(static_cast<int>(bodies.size())));
            Aura_SetBodyEnabled(world, bodies[k], 0);
            Aura_SetBodyEnabled(world, bodies[k], 1);
        }

        if (step % 40 == 30 && !joints.empty())
        {
            const uint64_t j = joints[static_cast<size_t>(rng.I(static_cast<int>(joints.size())))];
            AuraJointFeedback fb{};
            Aura_GetJointFeedback(world, j, &fb);
            AuraJointMotorDesc m{};
            m.mode = AURA_JOINT_MOTOR_VELOCITY;
            m.target = rng.F(-2, 2);
            m.maxForce = 50.0f;
            Aura_SetJointMotor(world, j, &m);
        }
    }

    uint64_t hash = 0;
    Aura_ComputeStateHash(world, &hash);
    std::printf("[%s] %d steps, %zu bodies, %zu joints, %zu characters, hash=%016llx\n", label, steps, bodies.size(), joints.size(),
                characters.size(), static_cast<unsigned long long>(hash));
    Aura_DestroyWorld(world);
}
} // namespace

int main(int argc, char** argv)
{
    const int steps = argc > 1 ? std::atoi(argv[1]) : 400;
    const uint64_t seed = argc > 2 ? static_cast<uint64_t>(std::strtoull(argv[2], nullptr, 10)) : 1;
    const std::string mode = argc > 3 ? argv[3] : "all";

    if (mode == "all" || mode == "jolt")
        RunScenario(false, steps, seed, 320, "jolt-3d");
    if (mode == "all" || mode == "box2d")
        RunScenario(true, steps, seed, 320, "box2d-2d");

    if (mode == "all" || mode == "multiworld")
    {
        // Separate worlds stepped concurrently from several caller threads; the first creations race on global init.
        std::vector<std::thread> threads;
        for (int t = 0; t < 4; ++t)
        {
            threads.emplace_back([=]() {
                char label[32];
                std::snprintf(label, sizeof(label), "multiworld-%s-%d", (t % 2) ? "2d" : "3d", t);
                RunScenario((t % 2) != 0, steps / 2, seed + static_cast<uint64_t>(t), 160, label);
            });
        }

        for (std::thread& thread : threads)
            thread.join();
    }

    std::printf("AURA_STRESS_DONE live worlds=%u\n", Aura_LiveWorldCount());
    return 0;
}
