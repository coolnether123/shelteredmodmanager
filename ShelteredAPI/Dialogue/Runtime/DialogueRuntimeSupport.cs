using System;

namespace ShelteredAPI.Dialogue.Runtime
{
    internal sealed class SystemDialogueClock : IDialogueClock
    {
        public float TimeSeconds
        {
            get { return Environment.TickCount / 1000f; }
        }

        public int CurrentDay
        {
            get { return 0; }
        }
    }

    internal sealed class SystemDialogueRandom : IDialogueRandom
    {
        private readonly Random _random;

        public SystemDialogueRandom()
            : this(Environment.TickCount)
        {
        }

        public SystemDialogueRandom(int seed)
        {
            _random = new Random(seed == 0 ? 1234567 : seed);
        }

        public float Range(float minInclusive, float maxInclusive)
        {
            if (maxInclusive <= minInclusive)
                return minInclusive;

            return minInclusive + ((float)_random.NextDouble() * (maxInclusive - minInclusive));
        }

        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                return minInclusive;

            return _random.Next(minInclusive, maxExclusive);
        }
    }

    internal sealed class DialogueDisposable : IDisposable
    {
        public static readonly IDisposable Empty = new DialogueDisposable(null);

        private Action _dispose;

        public DialogueDisposable(Action dispose)
        {
            _dispose = dispose;
        }

        public void Dispose()
        {
            Action dispose = _dispose;
            _dispose = null;
            if (dispose != null)
                dispose();
        }
    }
}
