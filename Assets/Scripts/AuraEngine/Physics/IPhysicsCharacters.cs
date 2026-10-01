using AuraEngine.Core;

namespace AuraEngine.Physics
{
    public interface IPhysicsCharacters
    {
        AuraCharacterId CreateCharacter(in AuraCharacterDefinition definition);

        AuraResult DestroyCharacter(AuraCharacterId character);

        bool TryGetCharacterState(AuraCharacterId character, out AuraCharacterState state);

        void MoveCharacter(AuraCharacterId character, AuraVector3 desiredTranslation, float deltaTime);
    }
}
