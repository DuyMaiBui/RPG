using RPG.Core.Actors;
using UnityEngine;

namespace RPG.Unity
{
    public sealed class ProjectileView : MonoBehaviour
    {
        public void ApplySnapshot(ProjectileSnapshot snapshot)
        {
            transform.position = new Vector3(snapshot.Position.X, snapshot.Position.Y, transform.position.z);
            gameObject.SetActive(true);
        }

        public void Release()
        {
            gameObject.SetActive(false);
        }
    }
}
