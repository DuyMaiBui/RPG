using System;

namespace RPG.Core.Actors
{
    /// <summary>An actor's abilities and their remaining cooldowns. Definitions keep their authored order so casting
    /// is deterministic (the first ready ability wins).</summary>
    public sealed class AbilityComponent : IActorComponent
    {
        private readonly AbilityDefinition[] _definitions;
        private readonly int[] _cooldowns;

        public AbilityComponent(AbilityDefinition[] definitions)
        {
            if (definitions == null || definitions.Length == 0)
                throw new ArgumentException("An ability component requires at least one ability.", nameof(definitions));

            _definitions = (AbilityDefinition[])definitions.Clone();
            _cooldowns = new int[_definitions.Length];
        }

        public int Count => _definitions.Length;

        public AbilityDefinition GetAt(int index) => _definitions[index];

        public int CooldownRemaining(int index) => _cooldowns[index];

        public bool IsReady(int index) => _cooldowns[index] <= 0;

        /// <summary>Advances every cooldown by one tick.</summary>
        public void Tick()
        {
            for (var index = 0; index < _cooldowns.Length; index++)
            {
                if (_cooldowns[index] > 0)
                    _cooldowns[index]--;
            }
        }

        public void StartCooldown(int index) => _cooldowns[index] = _definitions[index].CooldownTicks;
    }
}
