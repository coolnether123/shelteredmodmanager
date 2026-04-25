using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class SpritePatchValidator
    {
        public bool IsValid(SpritePatchDefinition patch)
        {
            if (patch == null || string.IsNullOrEmpty(patch.Id) || patch.Operations == null || patch.Operations.Count == 0)
                return false;

            for (int operationIndex = 0; operationIndex < patch.Operations.Count; operationIndex++)
            {
                SpritePatchOperation operation = patch.Operations[operationIndex];
                if (operation == null)
                    return false;

                if (operation.Kind == SpritePatchOperationKind.Pixels && (operation.Runs == null || operation.Runs.Count == 0))
                    return false;

                for (int runIndex = 0; operation.Runs != null && runIndex < operation.Runs.Count; runIndex++)
                {
                    SpritePatchDeltaRun run = operation.Runs[runIndex];
                    if (run == null || !run.IsValid())
                        return false;
                }
            }

            return true;
        }
    }
}
