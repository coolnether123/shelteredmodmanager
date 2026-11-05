[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
function Read-Method([string]$path, [string]$signature) {
    $text = Get-Content (Join-Path $root $path) -Raw
    $start = $text.IndexOf($signature, [StringComparison]::Ordinal)
    if ($start -lt 0) { throw "Missing production method: $signature" }
    $open = $text.IndexOf('{', $start)
    $depth = 1
    $end = $open + 1
    while ($depth -gt 0) {
        if ($text[$end] -eq '{') { $depth++ }
        if ($text[$end] -eq '}') { $depth-- }
        $end++
    }
    $text.Substring($start, $end - $start)
}
$map = 'ShelteredAPI/Map/Internal/HomeShelterMapGenerationPatches.cs'
$scenario = 'ShelteredAPI/Scenarios/Infrastructure/Harmony/ShelteredCustomScenarioPatches.cs'
$chatter = 'ShelteredAPI/Scenarios/Infrastructure/Harmony/ScenarioConversationChatterPatches.cs'
$ui = 'ShelteredAPI/UI/Compatibility/UIPatches.cs'
$methods = @(
    (Read-Method $map 'private static IEnumerable<CodeInstruction> HomeOriginTranspiler('),
    (Read-Method $map 'private static IEnumerable<CodeInstruction> ShelterClearanceTranspiler('),
    (Read-Method $scenario 'private static void SurvivalChosenPrefix('),
    (Read-Method $scenario 'private static void SlotSelectionCancelPrefix('),
    (Read-Method $scenario 'private static void SlotSelectionCancelPostfix('),
    (Read-Method $scenario 'private static void CustomisationCancelPrefix('),
    (Read-Method $chatter 'private static bool SayRandomCommentPrefix('),
    (Read-Method $chatter 'private static void SayRandomCommentPostfix('),
    (Read-Method $ui 'static void Postfix_UIPanelManager_PushPanel('),
    (Read-Method $ui 'static void Postfix_UIPanelManager_PopPanel(')
) -join "`n"
# Exercise production hooks with accepted/rejected transitions and matching/drifted IL.
# Fixtures model the state boundaries traced in the supplied game source.
Add-Type -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
public struct Vector2 {
    public Vector2(float x, float y) {}
    public static Vector2 zero { get { return new Vector2(); } }
}
public class ExpeditionMap {
    public class GridRef { public GridRef(int x, int y) {} }
    public GridRef WorldPosToGridRef(Vector2 position) { return null; }
    public void CreateMap() {}
    public void FindClearSpace() {}
}
public class FactionMan { public void GrowFactionZone() {} }
public static class AccessTools {
    public static FieldInfo Field(Type t, string name) { return t.GetField(name, BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); }
    public static MethodInfo Method(Type t, string name) { return t.GetMethod(name, BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); }
    public static MethodInfo Method(Type t, string name, Type[] args) { return t.GetMethod(name, args); }
    public static ConstructorInfo Constructor(Type t, Type[] args) { return t.GetConstructor(args); }
    public static MethodInfo PropertyGetter(Type t, string name) { return t.GetProperty(name).GetGetMethod(); }
}
public class CodeInstruction {
    public OpCode opcode; public object operand;
    public readonly List<string> labels = new List<string>();
    public CodeInstruction(OpCode op, object value = null) { opcode = op; operand = value; }
    public bool Calls(MethodInfo method) { return (opcode == OpCodes.Call || opcode == OpCodes.Callvirt) && Equals(operand, method); }
}
public class FluentTranspiler {
    List<CodeInstruction> code;
    public static FluentTranspiler For(IEnumerable<CodeInstruction> code, MethodBase original) { return new FluentTranspiler { code = code.ToList() }; }
    public IEnumerable<CodeInstruction> Instructions() { return code; }
    public void ReplaceAtWithCall(int i, MethodInfo method) { code[i].opcode = OpCodes.Call; code[i].operand = method; }
    public IEnumerable<CodeInstruction> Build(bool strict, bool validateStack) { return code; }
}
public static class MMLog {
    public static int warnings;
    public static void WriteWarning(string message) { warnings++; }
    public static void WriteInfo(string message) {}
}
public static class HomeShelterMapGenerationRuntime {
    public static ExpeditionMap.GridRef HomeOriginGrid(ExpeditionMap map, Vector2 pos) { return null; }
    public static ExpeditionMap.GridRef HomeClearanceGrid(int x, int y) { return null; }
}
public class SlotSelectionPanel {}
public class CustomisationPanel {}
public class SaveManager {
    public enum SaveType { Slot1 }
    public static SaveManager instance = new SaveManager();
    public bool isLoading, isDeleting;
}
public static class ShelteredCustomScenarioRuntimeState {
    public static int cleared;
    public static void ClearPendingCustomScenario() { cleared++; }
}
public static class SlotPagingScopeResolver {
    public static void ClearRememberedScenarioSelections() {}
    public static void ForgetScenarioSelection(SlotSelectionPanel panel) {}
}
public class BaseCharacter {
    public bool isInCoversation;
}
public class FamilyMember : BaseCharacter {
    public FamilyMember interactWith;
}
public class FamilyManager {
    public static FamilyManager Instance = new FamilyManager();
    public List<FamilyMember> members = new List<FamilyMember>();
    public List<FamilyMember> GetAllFamilyMembers() { return members; }
}
public class ScenarioConversationRuntimeService {
    public bool authored, suppressBantz;
    public bool TryHandleRandomComment(FamilyMember member, out string speech) { speech = null; return authored; }
    public bool ShouldSuppressGenericBantz() { return suppressBantz; }
}
public class BasePanel {}
public static class UIPanelLifecycleRuntimeService {
    public static int opened, closed;
    public static void RaisePanelOpened(BasePanel panel) { opened++; }
    public static void RaisePanelClosed(BasePanel panel) { closed++; }
}
public static class GamePatchContracts {
    private static readonly FieldInfo InConversationField = AccessTools.Field(typeof(BaseCharacter), "isInCoversation");
    private static readonly FieldInfo ConversationPartnerField = AccessTools.Field(typeof(FamilyMember), "interactWith");
    private sealed class ConversationState { internal bool InConversation; internal FamilyMember Partner; }
    static ScenarioConversationRuntimeService service = new ScenarioConversationRuntimeService();
    static ScenarioConversationRuntimeService ResolveService() { return service; }
    static SaveManager.SaveType[] GetScenarioStartupSaveTypes() { return new[] { SaveManager.SaveType.Slot1 }; }
    static int deleted;
    static void ClearQueuedStartupSave(SaveManager.SaveType type) { deleted++; }
    $methods
    static int passed;
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); passed++; }
    static List<CodeInstruction> OriginCall(bool getter) {
        var code = new List<CodeInstruction>();
        if (getter) code.Add(new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(Vector2), "zero")));
        else {
            code.Add(new CodeInstruction(OpCodes.Ldc_R4, 0f));
            code.Add(new CodeInstruction(OpCodes.Ldc_R4, 0f));
            code.Add(new CodeInstruction(OpCodes.Newobj, AccessTools.Constructor(typeof(Vector2), new[] { typeof(float), typeof(float) })));
        }
        code.Add(new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(ExpeditionMap), "WorldPosToGridRef", new[] { typeof(Vector2) })));
        code[code.Count - 1].labels.Add("branch-target");
        return code;
    }
    public static int Run() {
        var map = AccessTools.Method(typeof(ExpeditionMap), "CreateMap");
        var faction = AccessTools.Method(typeof(FactionMan), "GrowFactionZone");
        var home = AccessTools.Method(typeof(HomeShelterMapGenerationRuntime), "HomeOriginGrid");
        var conversion = AccessTools.Method(typeof(ExpeditionMap), "WorldPosToGridRef", new[] { typeof(Vector2) });
        var code = HomeOriginTranspiler(OriginCall(false), map).ToList();
        Require(code[3].Calls(home) && code[3].labels.Count == 1, "literal-zero rewrite preserves branch target");
        code = HomeOriginTranspiler(OriginCall(true), map).ToList();
        Require(code[1].Calls(home), "zero-getter rewrite");
        var input = OriginCall(true); input.AddRange(OriginCall(false));
        code = HomeOriginTranspiler(input, faction).ToList();
        Require(code[1].Calls(home) && code[5].Calls(conversion), "faction candidate conversion remains literal geometry");
        int warnings = MMLog.warnings;
        input = OriginCall(false); input[0].operand = 1f;
        code = HomeOriginTranspiler(input, map).ToList();
        Require(code[3].Calls(conversion) && MMLog.warnings == warnings + 1, "changed origin is reported and left intact");
        warnings = MMLog.warnings;
        input = OriginCall(true); input.AddRange(OriginCall(false));
        code = HomeOriginTranspiler(input, map).ToList();
        Require(code[1].Calls(conversion) && MMLog.warnings == warnings + 1, "extra conversion is reported and left intact");
        warnings = MMLog.warnings;
        HomeOriginTranspiler(new CodeInstruction[0], map).ToList();
        Require(MMLog.warnings == warnings + 1, "missing origin is reported");
        var constructor = AccessTools.Constructor(typeof(ExpeditionMap.GridRef), new[] { typeof(int), typeof(int) });
        input = new List<CodeInstruction> { new CodeInstruction(OpCodes.Newobj, constructor) };
        input[0].labels.Add("clearance");
        code = ShelterClearanceTranspiler(input, map).ToList();
        Require(code[0].opcode == OpCodes.Call && code[0].labels.Count == 1, "clearance constructor rewrite preserves metadata");
        warnings = MMLog.warnings;
        input = new List<CodeInstruction> { new CodeInstruction(OpCodes.Newobj, constructor), new CodeInstruction(OpCodes.Newobj, constructor) };
        code = ShelterClearanceTranspiler(input, map).ToList();
        Require(code.All(x => x.opcode == OpCodes.Newobj) && MMLog.warnings == warnings + 1, "ambiguous clearance constructor is reported");
        SurvivalChosenPrefix(false);
        Require(ShelteredCustomScenarioRuntimeState.cleared == 0, "locked survival choice keeps pending scenario");
        SurvivalChosenPrefix(true);
        Require(ShelteredCustomScenarioRuntimeState.cleared == 1, "accepted survival choice clears pending scenario");
        var panel = new SlotSelectionPanel(); bool state;
        SaveManager.instance.isLoading = true; SlotSelectionCancelPrefix(true, out state); SlotSelectionCancelPostfix(panel, state, true);
        SaveManager.instance.isLoading = false; SaveManager.instance.isDeleting = true; SlotSelectionCancelPrefix(true, out state); SlotSelectionCancelPostfix(panel, state, true);
        SaveManager.instance.isDeleting = false; SlotSelectionCancelPrefix(false, out state); SlotSelectionCancelPostfix(panel, state, true);
        Require(ShelteredCustomScenarioRuntimeState.cleared == 1, "busy and locked slot cancellation keeps pending scenario");
        SlotSelectionCancelPrefix(true, out state); SlotSelectionCancelPostfix(panel, state, false);
        Require(ShelteredCustomScenarioRuntimeState.cleared == 1, "archive-only cancellation keeps pending scenario");
        SlotSelectionCancelPrefix(true, out state); SlotSelectionCancelPostfix(panel, state, true);
        Require(ShelteredCustomScenarioRuntimeState.cleared == 2, "accepted slot cancellation clears pending scenario");
        var custom = new CustomisationPanel();
        CustomisationCancelPrefix(custom, true, 0); CustomisationCancelPrefix(custom, false, 1);
        Require(deleted == 0, "ignored cancel and previous-page navigation keep queued save");
        CustomisationCancelPrefix(custom, false, 0);
        Require(deleted == 1, "accepted first-page cancel deletes queued startup save");
        var initiator = new FamilyMember(); var partner = new FamilyMember(); var existingPartner = new FamilyMember();
        partner.isInCoversation = true; partner.interactWith = existingPartner;
        FamilyManager.Instance.members.AddRange(new[] { initiator, partner });
        service.suppressBantz = true; string result = null; Dictionary<FamilyMember, ConversationState> previous;
        Require(SayRandomCommentPrefix(initiator, ref result, out previous), "category suppression still allows native comment selection");
        initiator.interactWith = partner; initiator.isInCoversation = true; partner.interactWith = initiator;
        SayRandomCommentPostfix(initiator, string.Empty, previous);
        Require(!initiator.isInCoversation && partner.isInCoversation && partner.interactWith == existingPartner, "empty illness fallback restores both prior conversation states");
        service.authored = true;
        Require(!SayRandomCommentPrefix(initiator, ref result, out previous) && previous == null, "authored conversation skips native state restoration");
        var popup = new BasePanel(); var stack = new List<BasePanel>();
        Postfix_UIPanelManager_PushPanel(popup, stack);
        Require(UIPanelLifecycleRuntimeService.opened == 0, "rejected deferred push emits no open");
        stack.Add(popup); Postfix_UIPanelManager_PushPanel(popup, stack); Postfix_UIPanelManager_PopPanel(popup);
        Require(UIPanelLifecycleRuntimeService.opened == 1 && UIPanelLifecycleRuntimeService.closed == 1, "completed panel transitions publish events");
        return passed;
    }
}
"@ -ReferencedAssemblies System.Core
$count = [GamePatchContracts]::Run()
$project = Get-Content (Join-Path $root 'ShelteredAPI/ShelteredAPI.csproj') -Raw
if ($project.Contains('SaveManager_SaveGlobalData_Patch.cs')) { throw 'Preference saves still install the game-save overwrite hook.' }
$count++
Write-Host "Game patch contracts: $count passed, 0 failed."
