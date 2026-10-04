using AuraEngine.Core;
using AuraEngine.Networking;
using AuraEngine.Simulation;

namespace AuraEngine.Tests
{
    public sealed class AuraInputCommandHandler : IAuraCommandHandler
    {
        private readonly float _moveSpeed;

        public AuraInputCommandHandler(float moveSpeed)
        {
            _moveSpeed = moveSpeed;
        }

        public SimulationEntityId Entity { get; set; }

        ushort IAuraCommandHandler.TypeId => AuraInputCommand.CommandTypeId;

        AuraResult IAuraCommandHandler.Handle(IAuraSimulationContext context, IAuraCommand command)
        {
            if (!(command is AuraInputCommand input))
                return AuraResult.InvalidDefinition;

            if (!Entity.IsNone && context.TryGetBodyState(Entity, out var state))
            {
                var position = state.Pose.Position + new AuraVector3(input.MoveX * _moveSpeed, 0f, input.MoveY * _moveSpeed);
                return context.SetKinematicTarget(Entity, new AuraPose(position, state.Pose.Rotation));
            }

            return AuraResult.InvalidHandle;
        }
    }
}
