using RPG.Core.Actors;
using UnityEngine;

namespace RPG.Unity
{
    public abstract class ActorViewComponent : MonoBehaviour
    {
        public abstract void Initialize(in ActorViewContext context);
        public abstract void ApplySnapshot(ActorSnapshot snapshot);
        public abstract void PlaySignal(PresentationSignal signal);
        public abstract void Release();
    }
}
