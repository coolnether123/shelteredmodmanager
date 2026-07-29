namespace ParalivesAPI.Unsafe
{
    public interface IParalivesUnsafeApi
    {
    }

    public static class ParalivesUnsafeBoundary
    {
        public const string Namespace = "ParalivesAPI.Unsafe";

        public const string Contract =
            "APIs in this namespace may bypass stable guards and can break across game updates.";
    }
}
