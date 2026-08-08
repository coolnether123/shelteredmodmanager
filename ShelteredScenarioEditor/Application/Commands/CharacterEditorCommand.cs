using System.Globalization;
using UnityEngine;
using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Infrastructure.Assets;

namespace ShelteredScenarioEditor.Application.Commands
{
    internal enum CharacterMemberScope { Starting, Future }
    internal enum CharacterEditorCommandKind
    {
        AddStarting, AddLiveStarting, OpenColorPicker, Remove, Move, Duplicate,
        CycleName, CycleGender, ToggleAdult, StepStat, SetStat, StepCondition, SetCondition,
        SetTrait, CycleTrait, RandomizePerson, RandomizeLook, CycleTexture, CycleColor, ApplyColor,
        CopyLook, CopyIdentity, ClearLook, ToggleField, StepField, SetFieldText, CycleFieldEnum,
        OpenFieldColorPicker, SetFieldColor
    }

    internal sealed class CharacterEditorCommand : ScenarioAuthoringCommand, IScenarioTextValueCommand
    {
        private CharacterEditorCommand(CharacterEditorCommandKind kind, CharacterMemberScope scope, int index, int delta, int actorId, string key, string value, bool flag, ScenarioEditorCharacterTexturePart texturePart, ScenarioEditorCharacterColorPart colorPart, Color color, bool hasColor, string id)
            : base(id, ScenarioAuthoringCommandPolicy.Default)
        {
            Kind = kind; Scope = scope; Index = index; Delta = delta; ActorId = actorId; Key = key; Value = value;
            Flag = flag; TexturePart = texturePart; ColorPart = colorPart; Color = color; HasColor = hasColor;
        }
        public CharacterEditorCommandKind Kind { get; private set; }
        public CharacterMemberScope Scope { get; private set; }
        public int Index { get; private set; }
        public int Delta { get; private set; }
        public int ActorId { get; private set; }
        public string Key { get; private set; }
        public string Value { get; private set; }
        public bool Flag { get; private set; }
        public ScenarioEditorCharacterTexturePart TexturePart { get; private set; }
        public ScenarioEditorCharacterColorPart ColorPart { get; private set; }
        public Color Color { get; private set; }
        public bool HasColor { get; private set; }

        public static CharacterEditorCommand AddStarting() { return Create(CharacterEditorCommandKind.AddStarting, CharacterMemberScope.Starting, -1); }
        public static CharacterEditorCommand AddLiveStarting(int actorId) { return new CharacterEditorCommand(CharacterEditorCommandKind.AddLiveStarting, CharacterMemberScope.Starting, -1, 0, actorId, null, null, false, default(ScenarioEditorCharacterTexturePart), default(ScenarioEditorCharacterColorPart), Color.clear, false, "character.add_live." + actorId.ToString(CultureInfo.InvariantCulture)); }
        public static CharacterEditorCommand OpenColorPicker(string channel) { return Data(CharacterEditorCommandKind.OpenColorPicker, CharacterMemberScope.Starting, -1, channel, null); }
        public static CharacterEditorCommand Member(CharacterEditorCommandKind kind, CharacterMemberScope scope, int index) { return Create(kind, scope, index); }
        public static CharacterEditorCommand Step(CharacterEditorCommandKind kind, CharacterMemberScope scope, int index, string key, int delta) { return new CharacterEditorCommand(kind, scope, index, delta, 0, key, null, false, default(ScenarioEditorCharacterTexturePart), default(ScenarioEditorCharacterColorPart), Color.clear, false, Id(kind, scope, index, key, delta.ToString(CultureInfo.InvariantCulture))); }
        public static CharacterEditorCommand ValueCommand(CharacterEditorCommandKind kind, CharacterMemberScope scope, int index, string key, string value) { return Data(kind, scope, index, key, value); }
        public static CharacterEditorCommand Trait(CharacterMemberScope scope, int index, bool strength, string value) { return new CharacterEditorCommand(CharacterEditorCommandKind.SetTrait, scope, index, 0, 0, strength ? "strength" : "weakness", value, strength, default(ScenarioEditorCharacterTexturePart), default(ScenarioEditorCharacterColorPart), Color.clear, false, Id(CharacterEditorCommandKind.SetTrait, scope, index, strength ? "strength" : "weakness", value)); }
        public static CharacterEditorCommand CycleTrait(CharacterMemberScope scope, int index, bool strength, int delta) { return new CharacterEditorCommand(CharacterEditorCommandKind.CycleTrait, scope, index, delta, 0, strength ? "strength" : "weakness", null, strength, default(ScenarioEditorCharacterTexturePart), default(ScenarioEditorCharacterColorPart), Color.clear, false, Id(CharacterEditorCommandKind.CycleTrait, scope, index, strength ? "strength" : "weakness", delta.ToString(CultureInfo.InvariantCulture))); }
        public static CharacterEditorCommand Texture(CharacterMemberScope scope, int index, ScenarioEditorCharacterTexturePart part, int delta) { return new CharacterEditorCommand(CharacterEditorCommandKind.CycleTexture, scope, index, delta, 0, null, null, false, part, default(ScenarioEditorCharacterColorPart), Color.clear, false, Id(CharacterEditorCommandKind.CycleTexture, scope, index, part.ToString(), delta.ToString(CultureInfo.InvariantCulture))); }
        public static CharacterEditorCommand StepColor(CharacterMemberScope scope, int index, ScenarioEditorCharacterColorPart part, int delta) { return new CharacterEditorCommand(CharacterEditorCommandKind.CycleColor, scope, index, delta, 0, null, null, false, default(ScenarioEditorCharacterTexturePart), part, Color.clear, false, Id(CharacterEditorCommandKind.CycleColor, scope, index, part.ToString(), delta.ToString(CultureInfo.InvariantCulture))); }
        public static CharacterEditorCommand ApplyColor(CharacterMemberScope scope, int index, ScenarioEditorCharacterColorPart part, Color color) { return new CharacterEditorCommand(CharacterEditorCommandKind.ApplyColor, scope, index, 0, 0, null, null, false, default(ScenarioEditorCharacterTexturePart), part, color, true, Id(CharacterEditorCommandKind.ApplyColor, scope, index, part.ToString(), ColorUtility.ToHtmlStringRGBA(color))); }
        public ScenarioAuthoringCommand WithTextValue(string value)
        {
            if (Kind == CharacterEditorCommandKind.ApplyColor)
            {
                Color parsed;
                if (ColorUtility.TryParseHtmlString(value != null && value.StartsWith("#") ? value : "#" + (value ?? string.Empty), out parsed))
                    return ApplyColor(Scope, Index, ColorPart, parsed);
            }
            return ValueCommand(Kind, Scope, Index, Key, value);
        }
        private static CharacterEditorCommand Create(CharacterEditorCommandKind kind, CharacterMemberScope scope, int index) { return Data(kind, scope, index, null, null); }
        private static CharacterEditorCommand Data(CharacterEditorCommandKind kind, CharacterMemberScope scope, int index, string key, string value) { return new CharacterEditorCommand(kind, scope, index, 0, 0, key, value, false, default(ScenarioEditorCharacterTexturePart), default(ScenarioEditorCharacterColorPart), Color.clear, false, Id(kind, scope, index, key, value)); }
        private static string Id(CharacterEditorCommandKind kind, CharacterMemberScope scope, int index, string key = null, string value = null) { return "character." + scope.ToString().ToLowerInvariant() + "." + index.ToString(CultureInfo.InvariantCulture) + "." + kind.ToString().ToLowerInvariant() + (string.IsNullOrEmpty(key) ? string.Empty : "." + ScenarioAutomationIdCodec.EncodeToken(key)) + (string.IsNullOrEmpty(value) ? string.Empty : "." + ScenarioAutomationIdCodec.EncodeToken(value)); }
    }
}
