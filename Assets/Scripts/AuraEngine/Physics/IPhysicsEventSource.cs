using System;
using AuraEngine.Core;

namespace AuraEngine.Physics
{
    public interface IPhysicsEventSource
    {
        int PendingEventCount { get; }

        int CopyEvents(Span<AuraPhysicsEvent> buffer);
    }
}
