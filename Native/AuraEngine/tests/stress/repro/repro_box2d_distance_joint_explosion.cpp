// AI-agent soak/stress finding repro (not part of any build). Box2D (Plane2D) distance joint (type 0) and rope joint (type 12) between two dynamic circles: random anchors/length explode (|v| > 1e4, up to 1e20).
// usage: repro 0 -> about 13% of 400 random configurations; repro 12 -> about 1%.
// build: clang++ -std=c++17 -I../../../include repro_box2d_distance_joint_explosion.cpp -L<build-dir> -laura -Wl,-rpath,<build-dir>  (any libaura built from this tree, Jolt + Box2D)
#include "aura/aura_abi.h"
#include <cstdio>
#include <cstdlib>
#include <cmath>
static float R(float lo, float hi) { return lo + (hi - lo) * (rand() / (float)RAND_MAX); }
int main(int argc, char** argv)
{
    int jointType = argc > 1 ? atoi(argv[1]) : AURA_JOINT_DISTANCE;
    int bad = 0;
    for (int trial = 0; trial < 400; ++trial)
    {
        AuraWorldDesc wd{}; wd.mode = AURA_MODE_PLANE_2D; wd.gravity = {0,-9.81f,0}; wd.initialBodyCapacity = 4; wd.fixedDeltaTime = 1/60.f;
        AuraWorldHandle w{}; Aura_CreateWorld(&wd, &w);
        AuraShapeDesc s{}; s.type = AURA_SHAPE_SPHERE; s.radius = 0.5f; s.density = 1; s.friction = 0.5f; s.localPose.rotation = {0,0,0,1};
        AuraBodyDesc b{}; b.type = AURA_BODY_DYNAMIC; b.collisionMask = ~0ull; b.mass = 1; b.gravityScale = 1; b.density = 1;
        b.shapes = &s; b.shapeCount = 1; b.initialPose.rotation = {0,0,0,1};
        AuraEntityHandle e{}; AuraBodyHandle h1{}, h2{};
        b.initialPose.position = {R(-1,1),R(0,3),0}; Aura_AttachBody(w, e, &b, &h1);
        b.initialPose.position = {R(-1,1),R(0,3),0}; Aura_AttachBody(w, e, &b, &h2);
        AuraJointDesc jd{}; jd.type = jointType; jd.bodyA = h1; jd.bodyB = h2;
        jd.anchorA = {R(-1,1),R(-1,1),0}; jd.anchorB = {R(-1,1),R(-1,1),0};
        jd.jointRefA = {0xFFFFFFFFu,0xFFFFFFFFu}; jd.jointRefB = jd.jointRefA; jd.distance = R(0.2f,3);
        jd.springFrequency = R(0,8); jd.springDamping = R(0,1); jd.minLimit = R(-2,0); jd.maxLimit = R(0,2); jd.enableLimit = rand()%2;
        uint64_t j = 0; int rc = Aura_CreateJoint(w, &jd, &j);
        float maxV = 0;
        for (int i = 0; i < 400; ++i)
        {
            Aura_Step(w, i, 1/60.f);
            AuraBodyState st{}; Aura_GetBodyState(w, h1, &st);
            float v = std::fabs(st.linearVelocity.x) + std::fabs(st.linearVelocity.y);
            if (!(v <= maxV)) maxV = v;
        }
        if (!(maxV < 1e4f))
        {
            if (bad++ < 2) std::printf("EXPLODE type %d create=%d dist=%g freq=%g damp=%g anchors=(%g,%g)/(%g,%g) lim=%d[%g,%g] maxV=%g\n", jointType, rc, jd.distance, jd.springFrequency, jd.springDamping, jd.anchorA.x, jd.anchorA.y, jd.anchorB.x, jd.anchorB.y, jd.enableLimit, jd.minLimit, jd.maxLimit, maxV);
        }
        Aura_DestroyWorld(w);
    }
    std::printf("joint %d: %d/400 exploded\n", jointType, bad);
}
