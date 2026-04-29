using System;
using ModAPI.Core;
using ShelteredAPI.Saves;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioSaveDescriptorMirror
    {
        public void Mirror(CustomScenarioInfo info)
        {
            if (info == null || string.IsNullOrEmpty(info.Id))
                return;

            try
            {
                ScenarioRegistry.RegisterScenario(new ScenarioDescriptor
                {
                    id = info.Id,
                    displayName = info.DisplayName,
                    description = info.Description,
                    version = info.Version
                });
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ScenarioSaveDescriptorMirror.MirrorSaveScenarioDescriptor." + info.Id, ex.Message);
            }
        }
    }
}
