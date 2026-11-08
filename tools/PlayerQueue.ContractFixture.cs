public class Job { }
public class JobQueue { public List<Job> jobs = new List<Job>(); }
public class FamilyMember { public JobQueue job_queue = new JobQueue(); }
public static partial class PlayerQueueContractFixture {
    private static readonly FieldInfo JobListField = typeof(JobQueue).GetField("jobs");
    private static FamilyMember Current = new FamilyMember();
    private static bool TryResolveMember(int id, out FamilyMember member, out string reason) { member = Current; reason = null; return true; }
    private static PlayerQueueSnapshot Capture(ActorId id, bool payload) { return null; }
    private static PlayerQueueSnapshot Capture(FamilyMember member, bool payload) { return null; }
    private static int GetCapacity(JobQueue queue) { return 3; }
    private static bool CanReplaceEmptyQueue(FamilyMember member, JobQueue queue) { return true; }
    private static bool TryDeserializeJobs(PlayerQueueSnapshot snapshot, FamilyMember member, int capacity, out List<Job> jobs, out string failure) { jobs = new List<Job> { new Job() }; failure = null; return true; }
    private static string BuildMutationStamp(JobQueue queue) { return queue.jobs.Count.ToString(); }
    private static void RaiseChanged(PlayerQueueChangeKind kind, PlayerQueueSnapshot snapshot) { }
    private static PlayerQueueRestoreResult Failed(string message, PlayerQueueSnapshot snapshot) { return new PlayerQueueRestoreResult(false, message, snapshot); }
    public static int Count;
    private static void Check(bool value, string message) { Count++; if (!value) throw new Exception(message); }
    public static void Verify() {
        var owner = new PlayerQueueOwnerIdentity(new ActorId(ActorKind.Player, 1, ""), 1, "fixture");
        var snapshot = new PlayerQueueSnapshot(true, owner, 3, null, null, null, new byte[] { 1 }, Current);
        Check(Restore(snapshot).Success && Current.job_queue.jobs.Count == 1, "A snapshot must restore into its original character instance");
        Current = new FamilyMember();
        Check(!Restore(snapshot).Success && Current.job_queue.jobs.Count == 0, "A reused member ID in another save must not accept the previous save's jobs");
    }
}
