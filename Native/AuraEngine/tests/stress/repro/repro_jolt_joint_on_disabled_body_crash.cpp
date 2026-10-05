// AI-agent soak/stress finding repro (not part of any build). Aura_CreateJoint on a body disabled with Aura_SetBodyEnabled(false) is accepted, the next Aura_Step segfaults in JPH::QuadTree::NotifyBodiesAABBChanged.
// usage: repro <jointType> <order>   order 0 = disable before the joint (crashes, exit 139), 1 = disable after the joint (fine, constraint is disabled),
//        3/4/5 = destroy bodies after the joint (fine).
// build: clang++ -std=c++17 -I../../../include repro_jolt_joint_on_disabled_body_crash.cpp -L<build-dir> -laura -Wl,-rpath,<build-dir>  (any libaura built from this tree, Jolt + Box2D)
#include "aura/aura_abi.h"
#include <cstdio>
#include <cstdlib>
int main(int argc, char** argv)
{
    int jointType = argc > 1 ? atoi(argv[1]) : AURA_JOINT_SWING_TWIST;
    AuraWorldDesc wd{}; wd.mode = AURA_MODE_FULL_3D; wd.gravity = {0,-9.81f,0}; wd.initialBodyCapacity = 4; wd.fixedDeltaTime = 1/60.f;
    AuraWorldHandle w{}; Aura_CreateWorld(&wd, &w);
    AuraShapeDesc s{}; s.type = AURA_SHAPE_BOX; s.halfExtents = {0.5f,0.5f,0.5f}; s.density = 1; s.friction = 0.5f; s.localPose.rotation = {0,0,0,1};
    AuraBodyDesc b{}; b.type = AURA_BODY_DYNAMIC; b.collisionMask = ~0ull; b.mass = 1; b.gravityScale = 1; b.density = 1;
    b.shapes = &s; b.shapeCount = 1; b.initialPose.rotation = {0,0,0,1};
    AuraEntityHandle e{}; AuraBodyHandle h1{}, h2{};
    b.initialPose.position = {0,5,0}; Aura_AttachBody(w, e, &b, &h1);
    b.initialPose.position = {2,5,0}; Aura_AttachBody(w, e, &b, &h2);
    int order = argc > 2 ? atoi(argv[2]) : 0;
    if (order == 0) std::printf("disable=%d\n", Aura_SetBodyEnabled(w, h1, 0));
    AuraJointDesc jd{}; jd.type = jointType; jd.bodyA = h1; jd.bodyB = h2; jd.anchorA = {1,0,0}; jd.anchorB = {-1,0,0};
    jd.axisA = {1,0,0}; jd.normalAxisA = {0,1,0}; jd.axisB = {1,0,0}; jd.normalAxisB = {0,1,0};
    jd.swingLimit = 0.5f; jd.minLimit = -0.5f; jd.maxLimit = 0.5f; jd.distance = 2.0f;
    jd.jointRefA = {0xFFFFFFFFu,0xFFFFFFFFu}; jd.jointRefB = jd.jointRefA;
    uint64_t j = 0; int rc = Aura_CreateJoint(w, &jd, &j); std::printf("create joint type %d = %d\n", jointType, rc);
    if (order == 1) std::printf("disable after joint=%d\n", Aura_SetBodyEnabled(w, h1, 0));
    if (order == 2) { std::printf("disable+enable: %d %d\n", Aura_SetBodyEnabled(w, h1, 0), Aura_SetBodyEnabled(w, h1, 1)); }
    if (order == 3) std::printf("destroy A=%d\n", Aura_DestroyBody(w, h1));
    if (order == 4) std::printf("destroy A=%d B=%d\n", Aura_DestroyBody(w, h1), Aura_DestroyBody(w, h2));
    if (order == 5) std::printf("destroy B=%d A=%d\n", Aura_DestroyBody(w, h2), Aura_DestroyBody(w, h1));
    std::fflush(stdout);
    for (int i = 0; i < 5; ++i) { int r = Aura_Step(w, i, 1/60.f); std::printf("step %d = %d\n", i, r); std::fflush(stdout); }
    Aura_DestroyWorld(w);
}
