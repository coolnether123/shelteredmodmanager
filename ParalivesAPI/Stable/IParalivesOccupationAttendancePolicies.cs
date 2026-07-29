using System;
using ParalivesAPI.Core;

namespace ParalivesAPI.Stable
{
    public interface IParalivesOccupationAttendancePolicies
    {
        int RegisteredPolicyCount { get; }

        int RegisteredLegacyPolicyCount { get; }

        int RegisteredDecisionPolicyCount { get; }

        IDisposable Register(ParalivesAttendancePolicy policy);

        IDisposable Register(ParalivesOccupationAttendanceDecisionPolicy policy);

        bool Unregister(ParalivesAttendancePolicy policy);

        bool Unregister(ParalivesOccupationAttendanceDecisionPolicy policy);
    }
}
