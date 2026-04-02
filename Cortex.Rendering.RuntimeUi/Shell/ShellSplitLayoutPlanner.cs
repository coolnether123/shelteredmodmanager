using Cortex.Rendering.Models;

namespace Cortex.Rendering.RuntimeUi.Shell
{
    public sealed class ShellSplitLayoutPlan
    {
        public RenderRect FirstRect;
        public RenderRect SplitterRect;
        public RenderRect SecondRect;
        public float SplitPoint;
    }

    public static class ShellSplitLayoutPlanner
    {
        public static ShellSplitLayoutPlan BuildHorizontal(RenderRect bounds, float splitRatio, float splitterThickness, float minFirstSize, float minSecondSize)
        {
            var maxFirstSize = Max(minFirstSize + 1f, bounds.Width - minSecondSize - splitterThickness);
            var splitPoint = Clamp(bounds.Width * splitRatio, minFirstSize, maxFirstSize);
            return BuildHorizontalFromSplitPoint(bounds, splitPoint, splitterThickness);
        }

        public static ShellSplitLayoutPlan BuildHorizontalFromSplitPoint(RenderRect bounds, float splitPoint, float splitterThickness)
        {
            var clampedSplitPoint = Clamp(splitPoint, 0f, bounds.Width);
            var plan = new ShellSplitLayoutPlan();
            plan.SplitPoint = clampedSplitPoint;
            plan.FirstRect = new RenderRect(bounds.X, bounds.Y, clampedSplitPoint, bounds.Height);
            plan.SplitterRect = new RenderRect(bounds.X + clampedSplitPoint, bounds.Y, splitterThickness, bounds.Height);
            plan.SecondRect = new RenderRect(
                plan.SplitterRect.X + splitterThickness,
                bounds.Y,
                Max(0f, bounds.Width - clampedSplitPoint - splitterThickness),
                bounds.Height);
            return plan;
        }

        public static float ResolveHorizontalDragMaxSplitPoint(RenderRect bounds, float minFirstSize, float minSecondSize)
        {
            return Max(minFirstSize + 1f, bounds.Width - minSecondSize);
        }

        public static ShellSplitLayoutPlan BuildVertical(RenderRect bounds, float splitRatio, float splitterThickness, float minFirstSize, float minSecondSize)
        {
            var maxFirstSize = Max(minFirstSize + 1f, bounds.Height - minSecondSize - splitterThickness);
            var splitPoint = Clamp(bounds.Height * splitRatio, minFirstSize, maxFirstSize);
            return BuildVerticalFromSplitPoint(bounds, splitPoint, splitterThickness);
        }

        public static ShellSplitLayoutPlan BuildVerticalFromSplitPoint(RenderRect bounds, float splitPoint, float splitterThickness)
        {
            var clampedSplitPoint = Clamp(splitPoint, 0f, bounds.Height);
            var plan = new ShellSplitLayoutPlan();
            plan.SplitPoint = clampedSplitPoint;
            plan.FirstRect = new RenderRect(bounds.X, bounds.Y, bounds.Width, clampedSplitPoint);
            plan.SplitterRect = new RenderRect(bounds.X, bounds.Y + clampedSplitPoint, bounds.Width, splitterThickness);
            plan.SecondRect = new RenderRect(
                bounds.X,
                plan.SplitterRect.Y + splitterThickness,
                bounds.Width,
                Max(0f, bounds.Height - clampedSplitPoint - splitterThickness));
            return plan;
        }

        public static float ResolveVerticalDragMaxSplitPoint(RenderRect bounds, float minFirstSize, float minSecondSize)
        {
            return Max(minFirstSize + 1f, bounds.Height - minSecondSize);
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }

        private static float Max(float left, float right)
        {
            return left > right ? left : right;
        }
    }
}
