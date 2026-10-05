#include "aura_box2d_internal.h"

namespace aura
{

namespace
{

/* Box2D v3.1 corrects a constraint violation with a soft bias of biasRate * C (about 60/s at the default joint tuning)
   and has no cap on it (the revolute joint even has its maxBias commented out). A limit or rest length that starts 20 m
   away therefore asks for >1000 m/s, which the lever arm of an off-centre anchor turns into huge angular velocity and
   an exploding or NaN pose within a step or two. The kernel instead feeds Box2D an eased copy of every position target
   (EaseJoint): it never lies further than kMaxCorrectionSpeed * dt from the current value, so a violation is recovered
   at a bounded speed and the solver only ever sees small errors. */
constexpr float kMaxCorrectionSpeed = 8.0f; /* m/s for lengths and translations, rad/s for hinge angles */
constexpr float kTearDistance = 6.0f;       /* anchor error of a point-like constraint beyond which the joint tears off (m) */
constexpr float kLinearSlop = 0.005f;       /* Box2D clamps every length to at least this (B2_LINEAR_SLOP) */

float RelativeAngle(const b2BodyId bodyA, const b2BodyId bodyB)
{
    return b2RelativeAngle(b2Body_GetRotation(bodyB), b2Body_GetRotation(bodyA));
}

/* The Gauss-Seidel solve of a limit row and the perpendicular row of a slider/wheel joint degenerates when an anchor
   sits far from a light body: both rows then act almost only through the body's rotation (Jacobian rows nearly
   parallel, condition number ~ r^2 m / I) and a small error grows geometrically (measured: a 0.5 m circle with an anchor
   >11 m away diverges with a limit, never without). lever ratio = r^2 m / I; a unit box with a 5 m anchor is ~30, the
   0.5 m circle at 8 m is ~500. */
constexpr float kMaxLeverRatio = 500.0f;
constexpr float kMaxRodLeverRatio = 16.0f;
constexpr float kTearLeverRatio = 4.0f * kMaxLeverRatio; /* a joint that drifts this far out of conditioning tears off */

float LeverRatio(const b2BodyId body, const b2Vec2 worldAnchor)
{
    if (b2Body_GetType(body) != b2_dynamicBody)
        return 0.0f;
    const float inertia = b2Body_GetRotationalInertia(body);
    if (inertia <= 0.0f)
        return 0.0f; /* fixed rotation: no rotational degree of freedom to be ill conditioned */
    const b2Vec2 offset = b2Sub(worldAnchor, b2Body_GetWorldCenterOfMass(body));
    return b2Dot(offset, offset) * b2Body_GetMass(body) / inertia;
}

} // namespace

AuraResultCode Box2DWorld::CreateJoint(const AuraJointDesc& desc, uint64_t* outJoint)
{
    if (outJoint == nullptr)
        return AURA_INVALID_HANDLE;

    const bool isMouse = desc.type == AURA_JOINT_MOUSE;
    Impl::Slot* slotA = impl_->Find(desc.bodyA);
    Impl::Slot* slotB = impl_->Find(desc.bodyB);
    if (slotB == nullptr || (slotA == nullptr && !(isMouse && desc.bodyA.index == 0xFFFFFFFFu)))
        return AURA_INVALID_HANDLE;
    if (slotA == slotB)
        return AURA_INVALID_DEFINITION;
    if ((slotA != nullptr && !b2Body_IsEnabled(slotA->body)) || !b2Body_IsEnabled(slotB->body))
        return AURA_BODY_DISABLED;
    /* Box2D divides by the joint's effective mass: it needs a dynamic body (a mouse joint needs body B dynamic). */
    const bool dynamicA = slotA != nullptr && b2Body_GetType(slotA->body) == b2_dynamicBody;
    const bool dynamicB = b2Body_GetType(slotB->body) == b2_dynamicBody;
    if (isMouse ? !dynamicB : (!dynamicA && !dynamicB))
        return AURA_INVALID_DEFINITION;
    if (!IsFinite(desc.anchorA) || !IsFinite(desc.anchorB) || !IsFinite(desc.axisA) || !IsFinite(desc.distance)
        || !IsFinite(desc.minLimit) || !IsFinite(desc.maxLimit) || !IsFinite(desc.motorTargetVelocity)
        || !IsFinite(desc.maxMotorForce) || !IsFinite(desc.springFrequency) || !IsFinite(desc.springDamping))
        return AURA_INVALID_DEFINITION;
    /* Impossible definitions: a negative rest length, a negative spring parameter, limits with min > max (hinge also
       outside +-pi) and a slider axis without an in-plane component. */
    const b2Vec2 anchorA0 = ToVec2(desc.anchorA);
    const b2Vec2 anchorB0 = ToVec2(desc.anchorB);
    if (desc.distance < 0.0f || desc.springFrequency < 0.0f || desc.springDamping < 0.0f)
        return AURA_INVALID_DEFINITION;
    if ((desc.type == AURA_JOINT_HINGE || desc.type == AURA_JOINT_SLIDER || desc.type == AURA_JOINT_WHEEL)
        && desc.enableLimit != 0 && desc.minLimit > desc.maxLimit)
        return AURA_INVALID_DEFINITION;
    if (desc.type == AURA_JOINT_HINGE && desc.enableLimit != 0 && (desc.minLimit < -kPi || desc.maxLimit > kPi))
        return AURA_INVALID_DEFINITION;
    if (desc.type == AURA_JOINT_SLIDER && Length(ToVec2(desc.axisA)) < 1.0e-6f)
        return AURA_INVALID_DEFINITION;
    if ((desc.type == AURA_JOINT_DISTANCE || desc.type == AURA_JOINT_SPRING || desc.type == AURA_JOINT_ROPE)
        && (LeverRatio(slotA->body, anchorA0) > kMaxRodLeverRatio || LeverRatio(slotB->body, anchorB0) > kMaxRodLeverRatio))
        return AURA_INVALID_DEFINITION;
    if ((desc.type == AURA_JOINT_WHEEL || desc.type == AURA_JOINT_SLIDER)
        && (LeverRatio(slotA->body, anchorA0) > kMaxLeverRatio || LeverRatio(slotB->body, anchorB0) > kMaxLeverRatio))
        return AURA_INVALID_DEFINITION;

    const b2Vec2 anchorA = ToVec2(desc.anchorA);
    const b2Vec2 anchorB = ToVec2(desc.anchorB);

    b2JointId joint = b2_nullJointId;
    b2Vec2 localAxis{ 1.0f, 0.0f };
    float restLength = 0.0f;
    float springHertz = 0.0f;
    bool limitEnabled = false;
    float limitMin = 0.0f;
    float limitMax = 0.0f;
    switch (static_cast<AuraJointType>(desc.type))
    {
    case AURA_JOINT_DISTANCE:
    case AURA_JOINT_SPRING:
    {
        b2DistanceJointDef def = b2DefaultDistanceJointDef();
        def.bodyIdA = slotA->body;
        def.bodyIdB = slotB->body;
        def.localAnchorA = b2Body_GetLocalPoint(slotA->body, anchorA);
        def.localAnchorB = b2Body_GetLocalPoint(slotB->body, anchorB);
        restLength = std::max(desc.distance, kLinearSlop);
        def.length = restLength;
        if (desc.type == AURA_JOINT_SPRING)
        {
            def.enableSpring = true;
            springHertz = desc.springFrequency > 0.0f ? desc.springFrequency : 1.0f;
            def.hertz = springHertz;
            def.dampingRatio = desc.springDamping > 0.0f ? desc.springDamping : 1.0f;
        }
        joint = b2CreateDistanceJoint(impl_->world, &def);
        break;
    }
    case AURA_JOINT_POINT:
    case AURA_JOINT_HINGE:
    {
        b2RevoluteJointDef def = b2DefaultRevoluteJointDef();
        def.bodyIdA = slotA->body;
        def.bodyIdB = slotB->body;
        def.referenceAngle = RelativeAngle(slotA->body, slotB->body); /* limits are relative to the creation pose */
        def.localAnchorA = b2Body_GetLocalPoint(slotA->body, anchorA);
        def.localAnchorB = b2Body_GetLocalPoint(slotB->body, anchorB);
        if (desc.type == AURA_JOINT_HINGE)
        {
            if (desc.enableLimit != 0)
            {
                def.enableLimit = true;
                def.lowerAngle = desc.minLimit;
                def.upperAngle = desc.maxLimit;
                limitEnabled = true;
                limitMin = desc.minLimit;
                limitMax = desc.maxLimit;
            }
            if (desc.motorEnabled != 0)
            {
                def.enableMotor = true;
                def.motorSpeed = desc.motorTargetVelocity;
                def.maxMotorTorque = desc.maxMotorForce;
            }
        }
        joint = b2CreateRevoluteJoint(impl_->world, &def);
        break;
    }
    case AURA_JOINT_FIXED:
    {
        b2WeldJointDef def = b2DefaultWeldJointDef();
        def.bodyIdA = slotA->body;
        def.bodyIdB = slotB->body;
        def.referenceAngle = RelativeAngle(slotA->body, slotB->body);
        def.localAnchorA = b2Body_GetLocalPoint(slotA->body, anchorA);
        def.localAnchorB = b2Body_GetLocalPoint(slotB->body, anchorB);
        joint = b2CreateWeldJoint(impl_->world, &def);
        break;
    }
    case AURA_JOINT_SLIDER:
    {
        b2PrismaticJointDef def = b2DefaultPrismaticJointDef();
        def.bodyIdA = slotA->body;
        def.bodyIdB = slotB->body;
        def.referenceAngle = RelativeAngle(slotA->body, slotB->body);
        def.localAnchorA = b2Body_GetLocalPoint(slotA->body, anchorA);
        def.localAnchorB = b2Body_GetLocalPoint(slotB->body, anchorB);
        const b2Vec2 axis = b2Body_GetLocalVector(slotA->body, ToVec2(desc.axisA));
        def.localAxisA = b2Normalize(axis);
        localAxis = def.localAxisA;
        if (desc.enableLimit != 0)
        {
            def.enableLimit = true;
            def.lowerTranslation = desc.minLimit;
            def.upperTranslation = desc.maxLimit;
            limitEnabled = true;
            limitMin = desc.minLimit;
            limitMax = desc.maxLimit;
        }
        if (desc.motorEnabled != 0)
        {
            def.enableMotor = true;
            def.motorSpeed = desc.motorTargetVelocity;
            def.maxMotorForce = desc.maxMotorForce;
        }
        joint = b2CreatePrismaticJoint(impl_->world, &def);
        break;
    }
    case AURA_JOINT_WHEEL:
    {
        const b2Vec2 axis = b2Body_GetLocalVector(slotA->body, ToVec2(desc.axisA));
        if (Length(axis) < 1.0e-6f)
            return AURA_INVALID_DEFINITION;
        if (desc.enableLimit != 0 && desc.minLimit > desc.maxLimit)
            return AURA_INVALID_DEFINITION;
        if (desc.motorEnabled != 0 && desc.maxMotorForce <= 0.0f)
            return AURA_INVALID_DEFINITION;
        if (desc.springFrequency < 0.0f || desc.springDamping < 0.0f)
            return AURA_INVALID_DEFINITION;
        b2WheelJointDef def = b2DefaultWheelJointDef();
        def.bodyIdA = slotA->body;
        def.bodyIdB = slotB->body;
        def.localAnchorA = b2Body_GetLocalPoint(slotA->body, anchorA);
        def.localAnchorB = b2Body_GetLocalPoint(slotB->body, anchorB);
        def.localAxisA = b2Normalize(axis);
        localAxis = def.localAxisA;
        if (desc.springFrequency > 0.0f)
        {
            def.enableSpring = true;
            springHertz = desc.springFrequency;
            def.hertz = springHertz;
            def.dampingRatio = desc.springDamping;
        }
        if (desc.enableLimit != 0)
        {
            def.enableLimit = true;
            def.lowerTranslation = desc.minLimit;
            def.upperTranslation = desc.maxLimit;
            limitEnabled = true;
            limitMin = desc.minLimit;
            limitMax = desc.maxLimit;
        }
        if (desc.motorEnabled != 0)
        {
            def.enableMotor = true;
            def.motorSpeed = desc.motorTargetVelocity;
            def.maxMotorTorque = desc.maxMotorForce;
        }
        joint = b2CreateWheelJoint(impl_->world, &def);
        break;
    }
    case AURA_JOINT_MOUSE:
    {
        if (desc.maxMotorForce <= 0.0f || desc.springFrequency < 0.0f || desc.springDamping < 0.0f)
            return AURA_INVALID_DEFINITION;
        b2MouseJointDef def = b2DefaultMouseJointDef();
        def.bodyIdA = slotA != nullptr ? slotA->body : impl_->GroundBody();
        def.bodyIdB = slotB->body;
        def.target = anchorB;
        springHertz = desc.springFrequency > 0.0f ? desc.springFrequency : 5.0f;
        def.hertz = springHertz;
        def.dampingRatio = desc.springDamping > 0.0f ? desc.springDamping : 0.7f;
        def.maxForce = desc.maxMotorForce;
        joint = b2CreateMouseJoint(impl_->world, &def);
        break;
    }
    case AURA_JOINT_ROPE:
    {
        const float minLength = desc.enableLimit != 0 ? std::max(0.0f, desc.minLimit) : 0.0f;
        if (desc.distance <= 0.0f || minLength > desc.distance)
            return AURA_INVALID_DEFINITION;
        /* Box2D rope: a distance joint with the spring enabled at zero stiffness and a length limit. */
        b2DistanceJointDef def = b2DefaultDistanceJointDef();
        def.bodyIdA = slotA->body;
        def.bodyIdB = slotB->body;
        def.localAnchorA = b2Body_GetLocalPoint(slotA->body, anchorA);
        def.localAnchorB = b2Body_GetLocalPoint(slotB->body, anchorB);
        def.length = desc.distance;
        limitEnabled = true;
        limitMin = std::max(minLength, kLinearSlop);
        limitMax = std::max(desc.distance, kLinearSlop);
        def.enableSpring = true;
        def.hertz = 0.0f;
        def.dampingRatio = 0.0f;
        def.enableLimit = true;
        def.minLength = minLength;
        def.maxLength = desc.distance;
        joint = b2CreateDistanceJoint(impl_->world, &def);
        break;
    }
    default:
        return AURA_UNSUPPORTED_QUERY;
    }

    if (!b2Joint_IsValid(joint))
        return AURA_OUT_OF_MEMORY;

    int index;
    if (!impl_->freeJointSlots.empty())
    {
        index = impl_->freeJointSlots.back();
        impl_->freeJointSlots.pop_back();
    }
    else
    {
        index = static_cast<int>(impl_->jointSlots.size());
        impl_->jointSlots.emplace_back();
    }

    Impl::JointSlot& slot = impl_->jointSlots[index];
    slot.occupied = true;
    slot.joint = joint;
    slot.bodyA = desc.bodyA;
    slot.bodyB = desc.bodyB;
    slot.type = static_cast<AuraJointType>(desc.type);
    slot.localAxisA = localAxis;
    slot.limitEnabled = limitEnabled;
    slot.limitMin = limitMin;
    slot.limitMax = limitMax;
    slot.restLength = restLength;
    slot.springHertz = springHertz;
    slot.breakForce = 0.0f;
    slot.breakTorque = 0.0f;
    slot.broken = false;
    slot.lastForce = 0.0f;
    slot.lastTorque = 0.0f;
    *outJoint = Impl::MakeJointHandle(slot, index);
    impl_->EaseJoint(slot, impl_->lastDelta);
    return AURA_SUCCESS;
}

AuraResultCode Box2DWorld::DestroyJoint(uint64_t joint)
{
    Impl::JointSlot* slot = impl_->FindJoint(joint);
    if (slot == nullptr)
        return AURA_INVALID_HANDLE;

    if (b2Joint_IsValid(slot->joint))
        b2DestroyJoint(slot->joint);
    slot->joint = b2_nullJointId;
    slot->occupied = false;
    slot->generation += 1;
    impl_->freeJointSlots.push_back(static_cast<int>(joint & 0xFFFFFFFFull));
    return AURA_SUCCESS;
}

/* True while the handle is live, from CreateJoint until DestroyJoint, including after the joint broke (IsJointBroken
   tells). False for a destroyed, stale or invalid handle. Identical to JoltWorld::HasJoint. */
bool Box2DWorld::HasJoint(uint64_t joint) const
{
    return impl_->FindJoint(joint) != nullptr;
}

/* ---- v10 joint control. Box2D has no position motor: AURA_JOINT_MOTOR_POSITION is unsupported. ---- */

bool Box2DWorld::Impl::JointLoads(const JointSlot& joint, float& force, float& torque, float& motorLoad) const
{
    force = 0.0f;
    torque = 0.0f;
    motorLoad = 0.0f;
    if (joint.broken || !b2Joint_IsValid(joint.joint))
        return false;

    /* Sleeping joints keep the impulses of the step they fell asleep in, which is the load they still carry. */
    const Slot* a = Find(joint.bodyA);
    const Slot* b = Find(joint.bodyB);
    if ((a == nullptr && joint.type != AURA_JOINT_MOUSE) || b == nullptr)
        return false;

    const b2Vec2 constraintForce = b2Joint_GetConstraintForce(joint.joint);
    const float constraintTorque = b2Joint_GetConstraintTorque(joint.joint);
    switch (joint.type)
    {
    case AURA_JOINT_FIXED:
        force = Length(constraintForce);
        torque = std::abs(constraintTorque);
        return true;
    case AURA_JOINT_DISTANCE:
    case AURA_JOINT_SPRING:
        force = Length(constraintForce);
        return true;
    case AURA_JOINT_POINT:
    case AURA_JOINT_MOUSE:
    case AURA_JOINT_ROPE:
        force = Length(constraintForce);
        return true;
    case AURA_JOINT_WHEEL:
    {
        const float motor = b2WheelJoint_GetMotorTorque(joint.joint);
        force = Length(constraintForce);
        torque = std::abs(constraintTorque - motor);
        motorLoad = std::abs(motor);
        return true;
    }
    case AURA_JOINT_HINGE:
    {
        const float motor = b2RevoluteJoint_GetMotorTorque(joint.joint);
        force = Length(constraintForce);
        torque = std::abs(constraintTorque - motor);
        motorLoad = std::abs(motor);
        return true;
    }
    case AURA_JOINT_SLIDER:
    {
        const float motor = b2PrismaticJoint_GetMotorForce(joint.joint);
        const b2Vec2 axis = b2RotateVector(b2Body_GetRotation(a->body), joint.localAxisA);
        force = Length(b2Vec2{ constraintForce.x - axis.x * motor, constraintForce.y - axis.y * motor });
        torque = std::abs(constraintTorque);
        motorLoad = std::abs(motor);
        return true;
    }
    default:
        return false;
    }
}

void Box2DWorld::Impl::ProcessJointBreaks()
{
    for (JointSlot& joint : jointSlots)
    {
        if (!joint.occupied || joint.broken)
            continue;
        if (joint.breakForce <= 0.0f && joint.breakTorque <= 0.0f)
            continue;

        float force, torque, motor;
        JointLoads(joint, force, torque, motor);
        const bool overForce = joint.breakForce > 0.0f && force > joint.breakForce;
        const bool overTorque = joint.breakTorque > 0.0f && torque > joint.breakTorque;
        if (!overForce && !overTorque)
            continue;

        joint.lastForce = force;
        joint.lastTorque = torque;
        joint.broken = true;
        if (b2Joint_IsValid(joint.joint))
            b2DestroyJoint(joint.joint);
        joint.joint = b2_nullJointId;

        for (const AuraBodyHandle handle : { joint.bodyA, joint.bodyB })
        {
            const Slot* body = Find(handle);
            if (body != nullptr && b2Body_IsEnabled(body->body))
                b2Body_SetAwake(body->body, true);
        }
    }

    EaseJoints(lastDelta);
}


/* False when an anchor of a slider/wheel joint sits too far from a light body for its limit, motor or spring row, see LeverRatio. */
bool Box2DWorld::Impl::WellConditioned(const JointSlot& joint) const
{
    const Slot* a = Find(joint.bodyA);
    const Slot* b = Find(joint.bodyB);
    if (a == nullptr || b == nullptr || !b2Joint_IsValid(joint.joint))
        return true;
    return LeverRatio(a->body, b2Body_GetWorldPoint(a->body, b2Joint_GetLocalAnchorA(joint.joint))) <= kMaxLeverRatio
        && LeverRatio(b->body, b2Body_GetWorldPoint(b->body, b2Joint_GetLocalAnchorB(joint.joint))) <= kMaxLeverRatio;
}

/* Bounded position correction, see kMaxCorrectionSpeed. Runs after every step (for the next one) and when a joint is
   created or its limits change. Only the targets Box2D corrects through the uncapped joint bias are eased: hinge,
   slider and wheel limits, the rigid distance length and the rope length range. A pose teleported between two steps
   is eased from the next step on. */
void Box2DWorld::Impl::EaseJoint(JointSlot& joint, float deltaTime)
{
    if (joint.broken || !b2Joint_IsValid(joint.joint))
        return;
    const Slot* a = Find(joint.bodyA);
    const Slot* b = Find(joint.bodyB);
    if (a == nullptr || b == nullptr)
        return;

    /* Point-like rows (point, hinge, weld, the perpendicular row of slider and wheel) have no target to ease: Box2D
       would correct a teleported body's error of tens of metres at >1000 m/s. Such a joint tears off instead, exactly
       like one that exceeded its break threshold (IsJointBroken, handle valid until DestroyJoint). */
    if (joint.type == AURA_JOINT_POINT || joint.type == AURA_JOINT_HINGE || joint.type == AURA_JOINT_FIXED
        || joint.type == AURA_JOINT_SLIDER || joint.type == AURA_JOINT_WHEEL)
    {
        const b2Vec2 gap = b2Sub(b2Body_GetWorldPoint(b->body, b2Joint_GetLocalAnchorB(joint.joint)),
                                 b2Body_GetWorldPoint(a->body, b2Joint_GetLocalAnchorA(joint.joint)));
        float error = Length(gap);
        if (joint.type == AURA_JOINT_SLIDER || joint.type == AURA_JOINT_WHEEL)
            error = std::abs(b2Cross(b2RotateVector(b2Body_GetRotation(a->body), joint.localAxisA), gap));
        const bool illConditioned = (joint.type == AURA_JOINT_SLIDER || joint.type == AURA_JOINT_WHEEL)
            && (LeverRatio(a->body, b2Body_GetWorldPoint(a->body, b2Joint_GetLocalAnchorA(joint.joint))) > kTearLeverRatio
                || LeverRatio(b->body, b2Body_GetWorldPoint(b->body, b2Joint_GetLocalAnchorB(joint.joint))) > kTearLeverRatio);
        if (error > kTearDistance || illConditioned)
        {
            joint.lastForce = 0.0f;
            joint.lastTorque = 0.0f;
            joint.broken = true;
            b2DestroyJoint(joint.joint);
            joint.joint = b2_nullJointId;
            for (const Slot* body : { a, b })
                if (b2Body_IsEnabled(body->body))
                    b2Body_SetAwake(body->body, true);
            return;
        }
    }

    const float ease = kMaxCorrectionSpeed * std::max(deltaTime, 1.0f / 240.0f);
    const auto differs = [](float x, float y) { return std::abs(x - y) > 1.0e-6f; };

    switch (joint.type)
    {
    case AURA_JOINT_HINGE:
    {
        if (!joint.limitEnabled)
            return;
        const float angle = b2RevoluteJoint_GetAngle(joint.joint);
        const float lower = std::max(std::min(joint.limitMin, angle + ease), -kPi);
        const float upper = std::min(std::max(joint.limitMax, angle - ease), kPi);
        if (differs(lower, b2RevoluteJoint_GetLowerLimit(joint.joint)) || differs(upper, b2RevoluteJoint_GetUpperLimit(joint.joint)))
            b2RevoluteJoint_SetLimits(joint.joint, lower, upper);
        break;
    }
    case AURA_JOINT_SLIDER:
    case AURA_JOINT_WHEEL:
    {
        if (joint.type == AURA_JOINT_WHEEL && joint.springHertz > 0.0f)
        {
            const float hertz = std::min(joint.springHertz, 0.5f / std::max(deltaTime, 1.0f / 240.0f));
            if (differs(hertz, b2WheelJoint_GetSpringHertz(joint.joint)))
                b2WheelJoint_SetSpringHertz(joint.joint, hertz);
        }
        if (!joint.limitEnabled)
            return;
        float translation;
        if (joint.type == AURA_JOINT_SLIDER)
        {
            translation = b2PrismaticJoint_GetTranslation(joint.joint);
        }
        else
        {
            const b2Vec2 pointA = b2Body_GetWorldPoint(a->body, b2Joint_GetLocalAnchorA(joint.joint));
            const b2Vec2 pointB = b2Body_GetWorldPoint(b->body, b2Joint_GetLocalAnchorB(joint.joint));
            translation = b2Dot(b2Sub(pointB, pointA), b2RotateVector(b2Body_GetRotation(a->body), joint.localAxisA));
        }
        const float lower = std::min(joint.limitMin, translation + ease);
        const float upper = std::max(joint.limitMax, translation - ease);
        if (joint.type == AURA_JOINT_SLIDER)
        {
            if (differs(lower, b2PrismaticJoint_GetLowerLimit(joint.joint)) || differs(upper, b2PrismaticJoint_GetUpperLimit(joint.joint)))
                b2PrismaticJoint_SetLimits(joint.joint, lower, upper);
        }
        else if (differs(lower, b2WheelJoint_GetLowerLimit(joint.joint)) || differs(upper, b2WheelJoint_GetUpperLimit(joint.joint)))
        {
            b2WheelJoint_SetLimits(joint.joint, lower, upper);
        }
        break;
    }
    case AURA_JOINT_DISTANCE:
    {
        /* A rigid distance joint: the length target moves towards the rest length. */
        const float current = b2DistanceJoint_GetCurrentLength(joint.joint);
        const float length = std::max(std::min(std::max(joint.restLength, current - ease), current + ease), kLinearSlop);
        if (differs(length, b2DistanceJoint_GetLength(joint.joint)))
            b2DistanceJoint_SetLength(joint.joint, length);
        break;
    }
    case AURA_JOINT_MOUSE:
    {
        const float hertz = std::min(joint.springHertz, 0.5f / std::max(deltaTime, 1.0f / 240.0f));
        if (differs(hertz, b2MouseJoint_GetSpringHertz(joint.joint)))
            b2MouseJoint_SetSpringHertz(joint.joint, hertz);
        break;
    }
    case AURA_JOINT_SPRING:
    {
        /* Box2D caps its own joint stiffness at 1/8 of the substep rate; a spring faster than the step can resolve
           (8 Hz at dt 0.25) has to be capped the same way or the pair spins up and explodes. */
        const float hertz = std::min(joint.springHertz, 0.5f / std::max(deltaTime, 1.0f / 240.0f));
        if (differs(hertz, b2DistanceJoint_GetSpringHertz(joint.joint)))
            b2DistanceJoint_SetSpringHertz(joint.joint, hertz);
        break;
    }
    case AURA_JOINT_ROPE:
    {
        const float current = b2DistanceJoint_GetCurrentLength(joint.joint);
        const float lower = std::max(std::min(joint.limitMin, current + ease), kLinearSlop);
        const float upper = std::max(std::max(joint.limitMax, current - ease), kLinearSlop);
        if (differs(lower, b2DistanceJoint_GetMinLength(joint.joint)) || differs(upper, b2DistanceJoint_GetMaxLength(joint.joint)))
            b2DistanceJoint_SetLengthRange(joint.joint, lower, upper);
        break;
    }
    default:
        break;
    }
}

void Box2DWorld::Impl::EaseJoints(float deltaTime)
{
    for (JointSlot& joint : jointSlots)
    {
        if (!joint.occupied || joint.broken)
            continue;
        const Slot* a = Find(joint.bodyA);
        const Slot* b = Find(joint.bodyB);
        if (a == nullptr || b == nullptr || !(b2Body_IsAwake(a->body) || b2Body_IsAwake(b->body)))
            continue;
        EaseJoint(joint, deltaTime);
    }
}

AuraResultCode Box2DWorld::SetJointMotor(uint64_t joint, const AuraJointMotorDesc& motor)
{
    Impl::JointSlot* slot = impl_->FindJoint(joint);
    if (slot == nullptr)
        return AURA_INVALID_HANDLE;
    if (slot->broken)
        return AURA_UNSUPPORTED_OPERATION; /* the handle is valid but the constraint is gone */
    if (slot->type != AURA_JOINT_HINGE && slot->type != AURA_JOINT_SLIDER && slot->type != AURA_JOINT_WHEEL)
        return AURA_UNSUPPORTED_OPERATION;
    if (motor.mode < AURA_JOINT_MOTOR_OFF || motor.mode > AURA_JOINT_MOTOR_POSITION
        || !IsFinite(motor.target) || !IsFinite(motor.maxForce) || !IsFinite(motor.springFrequency)
        || !IsFinite(motor.springDamping) || motor.springFrequency < 0.0f || motor.springDamping < 0.0f)
        return AURA_INVALID_DEFINITION;
    if (motor.mode == AURA_JOINT_MOTOR_POSITION)
        return AURA_UNSUPPORTED_OPERATION;
    if (motor.mode != AURA_JOINT_MOTOR_OFF && motor.maxForce <= 0.0f)
        return AURA_INVALID_DEFINITION;

    const bool on = motor.mode == AURA_JOINT_MOTOR_VELOCITY;
    if (on && slot->type == AURA_JOINT_SLIDER && !impl_->WellConditioned(*slot))
        return AURA_INVALID_DEFINITION;
    if (slot->type == AURA_JOINT_HINGE)
    {
        if (on)
        {
            b2RevoluteJoint_SetMotorSpeed(slot->joint, motor.target);
            b2RevoluteJoint_SetMaxMotorTorque(slot->joint, motor.maxForce);
        }
        b2RevoluteJoint_EnableMotor(slot->joint, on);
    }
    else if (slot->type == AURA_JOINT_WHEEL)
    {
        if (on)
        {
            b2WheelJoint_SetMotorSpeed(slot->joint, motor.target);
            b2WheelJoint_SetMaxMotorTorque(slot->joint, motor.maxForce);
        }
        b2WheelJoint_EnableMotor(slot->joint, on);
    }
    else
    {
        if (on)
        {
            b2PrismaticJoint_SetMotorSpeed(slot->joint, motor.target);
            b2PrismaticJoint_SetMaxMotorForce(slot->joint, motor.maxForce);
        }
        b2PrismaticJoint_EnableMotor(slot->joint, on);
    }

    for (const AuraBodyHandle handle : { slot->bodyA, slot->bodyB })
    {
        const Impl::Slot* body = impl_->Find(handle);
        if (body != nullptr && b2Body_IsEnabled(body->body))
            b2Body_SetAwake(body->body, true);
    }
    return AURA_SUCCESS;
}

AuraResultCode Box2DWorld::SetJointLimits(uint64_t joint, bool enabled, float minLimit, float maxLimit)
{
    Impl::JointSlot* slot = impl_->FindJoint(joint);
    if (slot == nullptr)
        return AURA_INVALID_HANDLE;
    if (slot->broken)
        return AURA_UNSUPPORTED_OPERATION; /* the handle is valid but the constraint is gone */
    if (slot->type != AURA_JOINT_HINGE && slot->type != AURA_JOINT_SLIDER && slot->type != AURA_JOINT_WHEEL)
        return AURA_UNSUPPORTED_OPERATION;

    const bool hinge = slot->type == AURA_JOINT_HINGE;
    if (enabled)
    {
        if (!IsFinite(minLimit) || !IsFinite(maxLimit) || minLimit > 0.0f || maxLimit < 0.0f)
            return AURA_INVALID_DEFINITION;
        if (hinge && (minLimit < -kPi || maxLimit > kPi))
            return AURA_INVALID_DEFINITION;
    }

    if (enabled && !hinge && !impl_->WellConditioned(*slot))
        return AURA_INVALID_DEFINITION;

    /* The requested range is stored; EaseJoint feeds the solver a copy that never starts further than a bounded
       correction away from the current pose. */
    slot->limitEnabled = enabled;
    if (enabled)
    {
        slot->limitMin = minLimit;
        slot->limitMax = maxLimit;
    }
    if (hinge)
        b2RevoluteJoint_EnableLimit(slot->joint, enabled);
    else if (slot->type == AURA_JOINT_WHEEL)
        b2WheelJoint_EnableLimit(slot->joint, enabled);
    else
        b2PrismaticJoint_EnableLimit(slot->joint, enabled);

    /* A resting body would otherwise sleep through the changed limits. */
    for (const AuraBodyHandle handle : { slot->bodyA, slot->bodyB })
    {
        const Impl::Slot* body = impl_->Find(handle);
        if (body != nullptr)
            Wake(body->body);
    }
    impl_->EaseJoint(*slot, impl_->lastDelta);
    return AURA_SUCCESS;
}

AuraResultCode Box2DWorld::SetJointBreakThreshold(uint64_t joint, float maxForce, float maxTorque)
{
    Impl::JointSlot* slot = impl_->FindJoint(joint);
    if (slot == nullptr)
        return AURA_INVALID_HANDLE;
    if (slot->broken)
        return AURA_UNSUPPORTED_OPERATION; /* the handle is valid but the constraint is gone */

    bool hasTorque = false;
    switch (slot->type)
    {
    case AURA_JOINT_FIXED:
    case AURA_JOINT_HINGE:
    case AURA_JOINT_SLIDER:
    case AURA_JOINT_WHEEL:
        hasTorque = true;
        break;
    case AURA_JOINT_POINT:
    case AURA_JOINT_DISTANCE:
    case AURA_JOINT_SPRING:
    case AURA_JOINT_MOUSE:
    case AURA_JOINT_ROPE:
        break;
    default:
        return AURA_UNSUPPORTED_OPERATION;
    }

    if (!IsFinite(maxForce) || !IsFinite(maxTorque) || maxForce < 0.0f || maxTorque < 0.0f)
        return AURA_INVALID_DEFINITION;
    if (!hasTorque && maxTorque > 0.0f)
        return AURA_INVALID_DEFINITION;

    slot->breakForce = maxForce;
    slot->breakTorque = maxTorque;
    return AURA_SUCCESS;
}

AuraResultCode Box2DWorld::IsJointBroken(uint64_t joint, bool* outBroken) const
{
    const Impl::JointSlot* slot = impl_->FindJoint(joint);
    if (slot == nullptr || outBroken == nullptr)
        return AURA_INVALID_HANDLE;
    *outBroken = slot->broken;
    return AURA_SUCCESS;
}

AuraResultCode Box2DWorld::GetJointFeedback(uint64_t joint, AuraJointFeedback* outFeedback) const
{
    const Impl::JointSlot* slot = impl_->FindJoint(joint);
    if (slot == nullptr || outFeedback == nullptr)
        return AURA_INVALID_HANDLE;

    *outFeedback = AuraJointFeedback{};
    outFeedback->isBroken = slot->broken ? 1 : 0;
    if (slot->broken)
    {
        outFeedback->force = slot->lastForce;
        outFeedback->torque = slot->lastTorque;
        return AURA_SUCCESS;
    }

    impl_->JointLoads(*slot, outFeedback->force, outFeedback->torque, outFeedback->motorLoad);
    if (slot->type == AURA_JOINT_HINGE)
    {
        outFeedback->position = b2RevoluteJoint_GetAngle(slot->joint);
        outFeedback->motorMode = b2RevoluteJoint_IsMotorEnabled(slot->joint) ? AURA_JOINT_MOTOR_VELOCITY : AURA_JOINT_MOTOR_OFF;
    }
    else if (slot->type == AURA_JOINT_WHEEL)
    {
        const Impl::Slot* a = impl_->Find(slot->bodyA);
        const Impl::Slot* b = impl_->Find(slot->bodyB);
        if (a != nullptr && b != nullptr)
        {
            /* Suspension travel: wheel anchor relative to the chassis anchor along the suspension axis. */
            const b2Vec2 pointA = b2Body_GetWorldPoint(a->body, b2Joint_GetLocalAnchorA(slot->joint));
            const b2Vec2 pointB = b2Body_GetWorldPoint(b->body, b2Joint_GetLocalAnchorB(slot->joint));
            const b2Vec2 axis = b2RotateVector(b2Body_GetRotation(a->body), slot->localAxisA);
            outFeedback->position = b2Dot(b2Sub(pointB, pointA), axis);
        }
        outFeedback->motorMode = b2WheelJoint_IsMotorEnabled(slot->joint) ? AURA_JOINT_MOTOR_VELOCITY : AURA_JOINT_MOTOR_OFF;
    }
    else if (slot->type == AURA_JOINT_SLIDER)
    {
        outFeedback->position = b2PrismaticJoint_GetTranslation(slot->joint);
        outFeedback->motorMode = b2PrismaticJoint_IsMotorEnabled(slot->joint) ? AURA_JOINT_MOTOR_VELOCITY : AURA_JOINT_MOTOR_OFF;
    }
    return AURA_SUCCESS;
}

AuraResultCode Box2DWorld::SetJointTarget(uint64_t joint, const AuraVec3& target)
{
    Impl::JointSlot* slot = impl_->FindJoint(joint);
    if (slot == nullptr)
        return AURA_INVALID_HANDLE;
    if (slot->broken)
        return AURA_UNSUPPORTED_OPERATION; /* the handle is valid but the constraint is gone */
    if (slot->type != AURA_JOINT_MOUSE)
        return AURA_UNSUPPORTED_OPERATION;
    if (!IsFinite(target))
        return AURA_INVALID_DEFINITION;

    b2MouseJoint_SetTarget(slot->joint, ToVec2(target));
    const Impl::Slot* body = impl_->Find(slot->bodyB);
    if (body != nullptr)
        Wake(body->body);
    return AURA_SUCCESS;
}

} // namespace aura
