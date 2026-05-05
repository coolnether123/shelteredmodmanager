using UnityEngine;


using ShelteredAPI.Hooks;
namespace ShelteredAPI.UI.FieldManual.Animations
{
    /// <summary>
    /// Reusable transition contract for field-manual panel regions.
    /// </summary>
    internal interface IFieldManualTransition
    {
        void Play(GameObject target);
        void Complete(GameObject target);
    }
}
