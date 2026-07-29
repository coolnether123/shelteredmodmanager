namespace ParalivesAPI.Native
{
    public interface IParalivesNativeApi
    {
    }

    public static class ParalivesNativeBoundary
    {
        public const string Namespace = "ParalivesAPI.Native";

        public const string Contract =
            "APIs in this namespace may intentionally expose raw Paralives runtime types.";
    }
}
