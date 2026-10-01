using AuraEngine.Core;

namespace AuraEngine.Unity
{
    public interface IAuraPhysicsEventReceiver
    {
        void OnCollisionEnter(in AuraPhysicsEvent value);

        void OnCollisionExit(in AuraPhysicsEvent value);

        void OnTriggerEnter(in AuraPhysicsEvent value);

        void OnTriggerExit(in AuraPhysicsEvent value);
    }
}
