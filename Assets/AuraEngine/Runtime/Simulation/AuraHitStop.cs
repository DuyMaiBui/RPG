using System;
using System.Collections.Generic;
using AuraEngine.Core;
using AuraEngine.Physics;

namespace AuraEngine.Simulation
{
    /* Per-body hit-stop: freezes a dynamic body in place for a number of simulation steps (zero gravity scale and
       zero velocity, re-applied every step so contacts cannot push it) and then restores the velocity it had.
       Built on IPhysicsBodyControl, so it needs AuraPhysicsCapabilities.BodyControl.

       Call Tick() once per executed simulation step before the physics step (AuraSimulationWorld does this).
       Begin(body, n) freezes the body for exactly n steps. The body state does not expose the gravity scale, so
       Begin takes the scale to restore. Bodies destroyed during a freeze are dropped silently. */
    public sealed class AuraHitStop
    {
        private readonly IPhysicsWorld _physics;
        private readonly List<AuraHitStopEntry> _entries = new List<AuraHitStopEntry>(8);

        public AuraHitStop(IPhysicsWorld physics)
        {
            _physics = physics ?? throw new ArgumentNullException(nameof(physics));
        }

        public int ActiveCount => _entries.Count;

        public bool IsFrozen(PhysicsBodyId body) => IndexOf(body) >= 0;

        public AuraResult Begin(PhysicsBodyId body, int ticks, float restoreGravityScale = 1f)
        {
            if (ticks <= 0 || float.IsNaN(restoreGravityScale) || float.IsInfinity(restoreGravityScale))
                return AuraResult.InvalidDefinition;

            var existing = IndexOf(body);
            if (existing >= 0)
            {
                var entry = _entries[existing];
                entry.RemainingTicks = Math.Max(entry.RemainingTicks, ticks);
                _entries[existing] = entry;
                return AuraResult.Success;
            }

            var result = _physics.GetBodyState(body, out var state);
            if (result != AuraResult.Success)
                return result;

            var control = _physics.BodyControl;
            result = control.SetGravityScale(body, 0f);
            if (result != AuraResult.Success)
                return result;

            result = Zero(control, body);
            if (result != AuraResult.Success)
            {
                control.SetGravityScale(body, restoreGravityScale);
                return result;
            }

            _entries.Add(new AuraHitStopEntry
            {
                Body = body,
                RemainingTicks = ticks,
                LinearVelocity = state.LinearVelocity,
                AngularVelocity = state.AngularVelocity,
                GravityScale = restoreGravityScale,
            });
            return AuraResult.Success;
        }

        /* Ends the freeze now and restores the saved velocity. */
        public AuraResult Cancel(PhysicsBodyId body)
        {
            var index = IndexOf(body);
            if (index < 0)
                return AuraResult.InvalidHandle;

            var entry = _entries[index];
            _entries.RemoveAt(index);
            return Restore(entry);
        }

        public void Tick()
        {
            if (_entries.Count == 0)
                return;

            var control = _physics.BodyControl;
            for (var index = 0; index < _entries.Count;)
            {
                var entry = _entries[index];
                if (entry.RemainingTicks <= 0)
                {
                    _entries.RemoveAt(index);
                    Restore(entry);
                    continue;
                }

                if (Zero(control, entry.Body) == AuraResult.InvalidHandle)
                {
                    _entries.RemoveAt(index);
                    continue;
                }

                entry.RemainingTicks--;
                _entries[index] = entry;
                index++;
            }
        }

        private AuraResult Restore(in AuraHitStopEntry entry)
        {
            var control = _physics.BodyControl;
            var result = control.SetGravityScale(entry.Body, entry.GravityScale);
            if (result != AuraResult.Success)
                return result;

            result = control.SetLinearVelocity(entry.Body, entry.LinearVelocity);
            return result != AuraResult.Success ? result : control.SetAngularVelocity(entry.Body, entry.AngularVelocity);
        }

        private static AuraResult Zero(IPhysicsBodyControl control, PhysicsBodyId body)
        {
            var result = control.SetLinearVelocity(body, AuraVector3.Zero);
            return result != AuraResult.Success ? result : control.SetAngularVelocity(body, AuraVector3.Zero);
        }

        private int IndexOf(PhysicsBodyId body)
        {
            for (var index = 0; index < _entries.Count; index++)
                if (_entries[index].Body.Equals(body))
                    return index;

            return -1;
        }
    }
}
