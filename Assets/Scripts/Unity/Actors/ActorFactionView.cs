using System;
using RPG.Core.Actors;
using UnityEngine;

namespace RPG.Unity
{
    public sealed class ActorFactionView : ActorViewComponent
    {
        [SerializeField] private SpriteRenderer _renderer;
        private Color _factionColor;

        public override void Initialize(in ActorViewContext context)
        {
            if (_renderer == null)
                throw new InvalidOperationException("ActorFactionView prefab references are incomplete.");

            _factionColor = context.FactionColor;
            _renderer.color = _factionColor;
        }

        public override void ApplySnapshot(ActorSnapshot snapshot)
        {
        }

        public override void PlaySignal(PresentationSignal signal)
        {
        }

        public override void Release()
        {
        }
    }
}
