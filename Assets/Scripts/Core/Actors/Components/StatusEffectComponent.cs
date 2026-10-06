using System;
using System.Collections.Generic;

namespace RPG.Core.Actors
{
    /// <summary>An actor's active status effects. Deterministic: effects keep insertion order, and re-applying a type
    /// refreshes it in place (duration takes the max, magnitude takes the max, stacks grow up to the type cap).</summary>
    public sealed class StatusEffectComponent : IActorComponent
    {
        private readonly List<StatusEffect> _effects = new();

        public int Count => _effects.Count;

        public StatusEffect GetAt(int index) => _effects[index];

        public bool TryGet(StatusEffectType type, out StatusEffect effect)
        {
            for (var index = 0; index < _effects.Count; index++)
            {
                if (_effects[index].Type != type) continue;
                effect = _effects[index];
                return true;
            }

            effect = default;
            return false;
        }

        public bool Has(StatusEffectType type) => TryGet(type, out _);

        /// <summary>Applies an effect, or refreshes and stacks an existing one of the same type. Returns true when the
        /// state changed; a non-positive duration is ignored.</summary>
        public bool Apply(StatusEffectType type, int magnitude, int durationTicks)
        {
            if (magnitude <= 0) throw new ArgumentOutOfRangeException(nameof(magnitude));
            if (durationTicks <= 0) return false;

            for (var index = 0; index < _effects.Count; index++)
            {
                var existing = _effects[index];
                if (existing.Type != type) continue;

                var stacks = Math.Min(existing.Stacks + 1, StatusEffectRules.MaxStacks(type));
                _effects[index] = new StatusEffect(
                    type,
                    Math.Max(existing.Magnitude, magnitude),
                    Math.Max(existing.RemainingTicks, durationTicks),
                    stacks);
                return true;
            }

            _effects.Add(new StatusEffect(type, magnitude, durationTicks, 1));
            return true;
        }

        /// <summary>Advances every effect by one tick and removes the expired ones.</summary>
        public void Advance()
        {
            for (var index = _effects.Count - 1; index >= 0; index--)
            {
                var remaining = _effects[index].RemainingTicks - 1;
                if (remaining <= 0)
                    _effects.RemoveAt(index);
                else
                    _effects[index] = _effects[index].WithRemainingTicks(remaining);
            }
        }

        public void Clear() => _effects.Clear();

        /// <summary>Bit per <see cref="StatusEffectType"/> for the active effects; for presentation only.</summary>
        public byte Mask()
        {
            var mask = 0;
            for (var index = 0; index < _effects.Count; index++)
                mask |= 1 << (int)_effects[index].Type;
            return (byte)mask;
        }
    }
}
