using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace LIVE.Prototype.Editor
{
    public static class PrototypeNadineSmokeCheck
    {
        private static int assertions,timeoutDraws;
        private static readonly System.Reflection.FieldInfo Cooldown = typeof(PrototypeUnit).GetField("attackCooldown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        private static bool SafeCadence(PrototypeUnit unit,int ticks)
        {
            float value=(float)Cooldown.GetValue(unit);
            return value>=0&&!float.IsNaN(value)&&!float.IsInfinity(value)&&unit.Statistics.BasicAttackCount<=ticks;
        }
        public static void Validate(BattlefieldPrototype board)
        {
            assertions=timeoutDraws=0;
            for(int star=1;star<=3;star++){Rules(star);Coexistence(star);ShopMerge(board,"nadine",star);}
            Coalescing();Simulations();
            Debug.Log($"NADINE_SMOKE_CHECK_PASSED: {assertions} assertions; 1-3 stars, wild stacks/stat modifiers, limited attacks, CC/retarget/reset, real shop merges and24 deterministic simulations ({timeoutDraws} timeout draws).");
        }
        private static void Equip(PrototypeUnit u,string id,int star,string kind=null)
        {
            var abilities=JsonUtility.FromJson<PrototypeAbilitySettings>(JsonUtility.ToJson(PrototypeWebRoster.Load().CreateGameData().Definition(id).Abilities));
            if(kind!=null)abilities.Skills=abilities.Skills.Where(s=>s.Id.EndsWith("."+kind)).ToArray();
            u.ConfigureAbilities(abilities,star);u.Stats.MaxHealth=10000;u.Stats.AttackPower=100;u.Stats.Defense=20;u.Stats.AttackSpeed=1;u.Stats.AttackRange=6;u.Stats.SkillAmplification=200;
        }
        private static void Hold(PrototypeUnit u)=>u.ApplyStatus("test.hold",1,1,100,PrototypeStatusKind.CrowdControl);
        private static void Release(PrototypeUnit u)=>u.ApplyStatus("test.hold",-1,1,1);
        private static void Ticks(Fixture f,int n){for(int i=0;i<n;i++)f.Step();}
        private static PrototypeSkillRuntime Skill(PrototypeUnit u,string id)=>u.Skills.Single(s=>s.Definition.Id==id);
        private static void Attacks(Fixture f,PrototypeUnit u,int n){int ticks=0;while(u.Statistics.BasicAttackCount<n&&ticks++<2000)f.Step();Check(u.Statistics.BasicAttackCount==n,"Requested attack count");}
        private static void Rules(int star)
        {
            using(var f=new Fixture())
            {
                Equip(f.A,"nadine",star);var other=f.Add("B",0,5);f.B.Stats.MaxHealth=other.Stats.MaxHealth=100000;
                f.Start();Hold(f.A);Hold(f.B);Hold(other);var active=Skill(f.A,"web.nadine.active");double coefficient=new[]{.025,.0375,.05}[star-1];
                Check(f.A.StatusStacks("nadine.wild")==0&&f.A.EffectiveStats.AttackSpeed==1&&!active.NextBasicReserved,"Initial wild0, base AS, no wolf");
                Ticks(f,49);Check(f.A.StatusStacks("nadine.wild")==0,"No gain before first integer second");f.Step();
                Check(f.A.StatusStacks("nadine.wild")==2&&Math.Abs(f.A.EffectiveStats.AttackSpeed-(1+2*coefficient))<.00001,"First +2 and immediate AS");
                Ticks(f,300);Check(f.A.StatusStacks("nadine.wild")==14&&!active.NextBasicReserved,"Seven seconds14, no early activation");Ticks(f,50);
                Check(f.A.StatusStacks("nadine.wild")==15&&Math.Abs(f.A.EffectiveStats.AttackSpeed-(1+15*coefficient))<.00001&&active.ReservedAttacksRemaining==3&&active.ReservationActivationCount==1,"Eight seconds cap15/three hits, even CC");
                Ticks(f,100);Check(active.ReservedAttacksRemaining==3&&f.A.StatusStacks("nadine.wild")==15&&active.CastCount==0,"CC holds all charges; capped growth no duplicate modifiers");
                f.A.ApplyStatus("nadine.wild",-5,15,1,permanent:true);Check(Math.Abs(f.A.EffectiveStats.AttackSpeed-(1+10*coefficient))<.00001,"Stack decrement immediately recomputes");
                f.A.ApplyStatus("nadine.wild",5,15,1,permanent:true);Release(f.A);
                Attacks(f,f.A,1);int bonus=PrototypeDamageCalculator.RoundAmount(100*new[]{1,1.5,3}[star-1]);
                Check(active.ReservedAttacksRemaining==2&&active.CastCount==1&&f.B.CurrentHealth==100000-100-bonus&&f.A.StatusStacks("nadine.wild")==15,"First real basic AP extra, no wild consumption");
                f.B.SetHealth(0);Hold(f.A);Ticks(f,50);Check(active.ReservedAttacksRemaining==2,"External target death/CC retains remaining attacks");Release(f.A);
                Attacks(f,f.A,3);Check(active.CastCount==3&&active.ReservedAttacksRemaining==0&&!active.NextBasicReserved&&other.CurrentHealth==100000-200-2*bonus,"Retarget consumes exactly three independent skill hits");
                Attacks(f,f.A,5);Check(active.CastCount==3&&active.ReservationActivationCount==1&&f.A.StatusStacks("nadine.wild")==15,"No fourth extra or reactivation at cap; attacks do not add wild");
                Check(f.Events.Count(e=>e.Type==PrototypeCombatEventType.NextAttackReserved)==1&&f.Events.Count(e=>e.Type==PrototypeCombatEventType.NextAttackConsumed)==3,"One grant/three consumes, no recursive basic trigger");
                f.Combat.FinishAsDraw();Check(f.A.StatusStacks("nadine.wild")==0&&f.A.EffectiveStats.AttackSpeed==1,"Finish clears stacks/modifier");f.Combat.ResetBattle();f.Combat.StartBattle();Check(f.A.StatusStacks("nadine.wild")==0&&Skill(f.A,"web.nadine.active").ReservationActivationCount==0,"Next battle fresh");
            }
        }
        private static void Coexistence(int star)
        {
            using(var f=new Fixture())
            {
                Equip(f.A,"nadine",star);var data=PrototypeWebRoster.Load().CreateGameData();
                var combined=JsonUtility.FromJson<PrototypeAbilitySettings>(JsonUtility.ToJson(data.Definition("nadine").Abilities));
                combined.Skills=combined.Skills.Concat(data.Definition("laura").Abilities.Skills.Where(x=>x.Id.EndsWith(".passive"))).ToArray();f.A.ConfigureAbilities(combined,star);
                Equip(f.B,"kenneth",star);f.B.Stats.MaxHealth=100000;f.Start();Hold(f.B);
                Attacks(f,f.A,1);Check(f.A.EffectiveStats.AttackSpeed==2,"Laura reservation before wild");double first=f.Combat.ElapsedSeconds;
                // Actual timed AS effect from the existing Kenneth definition, already star-resolved.
                var timed=Skill(f.B,"web.kenneth.active").Definition.Effects.Single(x=>x.Type==PrototypeSkillEffectType.StatBuff&&x.Stat==PrototypeBuffStat.AttackSpeed);
                f.A.ApplyBuff(timed);double rage=new[]{.2,.3,.6}[star-1];
                Check(Math.Abs(f.A.EffectiveStats.AttackSpeed-2*(1+rage))<.00001,"Timed and reservation multiply");
                Attacks(f,f.A,2);double wild=new[]{.025,.0375,.05}[star-1];
                Check(Math.Abs(f.Combat.ElapsedSeconds-first-1)<.021,"AS changes do not retroactively alter existing cooldown");
                Check(Math.Abs(f.A.EffectiveStats.AttackSpeed-(1+rage)*(1+2*wild))<.00001,"Consume Laura removes only reservation, keeps timed+wild");
                Hold(f.A);Ticks(f,251);Check(Math.Abs(f.A.EffectiveStats.AttackSpeed-(1+f.A.StatusStacks("nadine.wild")*wild))<.00001,"Timed expiration preserves live stack modifier");
                f.A.SetHealth(0);Check(f.A.StatusStacks("nadine.wild")==0&&f.A.EffectiveStats.AttackSpeed==1,"Death removes all live stack/reservation modifiers");
            }
        }
        private static void Coalescing()
        {
            using(var f=new Fixture())
            {
                Equip(f.A,"nadine",1);f.Start();Hold(f.A);Hold(f.B);f.Combat.Step(3.2f);
                Check(f.A.StatusStacks("nadine.wild")==2,"Skipped whole seconds produce one gain like web");Ticks(f,10);Check(f.A.StatusStacks("nadine.wild")==2,"No catch-up burst");Ticks(f,30);Check(f.A.StatusStacks("nadine.wild")==4,"Next whole second resumes gain");
            }
        }
        private static void ShopMerge(BattlefieldPrototype board,string id,int star)
        {
            var c=board.RunController;c.SetDataMode(PrototypeDataMode.WebRoster);c.Data.Rules.StartingCredits=10000;c.ResetRun();foreach(var owned in c.Run.Player.OwnedUnits.ToArray())c.Sell(owned.InstanceId);
            int upgrades=0;while(c.Data.Odds(c.Run.Player.Mastery.Level)[c.Data.Definition(id).Cost-1]==0&&upgrades++<200)Check(c.Invest(),"Unlock real cost odds");
            int required=new[]{1,3,9}[star-1],bought=0;
            for(int roll=0;roll<2000&&bought<required;roll++)
            {for(int slot=0;slot<c.Run.Shop.Count&&bought<required;slot++)if(c.Run.Shop.Slot(slot)==id){Check(c.Buy(slot),"Buy real merge copy");bought++;}if(bought<required)Check(c.Reroll(),"Paid real reroll");}
            Check(bought==required&&c.Run.Player.OwnedUnits.Count==1,"Actual three-copy recursive merge");var ownedUnit=c.Run.Player.OwnedUnits.Single();Check(ownedUnit.Stars==star&&ownedUnit.Location==PrototypeUnitLocation.Bench,"Merged star and bench");
            Check(c.Move(ownedUnit.InstanceId,PrototypeUnitLocation.Board,1,1)&&c.Ready(),"Deploy actual merged character and Ready");
            foreach(var unit in board.Units){unit.Stats.MaxHealth=100000;unit.SetHealth(100000);}
            for(int n=0;n<900;n++)c.Step(.02f);var a=board.Units.Single(u=>u.Faction=="A");var active=Skill(a,"web."+id+".active");var passive=Skill(a,"web."+id+".passive");
            Check(active.CastCount>0&&passive.CastCount>0,"Both real skills execute after actual merge");
            Check(Math.Abs(active.Definition.Effects[0].AttackRatio-new[]{1,1.5,3}[star-1])<.0001,"Merged active coefficient");
            Check(Math.Abs(passive.Definition.StackModifiers[0].AmountPerStack-new[]{.025,.0375,.05}[star-1])<.0001,"Merged passive coefficient");
            c.SetDataMode(PrototypeDataMode.TestFixtures);c.SetDataMode(PrototypeDataMode.WebRoster);
        }
        private static void Simulations()
        {
            for(int seed=0;seed<12;seed++)
            {
                string previous=null;
                for(int repeat=0;repeat<2;repeat++)using(var f=new Fixture(false))
                {
                    var data=PrototypeWebRoster.Load().CreateGameData();
                    for(int row=0;row<3;row++)for(int team=0;team<2;team++)
                    {string id=(row==0&&team==0?"nadine":new[]{"laura","kenneth","ian","jenny"}[(row+team+seed)%4]);var unit=f.Add(team==0?"A":"B",row,team==0?1:4,data.CombatStats(id,1+seed%3));unit.ConfigureAbilities(data.Definition(id).Abilities,1+seed%3);}
                    f.Combat.Initialize(3,6,f.Units,BattlefieldPrototype.Position,seed);f.Combat.StartBattle();int ticks=0;
                    while(f.Combat.State==PrototypeCombatState.Fighting&&ticks<3000)
                    {ticks++;f.Step();var cells=new HashSet<Vector2Int>();foreach(var unit in f.Units)if(unit.IsAlive)Check(cells.Add(unit.Cell)&&SafeCadence(unit,ticks)&&!double.IsNaN(unit.CombatAttackPower)&&!double.IsInfinity(unit.CombatAttackPower)&&!float.IsNaN(unit.EffectiveStats.AttackSpeed)&&!float.IsInfinity(unit.EffectiveStats.AttackSpeed)&&unit.EffectiveStats.AttackSpeed>0,"Mixed team occupancy/finite stats");}
                    if(f.Combat.State==PrototypeCombatState.Fighting)
                    {
                        Check(ticks==3000&&f.Combat.StallCount==0&&f.Units.Sum(u=>u.Statistics.DamageDealt)>0,"60s active combat, not a silent stall");
                        // Core fixture uses the same deadline operation as PrototypeRunController.
                        f.Combat.FinishAsDraw();timeoutDraws++;
                        Check(f.Combat.Winner==null,"Existing 60s Draw rule");
                    }
                    Check(f.Units.All(u=>u.Skills.All(r=>!r.NextBasicReserved&&r.ReservedAttacksRemaining==0)),"No reservation leak after finish");Check(f.Combat.State==PrototypeCombatState.Finished,"Mixed battle resolves by elimination or existing deadline");string result=f.Combat.Winner+"|"+ticks+"|"+f.Combat.StatisticsSummary();Check(previous==null||previous==result,"Mixed same seed deterministic");previous=result;
                }
            }
        }
        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root = new GameObject("Ability smoke fixture");
            public readonly List<PrototypeUnit> Units = new List<PrototypeUnit>();
            public readonly List<PrototypeCombatEvent> Events = new List<PrototypeCombatEvent>();
            public PrototypeUnit A => Units[0];
            public PrototypeUnit B => Units[1];
            public readonly PrototypeCombatController Combat;
            public Fixture(bool defaultPair = true)
            {
                Combat = root.AddComponent<PrototypeCombatController>(); Combat.enabled = false;
                Combat.EventRaised += Events.Add;
                if (defaultPair) { Add("A", 1, 1); Add("B", 1, 4); }
            }
            public PrototypeUnit Add(string faction, int row, int column, PrototypeCombatStats stats = null)
            {
                var obj = new GameObject("Ability test " + faction); obj.transform.SetParent(root.transform);
                var unit = obj.AddComponent<PrototypeUnit>();
                unit.Initialize(faction, row, column, stats ?? new PrototypeCombatStats { MaxHealth = 1000, Defense = 0, AttackRange = 3 }, null, null);
                Units.Add(unit); return unit;
            }
            public void Start() { Events.Clear(); Combat.Initialize(3, 6, Units, BattlefieldPrototype.Position, 2468); Combat.StartBattle(); }
            public void Step() => Combat.Step(0.02f);
            public void Dispose() => UnityEngine.Object.DestroyImmediate(root);
        }
        private static void Check(bool value,string message){assertions++;if(!value)throw new Exception("Charge smoke: "+message);}
    }
}
