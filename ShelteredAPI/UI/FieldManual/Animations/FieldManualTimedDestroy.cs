using UnityEngine;

namespace ShelteredAPI.UI.FieldManual.Animations
{
    internal sealed class FieldManualTimedDestroy : MonoBehaviour
    {
        public float Lifetime = 0.2f;

        private float _startedAt;

        private void OnEnable()
        {
            _startedAt = Time.realtimeSinceStartup;
        }

        private void Update()
        {
            if (Time.realtimeSinceStartup - _startedAt >= Lifetime)
                Destroy(gameObject);
        }
    }
}
