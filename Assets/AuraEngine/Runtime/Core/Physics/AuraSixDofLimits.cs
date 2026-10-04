using System;

namespace AuraEngine.Core
{
    /* The six axis settings of a SixDof joint. The default value locks every axis (a fixed joint). */
    public readonly struct AuraSixDofLimits
    {
        public AuraSixDofLimits(
            AuraJointAxisLimit translationX,
            AuraJointAxisLimit translationY,
            AuraJointAxisLimit translationZ,
            AuraJointAxisLimit rotationX,
            AuraJointAxisLimit rotationY,
            AuraJointAxisLimit rotationZ)
        {
            TranslationX = translationX;
            TranslationY = translationY;
            TranslationZ = translationZ;
            RotationX = rotationX;
            RotationY = rotationY;
            RotationZ = rotationZ;
        }

        public AuraJointAxisLimit TranslationX { get; }
        public AuraJointAxisLimit TranslationY { get; }
        public AuraJointAxisLimit TranslationZ { get; }
        public AuraJointAxisLimit RotationX { get; }
        public AuraJointAxisLimit RotationY { get; }
        public AuraJointAxisLimit RotationZ { get; }

        public AuraJointAxisLimit Get(int axis)
        {
            switch (axis)
            {
                case 0: return TranslationX;
                case 1: return TranslationY;
                case 2: return TranslationZ;
                case 3: return RotationX;
                case 4: return RotationY;
                case 5: return RotationZ;
                default: throw new ArgumentOutOfRangeException(nameof(axis));
            }
        }

        public bool IsValid(bool pyramidSwing)
        {
            for (var axis = 0; axis < 6; axis++)
                if (!Get(axis).IsValid(axis, pyramidSwing))
                    return false;
            return true;
        }
    }
}
