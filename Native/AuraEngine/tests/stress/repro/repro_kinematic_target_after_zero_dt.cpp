// AI-agent soak/stress finding repro (not part of any build). Aura_SetKinematicTarget after Aura_Step(dt = 0) passes lastDelta = 0 to Jolt MoveKinematic (aura_jolt_world.cpp:272): the body ends at inf/NaN.
// Expected output: lines with stepZero=1 show pos=inf or nan.
// build: clang++ -std=c++17 -I../../../include repro_kinematic_target_after_zero_dt.cpp -L<build-dir> -laura -Wl,-rpath,<build-dir>  (any libaura built from this tree, Jolt + Box2D)
#include "aura/aura_abi.h"
#include <cstdio>
#include <cstring>
static void Try(int bodyType, bool stepZero, bool stepFirst)
{
    AuraWorldDesc wd{}; wd.mode = AURA_MODE_FULL_3D; wd.gravity = {0,-9.81f,0}; wd.initialBodyCapacity = 4; wd.fixedDeltaTime = 1/60.f;
    AuraWorldHandle w{}; Aura_CreateWorld(&wd, &w);
    AuraShapeDesc s{}; s.type = AURA_SHAPE_BOX; s.halfExtents = {0.5f,0.5f,0.5f}; s.density = 1; s.friction = 0.5f;
    s.localPose.rotation = {0,0,0,1};
    AuraBodyDesc b{}; b.type = (AuraBodyType)bodyType; b.collisionMask = ~0ull; b.mass = 1; b.gravityScale = 1; b.density = 1;
    b.shapes = &s; b.shapeCount = 1; b.initialPose.rotation = {0,0,0,1}; b.initialPose.position = {0,5,0};
    AuraEntityHandle e{}; AuraBodyHandle h{};
    Aura_AttachBody(w, e, &b, &h);
    if (stepFirst) Aura_Step(w, 1, 1/60.f);
    if (stepZero) Aura_Step(w, 2, 0.0f);
    AuraPose p{}; p.position = {3,4,5}; p.rotation = {0,0,0,1};
    int rc = Aura_SetKinematicTarget(w, h, &p);
    Aura_Step(w, 3, 1/60.f);
    AuraBodyState st{}; Aura_GetBodyState(w, h, &st);
    std::printf("type=%d stepFirst=%d stepZero=%d setTarget=%d pos=%g,%g,%g lin=%g,%g,%g\n", bodyType, stepFirst, stepZero, rc,
        st.pose.position.x, st.pose.position.y, st.pose.position.z, st.linearVelocity.x, st.linearVelocity.y, st.linearVelocity.z);
    Aura_DestroyWorld(w);
}
int main()
{
    Try(2, false, false); Try(2, false, true); Try(2, true, true); Try(1, false, true); Try(1, true, true); Try(0,false,true);
}
