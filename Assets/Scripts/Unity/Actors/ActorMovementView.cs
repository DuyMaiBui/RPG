using RPG.Core.Actors;
using UnityEngine;

namespace RPG.Unity
{
    public sealed class ActorMovementView : ActorViewComponent
    {
        public override void Initialize(in ActorViewContext context)
        {
        }

        public override void ApplySnapshot(ActorSnapshot snapshot)
        {
            transform.position = new Vector3(snapshot.Position.X, snapshot.Position.Y, transform.position.z);
        }

        public void ApplyPredictedPosition(RPG.Simulation.Contracts.SimulationVector2 position)
        {
            transform.position = new Vector3(position.X, position.Y, transform.position.z);
        }

        public override void PlaySignal(PresentationSignal signal)
        {
        }

        public override void Release()
        {
        }
    }
}
