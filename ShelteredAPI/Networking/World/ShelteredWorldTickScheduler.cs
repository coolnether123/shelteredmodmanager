using System;

namespace ShelteredAPI.Networking.World
{
    internal interface IShelteredWorldTickScheduler
    {
        ShelteredWorldTickAdvance AdvanceFixedSteps(long stepCount, int tickRate);

        ShelteredWorldTickAdvance AccumulateFixedInterval(float elapsedSeconds, int tickRate);

        void Reset();
    }

    internal sealed class ShelteredWorldTickAdvance
    {
        public long TicksToAdvance { get; set; }

        public float DeltaSeconds { get; set; }

        public double FractionalTicksRemaining { get; set; }
    }

    internal sealed class ShelteredWorldTickScheduler : IShelteredWorldTickScheduler
    {
        private const double NearIntegerToleranceTicks = 0.000001d;
        private const double FloorToleranceTicks = 0.000000001d;

        private readonly object _sync = new object();
        private double _fractionalTicks;

        public ShelteredWorldTickAdvance AdvanceFixedSteps(long stepCount, int tickRate)
        {
            int normalizedTickRate = NormalizeTickRate(tickRate);
            long normalizedStepCount = stepCount > 0 ? stepCount : 0;

            return new ShelteredWorldTickAdvance
            {
                TicksToAdvance = normalizedStepCount,
                DeltaSeconds = ToDeltaSeconds(normalizedStepCount, normalizedTickRate),
                FractionalTicksRemaining = GetFractionalTicks()
            };
        }

        public ShelteredWorldTickAdvance AccumulateFixedInterval(float elapsedSeconds, int tickRate)
        {
            int normalizedTickRate = NormalizeTickRate(tickRate);
            double normalizedElapsedSeconds = elapsedSeconds > 0f ? elapsedSeconds : 0d;
            long ticksToAdvance;
            double remaining;

            lock (_sync)
            {
                _fractionalTicks = NormalizeNearInteger(
                    _fractionalTicks + NormalizeNearInteger(normalizedElapsedSeconds * normalizedTickRate));
                ticksToAdvance = (long)Math.Floor(_fractionalTicks + FloorToleranceTicks);
                if (ticksToAdvance > 0)
                    _fractionalTicks -= ticksToAdvance;

                if (_fractionalTicks < FloorToleranceTicks)
                    _fractionalTicks = 0d;

                remaining = _fractionalTicks;
            }

            return new ShelteredWorldTickAdvance
            {
                TicksToAdvance = ticksToAdvance > 0 ? ticksToAdvance : 0,
                DeltaSeconds = ToDeltaSeconds(ticksToAdvance, normalizedTickRate),
                FractionalTicksRemaining = remaining
            };
        }

        public void Reset()
        {
            lock (_sync)
            {
                _fractionalTicks = 0d;
            }
        }

        private double GetFractionalTicks()
        {
            lock (_sync)
            {
                return _fractionalTicks;
            }
        }

        private static float ToDeltaSeconds(long ticks, int tickRate)
        {
            if (ticks <= 0)
                return 0f;

            return (float)((double)ticks / NormalizeTickRate(tickRate));
        }

        private static int NormalizeTickRate(int tickRate)
        {
            return tickRate > 0 ? tickRate : ShelteredMultiplayerWorldClock.DefaultTickRate;
        }

        private static double NormalizeNearInteger(double value)
        {
            double rounded = Math.Round(value);
            return Math.Abs(value - rounded) <= NearIntegerToleranceTicks ? rounded : value;
        }
    }
}
