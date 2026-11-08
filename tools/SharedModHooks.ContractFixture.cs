// Game stand-ins let the contract gate execute production bridge logic without a game install.
namespace ModAPI.Core { public static class ModRuntime { public static bool IsQuitting; } }
namespace ModAPI.Util {
    public static class FixtureCollections {
        public static System.Collections.Generic.IReadOnlyList<T> ToReadOnlyList<T>(this System.Collections.Generic.List<T> values) { return values; }
    }
}
namespace UnityEngine { public class GameObject { } }
public class BasePanel { }
public class UIPanelManager { public static UIPanelManager instance; }
public class TradingPanel {
    public int Trades, Labels;
    private void UpdateTrade() { Trades++; ShelteredAPI.Events.UIEvents.RaiseTradeUpdated(this); }
    private void UpdateSelectionLabels() { Labels++; ShelteredAPI.Events.UIEvents.RaiseTradeSelectionLabelsUpdated(this); }
}
public class ExpeditionMainPanelNew { }
public class ExplorationManager {
    public static ExplorationManager Instance = new ExplorationManager();
    public int worldWidth = 100, worldHeight = 100, mapImageWidth = 1000, mapImageHeight = 1000;
}
public class BaseCharacter {
    public enum DamageType { Weapon, Bleeding, Radiation }
    public int health = 100;
    public void Heal() { } public void HealAfterUnconscious() { }
}
public class FamilyMember : BaseCharacter { public bool isAway; }
public class NpcVisitor : BaseCharacter { }
public class PartyMember { public FamilyMember person; }
public class ExplorationParty { public int id; }
namespace ShelteredAPI.Characters {
    internal static class PartyHelper {
        internal static int Notifications;
        internal static void NotifyPartyChanged(int id) { Notifications++; }
    }
}
public class BreachMan { public static BreachMan instance; public bool inProgress; }
public class ItemDefinition_Combat { public enum DamageTypeEnum { Undefined, Blunt, Sharp } }
public class EncounterCharacter {
    public enum BattleLocation { shelterBreach, aboveShelter, field }
    public bool isDead, isKnockedOut;
    public EncounterCharacter lastDamageFrom;
    public ItemDefinition_Combat.DamageTypeEnum lastDamageType;
    public int lastDamagedBy;
    public PartyMember partyMember;
    public NpcVisitor visitor;
    public BattleLocation GetCurrentBattleLocation;
}
public class Obj_Integrity {
    public float m_DegradeInterval = 10;
    public int Lost;
    public void Degrade(int amount) { Lost += amount; }
}
public class ExpeditionMap {
    public int width = 10, height = 10;
    public object[,] m_mapRegions = new object[10,10], m_mapScratchpad = new object[10,10];
    public float m_mapGridScaleX = .1f, m_mapGridScaleY = .1f;
    public int m_gridPixelSizeX = 100, m_gridPixelSizeY = 100;
    public int woodland = 8, mountains = 6, cities = 2, towns = 3, villages = 4;
    public int housingEstates = 2, loneBuildings = 2, shacks = 2, reservoirs = 2, lumberYards = 2;
    public int scrapYards = 2, clearings = 2, mountainPasses = 2, caves = 2, mines = 2, recyclingCentres = 2, restaurants = 2;
}
namespace HarmonyLib {
    public enum MethodType { Setter }
    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Method, AllowMultiple=true)]
    public class HarmonyPatch : System.Attribute {
        public System.Type TargetType; public string TargetMethod;
        public HarmonyPatch() { } public HarmonyPatch(System.Type type, string member) { TargetType = type; TargetMethod = member; }
        public HarmonyPatch(System.Type type, string member, MethodType kind) { }
    }
    public class HarmonyPostfix : System.Attribute { }
    public static class AccessTools {
        public static System.Reflection.FieldInfo Field(System.Type type, string name) { return type.GetField(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); }
        public static System.Reflection.MethodInfo Method(System.Type type, string name) { return type.GetMethod(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance); }
    }
    public class CodeInstruction {
        public System.Reflection.Emit.OpCode opcode;
        public object operand;
        public bool Calls(System.Reflection.MethodInfo method) { return object.Equals(operand, method); }
    }
}
namespace ModAPI.Harmony {
    public enum PatchDomain { Events, UI, Characters }
    public enum PatchStartupTiming { GameplayDeferred }
    public class PatchPolicyAttribute : System.Attribute {
        public PatchPolicyAttribute(PatchDomain domain, string name) { }
        public string TargetBehavior {get;set;} public string FailureMode {get;set;} public string RollbackStrategy {get;set;}
        public PatchStartupTiming StartupTiming {get;set;}
    }
}
namespace ShelteredAPI.Map { public struct ExpeditionMapGridPosition { public int X, Y; } }
namespace ShelteredAPI.Characters.Internal { internal static class CharacterEffectSystemImpl { internal static float NowSeconds() { return 0; } } }
public static class SharedModHooksContractFixture {
    public static int Count;
    private static void Check(bool condition, string description) { Count++; if (!condition) throw new System.Exception(description); }
    private static object Call(System.Type type, string method, params object[] arguments) {
        return type.GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, arguments);
    }
    public static void Verify() {
        Attributes(); Events(); Panels(); Wear(); Maps(); Parties();
    }
    private static void PartyPostfix(string target, params object[] args) {
        foreach (var method in typeof(ShelteredAPI.Characters.PartyPatches).GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)) {
            foreach (HarmonyLib.HarmonyPatch patch in method.GetCustomAttributes(typeof(HarmonyLib.HarmonyPatch), false))
                if (patch.TargetMethod == target) method.Invoke(null, args);
        }
    }
    private static void Parties() {
        // ExplorationManager.PartyHasReturned disbands only an existing party.
        ShelteredAPI.Characters.PartyHelper.Notifications = 0;
        PartyPostfix("DisbandExplorationParty", true, 7);
        PartyPostfix("PartyHasReturned", 7);
        Check(ShelteredAPI.Characters.PartyHelper.Notifications == 1, "Returning a party must publish one composition change");
        PartyPostfix("PartyHasReturned", 99);
        PartyPostfix("DisbandExplorationParty", false, 99);
        Check(ShelteredAPI.Characters.PartyHelper.Notifications == 1, "An absent party must not publish a composition change");
    }
    private static void Attributes() {
        var values = new ShelteredAPI.Characters.Internal.BasicCharacterAttributes();
        Check(values.ApplyTo("WalkSpeed", -2) == -2, "No contribution must preserve even unusual vanilla results");
        var add = values.Apply("walkspeed", 2, 0, "first");
        var mult = values.Apply("WalkSpeed", .5f, 0, "second"); mult.IsMultiplicative = true;
        Check(values.ApplyTo("WalkSpeed", 4) == 3, "Speed must combine additions then factors");
        Check(values.GetModifier("WalkSpeed") == 2, "Legacy additive accessor must not sum factors");
        mult.MinimumResult = 4; Check(values.ApplyTo("WalkSpeed", 4) == 4, "Shared minimum must apply after all factors");
        var restored = new ShelteredAPI.Characters.Internal.BasicCharacterAttributes(); restored.RestoreFromSaveData(values.ToSaveData());
        Check(restored.ApplyTo("WalkSpeed", 4) == 4, "Factor and floor must survive save roundtrip");
        Check(values.RemoveAllFromMod("SECOND") == 1 && values.ApplyTo("WalkSpeed", 4) == 6, "Mod removal must remove its factor only");
        values.Remove(add); var timed = values.Apply("WalkSpeed", 9, 1, "timer"); timed.TimeRemaining = 0;
        Check(values.ApplyTo("WalkSpeed", 4) == 4, "Expired contribution must not affect the game before cleanup");
        values.Tick(1); Check(values.GetModifiers("WalkSpeed").Count == 0, "Tick must remove expired handles");
        values.Apply("WalkSpeed", float.NaN, 0, "invalid"); Check(values.ApplyTo("WalkSpeed", 4) == 4, "Nonfinite contributions must not poison game speed");
    }
    private static void Events() {
        int deaths = 0, fatal = 0, ticks = 0;
        ShelteredAPI.Events.CharacterDeathEventArgs last = null;
        System.Action<ShelteredAPI.Events.CharacterDeathEventArgs> record = delegate(ShelteredAPI.Events.CharacterDeathEventArgs e) { deaths++; last=e; };
        ShelteredAPI.Events.GameEvents.OnCharacterDied += record;
        var person = new FamilyMember();
        bool captured = false;
        System.Action<FamilyMember> beforeFatal = delegate(FamilyMember member) { captured = member == person; };
        ShelteredAPI.Events.GameEvents.OnCharacterFatalDamageStarting += beforeFatal;
        Call(typeof(ShelteredAPI.Events.CharacterFatalDamageEventPatch), "Prefix", person);
        Check(captured, "Fatal damage prefix must expose the character before vanilla changes its job");
        ShelteredAPI.Events.GameEvents.OnCharacterFatalDamageStarting -= beforeFatal;
        ShelteredAPI.Events.GameEvents.RaiseDeath(person, true, BaseCharacter.DamageType.Radiation, "illness", false);
        Check(deaths == 0, "Surviving damage must not report death");
        person.health=0; ShelteredAPI.Events.GameEvents.RaiseDeath(person, true, BaseCharacter.DamageType.Radiation, "illness", false);
        Check(deaths == 1 && last.Victim == person && last.Killer == null && last.Context == "Shelter" && last.Details == "illness", "Environmental shelter death attribution");
        var killer = new EncounterCharacter { partyMember = new PartyMember { person = new FamilyMember() } };
        var victim = new EncounterCharacter { visitor = new NpcVisitor(), isDead = true, lastDamageFrom = killer, lastDamageType = ItemDefinition_Combat.DamageTypeEnum.Sharp, GetCurrentBattleLocation = EncounterCharacter.BattleLocation.shelterBreach };
        ShelteredAPI.Events.GameEvents.RaiseEncounterDeath(victim,true);
        Check(deaths==2 && last.Killer==killer.partyMember.person && last.EncounterVictim==victim && last.EncounterKiller==killer && last.Context=="Breach", "Combat victim and credited killer must survive mapping");
        Check(last.EncounterDamageType==ItemDefinition_Combat.DamageTypeEnum.Sharp, "Native damage category must be retained");
        victim.visitor.health=0; ShelteredAPI.Events.GameEvents.RaiseDeath(victim.visitor,true,BaseCharacter.DamageType.Weapon,"settlement",false);
        Check(deaths==2, "Encounter settlement must not report the same death twice");
        victim.isKnockedOut=true; ShelteredAPI.Events.GameEvents.RaiseEncounterDeath(victim,true); Check(deaths==2,"Stasis knockout must not report death");
        victim.isKnockedOut=false; victim.GetCurrentBattleLocation=EncounterCharacter.BattleLocation.field; victim.lastDamageFrom=null;
        ShelteredAPI.Events.GameEvents.RaiseEncounterDeath(victim,true); Check(last.Context=="ExpeditionEncounter" && last.Killer==null,"Unknown encounter killer must stay unknown");
        victim.visitor.health=10; ShelteredAPI.Events.GameEvents.ClearEncounterDeath(victim.visitor); victim.visitor.health=0;
        ShelteredAPI.Events.GameEvents.RaiseDeath(victim.visitor,true,BaseCharacter.DamageType.Bleeding,"later",true); Check(deaths==4,"Healing must release settlement suppression for later death");
        System.Action<FamilyMember,bool> onFatal=delegate(FamilyMember m,bool allowed) { if(m==person && !allowed) fatal++; };
        System.Action<ExplorationManager> onTick=delegate(ExplorationManager m) { if(m==ExplorationManager.Instance) ticks++; };
        ShelteredAPI.Events.GameEvents.OnCharacterFatalDamage+=onFatal; ShelteredAPI.Events.GameEvents.OnExpeditionTick+=onTick;
        ShelteredAPI.Events.GameEvents.RaiseFatalDamage(person,false); ShelteredAPI.Events.GameEvents.RaiseExpeditionTick(ExplorationManager.Instance);
        Check(fatal==1,"Fatal damage must report unconsciousness result"); Check(ticks==1,"Expedition tick must retain manager identity");
        ShelteredAPI.Events.GameEvents.OnCharacterFatalDamage-=onFatal; ShelteredAPI.Events.GameEvents.OnExpeditionTick-=onTick; ShelteredAPI.Events.GameEvents.OnCharacterDied-=record;
    }
    private static void Panels() {
        var trade = new TradingPanel(); var party = new ExpeditionMainPanelNew(); int shown=0, updated=0, labels=0, totals=0;
        System.Action<TradingPanel> onTrade=delegate(TradingPanel p) { totals++; ShelteredAPI.Events.UIEvents.RefreshTradingPanel(p); };
        System.Action<TradingPanel> onLabels=delegate(TradingPanel p) { labels++; };
        System.Action<ExpeditionMainPanelNew> onShow=delegate(ExpeditionMainPanelNew p) { if(p==party) shown++; };
        System.Action<ExpeditionMainPanelNew> onUpdate=delegate(ExpeditionMainPanelNew p) { if(p==party) updated++; };
        ShelteredAPI.Events.UIEvents.OnTradeUpdated+=onTrade; ShelteredAPI.Events.UIEvents.OnTradeSelectionLabelsUpdated+=onLabels;
        ShelteredAPI.Events.UIEvents.OnExpeditionPanelShown+=onShow; ShelteredAPI.Events.UIEvents.OnExpeditionPanelUpdated+=onUpdate;
        ShelteredAPI.Events.UIEvents.RefreshTradingPanel(trade);
        Check(trade.Trades==1 && trade.Labels==1 && totals==1 && labels==1,"Trade refresh must dispatch both updates and prevent recursive refresh");
        Call(typeof(ShelteredAPI.Events.ExpeditionPanelShownPatch),"Postfix",party); Call(typeof(ShelteredAPI.Events.ExpeditionPanelUpdatedPatch),"Postfix",party);
        Check(shown==1 && updated==1,"Party show and update bridges must retain panel identity");
        ModAPI.Core.ModRuntime.IsQuitting=true; ShelteredAPI.Events.UIEvents.RefreshTradingPanel(trade); Check(trade.Trades==1,"Trade refresh must stop during shutdown"); ModAPI.Core.ModRuntime.IsQuitting=false;
        ShelteredAPI.Events.UIEvents.OnTradeUpdated-=onTrade; ShelteredAPI.Events.UIEvents.OnTradeSelectionLabelsUpdated-=onLabels;
        ShelteredAPI.Events.UIEvents.OnExpeditionPanelShown-=onShow; ShelteredAPI.Events.UIEvents.OnExpeditionPanelUpdated-=onUpdate;
    }
    private static void Wear() {
        var target=new Obj_Integrity(); var patch=typeof(ShelteredAPI.Events.ObjectWearEventPatch);
        Call(patch,"ApplyTimedWear",target,3); Check(target.Lost==3 && target.m_DegradeInterval==10,"Unsubscribed wear must remain vanilla");
        System.Action<ShelteredAPI.Events.ObjectDegradingEventArgs> first=delegate(ShelteredAPI.Events.ObjectDegradingEventArgs e) { e.WearAmount+=2; e.Interval*=2; throw new System.Exception("wear fixture"); };
        System.Action<ShelteredAPI.Events.ObjectDegradingEventArgs> second=delegate(ShelteredAPI.Events.ObjectDegradingEventArgs e) { e.WearAmount*=2; e.Interval*=3; };
        ShelteredAPI.Events.GameEvents.OnObjectDegrading+=first; ShelteredAPI.Events.GameEvents.OnObjectDegrading+=second;
        Call(patch,"ApplyTimedWear",target,3); Check(target.Lost==13 && target.m_DegradeInterval==60,"Wear handlers must share adjustments in dispatch order despite exceptions");
        var error=new System.Exception("original"); object[] args={target.m_DegradeInterval,(float?)10,error};
        Check(object.ReferenceEquals(Call(patch,"Finalizer",args),error) && (float)args[0]==10,"Wear finalizer must restore interval and preserve original exception");
        ShelteredAPI.Events.GameEvents.OnObjectDegrading-=first; ShelteredAPI.Events.GameEvents.OnObjectDegrading-=second;
        var instruction=new HarmonyLib.CodeInstruction { opcode=System.Reflection.Emit.OpCodes.Callvirt, operand=typeof(Obj_Integrity).GetMethod("Degrade") };
        var list=new System.Collections.Generic.List<HarmonyLib.CodeInstruction> {instruction};
        Call(patch,"Transpiler",list); Check(instruction.opcode==System.Reflection.Emit.OpCodes.Call && ((System.Reflection.MethodInfo)instruction.operand).Name=="ApplyTimedWear","Timed call must be adapted in place");
        var unknown=new HarmonyLib.CodeInstruction(); Call(patch,"Transpiler",(object)new [] { unknown }); Check(unknown.operand==null,"Unknown game IL shape must remain untouched");
    }
    private static void Maps() {
        var map=new ExpeditionMap(); var regions=map.m_mapRegions;
        Check(ShelteredAPI.Map.Internal.MapSizeGenerationScope.Begin(map)==null && map.m_mapRegions==regions,"No map contributions must preserve array identity");
        var invalid=new ShelteredAPI.Map.MapSizePolicy { SourceId="fixture",PolicyId="bad",Width=0 };
        Check(!ShelteredAPI.Map.Internal.MapGenerationPolicyRegistry.Register(invalid).Success,"Invalid size must be rejected before registration");
        int completed=0;
        var policy=new ShelteredAPI.Map.MapSizePolicy { SourceId="fixture",PolicyId="size",Width=20,Height=30,TerrainMultiplier=.5f,ContentMultiplier=2,GenerationFinished=delegate(ExpeditionMap m,System.Exception error) { completed++; Check(m.cities==4,"Generation callback must see temporary counts"); } };
        Check(ShelteredAPI.Map.Internal.MapGenerationPolicyRegistry.Register(policy).Success,"Valid map size policy must register");
        policy.Width=99; var snapshot=ShelteredAPI.Map.Internal.MapGenerationPolicyRegistry.Resolve(); Check(snapshot.Width==20,"Registry must own a policy copy");
        var scope=ShelteredAPI.Map.Internal.MapSizeGenerationScope.Begin(map);
        Check(map.width==20 && map.height==30 && map.m_mapRegions.GetLength(0)==20 && map.m_mapScratchpad.GetLength(1)==30,"Map dimensions and both arrays must agree");
        Check(map.m_mapGridScaleX==.2f && map.m_gridPixelSizeY==33,"Map coordinate and pixel scales must track explicit dimensions");
        Check(map.woodland==4 && map.cities==4,"Terrain and content factors must be independent");
        scope.Finish(new System.Exception("generation")); scope.Finish(null);
        Check(completed==1 && map.woodland==8 && map.cities==2,"Generation finalizer must notify once and restore counts on failure");
        Check(map.width==20 && map.m_mapRegions.GetLength(0)==20,"Generated dimensions must remain after count restoration");
        ShelteredAPI.Map.Internal.MapGenerationPolicyRegistry.Clear("fixture");
        var manager=ExplorationManager.Instance; ExplorationManager.Instance=null;
        ShelteredAPI.Map.Internal.MapGenerationPolicyRegistry.Register(new ShelteredAPI.Map.MapSizePolicy { SourceId="fixture",PolicyId="rollback",Width=40 });
        regions=map.m_mapRegions; Check(ShelteredAPI.Map.Internal.MapSizeGenerationScope.Begin(map)==null && map.width==20 && map.m_mapRegions==regions,"Failed setup must restore dimensions and arrays");
        ExplorationManager.Instance=manager; ShelteredAPI.Map.Internal.MapGenerationPolicyRegistry.Clear("fixture");
    }
}
