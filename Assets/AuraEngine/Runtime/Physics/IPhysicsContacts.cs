using System;
using AuraEngine.Core;

namespace AuraEngine.Physics
{
    public interface IPhysicsContacts
    {
        int CopyContacts(Span<AuraContact> buffer);
    }
}
