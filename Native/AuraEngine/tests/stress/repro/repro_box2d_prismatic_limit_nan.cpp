// AI-agent soak/stress finding repro (not part of any build). Box2D (Plane2D) slider joint with enableLimit between a dynamic body and a static/kinematic body that starts far outside the limit range: NaN pose within 3 steps.
// usage: LIM=1 repro 4 0 0 1 0   -> about 27% of 3000 random configurations give NaN; LIM=0 gives 0.
// build: clang++ -std=c++17 -I../../../include repro_box2d_prismatic_limit_nan.cpp -L<build-dir> -laura -Wl,-rpath,<build-dir>  (any libaura built from this tree, Jolt + Box2D)
#include "aura/aura_abi.h"
#include <cstdio>
#include <cstdlib>
#include <cmath>
static float R(float lo, float hi) { return lo + (hi - lo) * (rand() / (float)RAND_MAX); }
int main(int argc, char** argv)
{
    int jointType = argc > 1 ? atoi(argv[1]) : AURA_JOINT_SLIDER;
    int bType = argc > 2 ? atoi(argv[2]) : AURA_BODY_KINEMATIC;
    int bad = 0; float velScale = argc > 3 ? atof(argv[3]) : 1; float sep = argc > 4 ? atof(argv[4]) : 1; float spin = argc > 5 ? atof(argv[5]) : 1;
    for (int trial = 0; trial < 3000; ++trial)
    {
        AuraWorldDesc wd{}; wd.mode = AURA_MODE_PLANE_2D; wd.gravity = {0,-9.81f,0}; wd.initialBodyCapacity = 4; wd.fixedDeltaTime = 1/60.f;
        AuraWorldHandle w{}; Aura_CreateWorld(&wd, &w);
        AuraShapeDesc s{}; s.type = AURA_SHAPE_SPHERE; s.radius = 0.5f; s.density = 1; s.friction = 0.5f; s.localPose.rotation = {0,0,0,1};
        AuraBodyDesc b{}; b.type = AURA_BODY_DYNAMIC; b.collisionMask = ~0ull; b.mass = 1; b.gravityScale = 1; b.density = 1;
        b.shapes = &s; b.shapeCount = 1;
        AuraEntityHandle e{}; AuraBodyHandle h1{}, h2{};
        b.initialPose.rotation = {0,0,sinf(-0.25f),cosf(-0.25f)}; b.initialPose.position = {-14.5f*sep,-17.0f*sep,0}; b.initialLinearVelocity = {-10.6f*velScale,-114.7f*velScale,0}; b.initialAngularVelocity = {0,0,3.4f*spin};
        Aura_AttachBody(w, e, &b, &h1);
        b.type = (AuraBodyType)bType; b.initialPose.rotation = {0,0,0.018f,0.9998f}; b.initialPose.position = {5.28f,-6.47f,0}; b.initialLinearVelocity = {1.55f*velScale,3.29f*velScale,0}; b.initialAngularVelocity = {0,0,3.43f*spin};
        Aura_AttachBody(w, e, &b, &h2);
        AuraJointDesc jd{}; jd.type = jointType; jd.bodyA = h1; jd.bodyB = h2;
        jd.anchorA = {R(-1,1),R(-1,1),R(-1,1)}; jd.anchorB = {R(-1,1),R(-1,1),R(-1,1)};
        float v[3] = {R(-1,1),R(-1,1),R(-1,1)}; float l = sqrtf(v[0]*v[0]+v[1]*v[1]+v[2]*v[2]);
        jd.axisA = {v[0]/l,v[1]/l,v[2]/l}; jd.axisB = jd.axisA; jd.normalAxisA = {0,1,0}; jd.normalAxisB = {0,1,0};
        jd.jointRefA = {0xFFFFFFFFu,0xFFFFFFFFu}; jd.jointRefB = jd.jointRefA; jd.distance = R(0.2f,3);
        jd.minLimit = R(-2,0); jd.maxLimit = R(0,2); jd.enableLimit = getenv("LIM") ? atoi(getenv("LIM")) : rand()%2;
        uint64_t j = 0; int rc = Aura_CreateJoint(w, &jd, &j);
        for (int i = 0; i < 3; ++i) Aura_Step(w, i, 1/60.f);
        AuraBodyState st{}; Aura_GetBodyState(w, h1, &st);
        if (!std::isfinite(st.pose.position.x) || !std::isfinite(st.pose.position.y))
        {
            if (bad++ < 0) std::printf("NaN: joint %d create=%d anchorA=(%g,%g) anchorB=(%g,%g) axis=(%g,%g,%g) dist=%g limit=%d [%g,%g]\n", jointType, rc, jd.anchorA.x, jd.anchorA.y, jd.anchorB.x, jd.anchorB.y, jd.axisA.x, jd.axisA.y, jd.axisA.z, jd.distance, jd.enableLimit, jd.minLimit, jd.maxLimit);
        }
        Aura_DestroyWorld(w);
    }
    std::printf("joint %d: %d/3000 NaN\n", jointType, bad);
}
