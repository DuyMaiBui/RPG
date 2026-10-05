// AI-agent soak/stress finding repro (not part of any build). Aura_SetKinematicTarget on a plane (also mesh / height field) body segfaults: such bodies are created without Jolt motion properties (aura_jolt_world.cpp:146-155)
// and SetKinematicTarget (aura_jolt_world.cpp:262-275) calls MoveKinematic without checking slot->canChangeMotion.
// usage: repro 6   (plane, crashes with exit 139), repro 0 (box, fine)
// build: clang++ -std=c++17 -I../../../include repro_kinematic_target_on_static_only_body_crash.cpp -L<build-dir> -laura -Wl,-rpath,<build-dir>  (any libaura built from this tree, Jolt + Box2D)
#include "aura/aura_abi.h"
#include <cstdio>
#include <cstdlib>
int main(int argc, char** argv)
{
    int shapeType = argc > 1 ? atoi(argv[1]) : 6;
    AuraWorldDesc wd{}; wd.mode = AURA_MODE_FULL_3D; wd.gravity = {0,-9.81f,0}; wd.initialBodyCapacity = 4; wd.fixedDeltaTime = 1/60.f;
    AuraWorldHandle w{}; Aura_CreateWorld(&wd, &w);
    AuraShapeDesc s{}; s.type = (AuraShapeType)shapeType; s.halfExtents = {0.5f,0.5f,0.5f}; s.density = 1; s.friction = 0.5f; s.localPose.rotation = {0,0,0,1};
    s.planeNormal = {0,1,0};
    AuraBodyDesc b{}; b.type = AURA_BODY_STATIC; b.collisionMask = ~0ull; b.mass = 1; b.gravityScale = 1; b.density = 1;
    b.shapes = &s; b.shapeCount = 1; b.initialPose.rotation = {0,0,0,1};
    AuraEntityHandle e{}; AuraBodyHandle h{};
    int rc = Aura_AttachBody(w, e, &b, &h); std::printf("attach=%d\n", rc);
    Aura_Step(w, 1, 1/60.f);
    AuraPose p{}; p.position = {3,4,5}; p.rotation = {0,0,0,1};
    std::printf("about to SetKinematicTarget on static shape type %d\n", shapeType); std::fflush(stdout);
    rc = Aura_SetKinematicTarget(w, h, &p);
    std::printf("setTarget=%d\n", rc);
    Aura_DestroyWorld(w);
}
