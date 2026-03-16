using UnityEngine;

namespace ShelteredAPI.Input
{
    internal enum UnityScrollGestureKind
    {
        None,
        MouseWheel,
        Indirect
    }

    internal struct UnityScrollGestureSample
    {
        public readonly Vector2 Delta;
        public readonly UnityScrollGestureKind Kind;

        public UnityScrollGestureSample(Vector2 delta, UnityScrollGestureKind kind)
        {
            Delta = delta;
            Kind = kind;
        }

        public bool HasScroll
        {
            get { return Kind != UnityScrollGestureKind.None; }
        }
    }
}
