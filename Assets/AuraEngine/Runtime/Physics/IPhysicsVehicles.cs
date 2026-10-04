using AuraEngine.Core;

namespace AuraEngine.Physics
{
    public interface IPhysicsVehicles
    {
        AuraVehicleId CreateVehicle(in AuraVehicleDefinition definition);
        AuraResult DestroyVehicle(AuraVehicleId vehicle);
        AuraResult SetVehicleInput(AuraVehicleId vehicle, float forward, float steering, float brake, float handBrake);
        bool TryGetWheelState(AuraVehicleId vehicle, int wheelIndex, out AuraVehicleWheelState state);
    }
}
