using System;

namespace AuraEngine.Core
{
    public sealed class AuraException : Exception
    {
        public AuraException(AuraResult result, string message)
            : base(message)
        {
            Result = result;
        }

        public AuraResult Result { get; }

        public static AuraException FromResult(AuraResult result, string message)
        {
            if (result == AuraResult.Success)
                throw new ArgumentOutOfRangeException(nameof(result), result, "Success is not an error.");

            return new AuraException(result, message);
        }

        public static void ThrowIfFailed(AuraResult result, string message)
        {
            if (result != AuraResult.Success)
                throw FromResult(result, message);
        }
    }
}
