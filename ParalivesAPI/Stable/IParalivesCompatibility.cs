using ParalivesAPI.Core;

namespace ParalivesAPI.Stable
{
    public interface IParalivesCompatibility
    {
        bool TryReadMember<T>(object source, string memberName, out T value);

        bool TryReadMember<T>(object source, out T value, params string[] candidateNames);

        T ReadMemberOrDefault<T>(object source, string memberName, T defaultValue);

        bool TryReadGuid(object source, out ulong value, params string[] candidateNames);

        ulong ReadGuidOrDefault(object source, ulong defaultValue, params string[] candidateNames);

        bool TryReadString(object source, out string value, params string[] candidateNames);

        string ReadStringOrDefault(object source, string defaultValue, params string[] candidateNames);

        bool TrySetMember(object target, string memberName, object value);

        bool TrySetMember(object target, object value, params string[] candidateNames);

        bool TryInvoke(object target, string methodName, params object[] args);

        bool TryCall<T>(object target, string methodName, out T result, params object[] args);

        bool TryCall<T>(object target, out T result, string[] candidateNames, params object[] args);

        bool TryCallAllowValueTypeReturn<T>(object target, string methodName, out T result, params object[] args);

        bool TryCallAllowValueTypeReturn<T>(object target, out T result, string[] candidateNames, params object[] args);

        ParalivesCompatibilityReport CheckMembers(object target, params string[] requiredMemberNames);
    }
}
