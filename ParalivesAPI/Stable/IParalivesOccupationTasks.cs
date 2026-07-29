using System;
using ParalivesAPI.Core;

namespace ParalivesAPI.Stable
{
    public interface IParalivesOccupationTasks
    {
        ParalivesOccupationTaskEntry[] ReadActiveTasks(ulong characterGuid);

        ParalivesOccupationTaskEntry[] ReadActiveTasks(ulong characterGuid, ulong occupationGuid);

        ParalivesOccupationTaskAssignmentResult AssignTask(
            ulong characterGuid,
            ulong occupationGuid,
            ulong taskGuid);

        ParalivesOccupationTaskAssignmentResult AssignTask(
            ulong characterGuid,
            ulong occupationGuid,
            ulong taskGuid,
            ulong skillGuid);

        ParalivesOccupationTaskAssignmentResult AssignTask(
            ulong characterGuid,
            ParalivesOccupationTaskDefinition definition);

        ParalivesOccupationTaskCompletionResult CompleteTask(
            ulong characterGuid,
            ulong occupationGuid,
            ulong taskGuid);

        ParalivesOccupationTaskCompletionResult CompleteMatchingTask(
            ulong characterGuid,
            ulong occupationGuid);

        ParalivesOccupationTaskCompletionResult CompleteMatchingTask(
            ulong characterGuid,
            ulong occupationGuid,
            ulong skillGuid);

        ParalivesOccupationTaskCompletionResult CompleteMatchingTask(
            ulong characterGuid,
            ulong occupationGuid,
            ulong skillGuid,
            ulong characterTargetGuid);

        ParalivesOccupationTaskCompletionResult CompleteMatchingTask(
            ulong characterGuid,
            ulong occupationGuid,
            Predicate<ParalivesOccupationTaskEntry> predicate);
    }
}
