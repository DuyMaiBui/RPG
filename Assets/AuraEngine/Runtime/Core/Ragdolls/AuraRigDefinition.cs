namespace AuraEngine.Core
{
    public readonly struct AuraRigDefinition
    {
        public AuraRigDefinition(int[] parentIndices, AuraPose[] bindPoses)
        { ParentIndices = parentIndices; BindPoses = bindPoses; }
        public int[] ParentIndices { get; }
        public AuraPose[] BindPoses { get; }
    }
}
