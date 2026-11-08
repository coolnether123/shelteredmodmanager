using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using ShelteredAPI.Characters.Abstractions;
using ShelteredAPI.Characters.Models;
using ShelteredAPI.Characters.Internal;
namespace UnityEngine { public struct Color { public float r, g, b, a; public Color(float red, float green, float blue, float alpha) { r = red; g = green; b = blue; a = alpha; } } }
namespace ShelteredAPI.Characters.Abstractions { public enum CharacterSource { RealFamily, Visitor, Synthetic } }
public class FixtureStat { public int Level = 5; }
public class FixtureStats { public FixtureStat Strength = new FixtureStat(), Dexterity = new FixtureStat(), Intelligence = new FixtureStat(), Charisma = new FixtureStat(), Perception = new FixtureStat(); }
public class BaseCharacter {
    public string firstName = "first", lastName = "last", meshId = "man";
    public bool isMale = true, isAdult = true;
    public int health = 70, maxHealth = 100;
    public Color m_skinColor = new Color(1, 2, 3, 4), m_hairColor = new Color(4, 3, 2, 1), m_originalHairColor;
    public Color skinColor { get { return m_skinColor; } }
    public Color hairColor { get { return m_hairColor; } }
    public FixtureStats BaseStats = new FixtureStats();
    public void UpdateAvatarSprite() { }
}
public class FamilyMember : BaseCharacter { public int GetId() { return 1; } }
public class NpcVisitor : BaseCharacter { public int npcId = 2; }
public class CharacterMesh {
    public enum ColorCustomization { SkinColor, HairColor }
    public Color Skin, Hair;
    public void SetColor(ColorCustomization part, Color color) { if (part == ColorCustomization.SkinColor) Skin = color; else Hair = color; }
    public void RefreshColors() { }
}
namespace ShelteredAPI.Scenarios.Infrastructure.Assets {
    public static class ScenarioCharacterAppearanceService {
        public static CharacterMesh Mesh = new CharacterMesh();
        public static bool TryGetBody(BaseCharacter character, out CharacterMesh mesh) { mesh = Mesh; return true; }
        public static bool TrySetBody(BaseCharacter character, string id, out string message) { character.meshId = id; character.isMale = id == "man"; message = null; return true; }
    }
}
namespace ShelteredAPI.Characters.Internal {
    internal static class CharacterEffectSystemImpl { internal static float NowSeconds() { return 1; } }
}
public partial class CharacterAppearanceContractFixture {
    private readonly CharacterDataModel _data = new CharacterDataModel();
    private readonly CharacterDataModel _synced = new CharacterDataModel();
    public static int Count;
    private static void Check(bool value, string message) { Count++; if (!value) throw new Exception(message); }
    public static void Verify() {
        var member = new FamilyMember(); var visitor = new NpcVisitor();
        var family = CharacterFactory.CreateFromFamilyMember(member);
        family.CustomDataChanged = delegate { };
        var npc = CharacterFactory.CreateFromVisitor(visitor);
        Check(family.IsMale && family.MeshId == "man" && family.SkinColor.r == 1 && family.HairColor.r == 4, "Family data must read live appearance");
        Check(npc.IsMale && npc.MeshId == "man" && npc.SkinColor.r == 1 && npc.HairColor.r == 4, "Visitor data must read live appearance");
        var proxy = new CharacterAppearanceContractFixture();
        proxy.PullFromGame(family, false);
        proxy._data.MeshId = "woman"; proxy._data.IsMale = false;
        proxy._data.SkinColor = new Color(8, 7, 6, 5); proxy._data.HairColor = new Color(9, 8, 7, 6);
        proxy.PullFromGame(family, true);
        Check(!proxy._data.IsMale && proxy._data.MeshId == "woman" && proxy._data.SkinColor.r == 8 && proxy._data.HairColor.r == 9, "Refreshing the same character must preserve pending appearance edits");
        proxy.SyncAppearance(member);
        Check(!member.isMale && member.meshId == "woman" && member.skinColor.r == 8 && member.hairColor.r == 9 && member.m_originalHairColor.r == 9, "Appearance writes must reach the game character and saved hair identity");
        Check(ShelteredAPI.Scenarios.Infrastructure.Assets.ScenarioCharacterAppearanceService.Mesh.Skin.r == 8 && ShelteredAPI.Scenarios.Infrastructure.Assets.ScenarioCharacterAppearanceService.Mesh.Hair.r == 9, "Color writes must update the live mesh");
    }
}
