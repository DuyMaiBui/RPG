namespace AuraEngine.Physics
{
    public interface IPhysicsSerialization
    {
        ulong ComputeStateHash();

        byte[] SaveState();

        void RestoreState(byte[] state);
    }
}
