using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace LIVE.Prototype.Editor
{
    public static class PrototypeChargeSmokeCheck
    {
        private static int assertions,timeoutDraws;
        private static readonly string[] Ids={"hyunwoo","yuki"};
        public static void Validate(BattlefieldPrototype board)
        {
            assertions=timeoutDraws=0;
            Check(PrototypeCharacterSkills.Load().Definitions.Count(e=>e.Implemented)==30,"Only fifteen character pairs executable");
            for(int star=1;star<=3;star++){Hyunwoo(star);Yuki(star);Buttons(star);}
            ReservationWhileMoving();
            ChargeLifecycle();
            foreach(string id in Ids)foreach(int star in new[]{1,2,3})ShopMerge(board,id,star);
            Simulations();
            Debug.Log($"CHARGE_SMOKE_CHECK_PASSED: {assertions} assertions; Hyunwoo/Yuki 1-3 stars, reserve/consume/followup order, CC/movement, keyed charges/unique refill/reset, timed buff refresh, real shop merges to2/3 stars and24 seeded mixed simulations ({timeoutDraws} timeout draws).");
        }
        private static void Equip(PrototypeUnit u,string id,int star,string kind=null)
        {
            var abilities=JsonUtility.FromJson<PrototypeAbilitySettings>(JsonUtility.ToJson(PrototypeWebRoster.Load().CreateGameData().Definition(id).Abilities));
            if(kind!=null)abilities.Skills=abilities.Skills.Where(s=>s.Id.EndsWith("."+kind)).ToArray();
            u.ConfigureAbilities(abilities,star);u.Stats.MaxHealth=10000;u.Stats.AttackPower=100;u.Stats.Defense=20;u.Stats.AttackSpeed=1;u.Stats.AttackRange=6;
        }
        private static void Hold(PrototypeUnit u)=>u.ApplyStatus("test.hold",1,1,100,PrototypeStatusKind.CrowdControl);
        private static void Release(PrototypeUnit u)=>u.ApplyStatus("test.hold",-1,1,1);
        private static void Ticks(Fixture f,int n){for(int i=0;i<n;i++)f.Step();}
        private static PrototypeSkillRuntime Skill(PrototypeUnit u,string id)=>u.Skills.Single(s=>s.Definition.Id==id);
        private static void Attacks(Fixture f,PrototypeUnit u,int n){int ticks=0;while(u.Statistics.BasicAttackCount<n&&ticks++<2000)f.Step();Check(u.Statistics.BasicAttackCount==n,"Requested attack count");}
        private static void Hyunwoo(int star)
        {
            using(var f=new Fixture())
            {
                Equip(f.A,"hyunwoo",star);f.B.Stats.MaxHealth=100000;f.Start();Hold(f.B);f.A.SetHealth(5000);var active=Skill(f.A,"web.hyunwoo.active");
                Attacks(f,f.A,1);Check(!active.NextBasicReserved,"Hyunwoo first no reserve");Attacks(f,f.A,2);Check(!active.NextBasicReserved,"Hyunwoo second no reserve");
                Attacks(f,f.A,3);Check(active.NextBasicReserved&&active.CastCount==0&&f.A.CurrentHealth==5000&&Math.Abs(f.A.CombatDefense-20)<.001,"Third reserves only; no heal or Bluff");
                Hold(f.A);Ticks(f,20);Check(active.NextBasicReserved&&active.CastCount==0,"CC cannot consume reservation");Release(f.A);
                Attacks(f,f.A,4);int extra=PrototypeDamageCalculator.RoundAmount(100*new[]{1.25,1.8,3}[star-1]);int heal=PrototypeDamageCalculator.RoundAmount(10000*new[]{.05,.07,.1}[star-1]);int defense=new[]{10,20,35}[star-1];
                Check(!active.NextBasicReserved&&active.CastCount==1&&f.B.CurrentHealth==100000-400-extra,"Fourth consumes AP coefficient once");
                Check(f.A.CurrentHealth==5000+heal&&Math.Abs(f.A.CombatDefense-(20+defense))<.001,"MaxHP heal and flat defense after actual consume");
                var events=f.Events;int hit=events.FindLastIndex(e=>e.Type==PrototypeCombatEventType.DamageDealt&&e.SkillId=="web.hyunwoo.active"),recovery=events.FindLastIndex(e=>e.Type==PrototypeCombatEventType.HealApplied&&e.SkillId=="web.hyunwoo.active"),buff=events.FindLastIndex(e=>e.Type==PrototypeCombatEventType.BuffApplied&&e.SkillId=="web.hyunwoo.passive");
                Check(hit<recovery&&recovery<buff,"Damage then heal then passive callback");Hold(f.A);Ticks(f,99);Check(f.A.CombatDefense>20,"Bluff before2 seconds");f.Step();Check(Math.Abs(f.A.CombatDefense-20)<.001,"Bluff exactly2 expires");
                Release(f.A);Attacks(f,f.A,6);Check(active.NextBasicReserved&&active.CastCount==1,"Web next cycle reserves sixth");Attacks(f,f.A,7);Check(active.CastCount==2,"Seventh consumes next cycle");
                f.A.Stats.AttackSpeed=4;Attacks(f,f.A,10);Check(active.CastCount==3&&Math.Abs(f.A.CombatDefense-(20+defense))<.001,"Rapid reapply refreshes instead of stacking");
                Hold(f.A);Ticks(f,99);Check(f.A.CombatDefense>20,"Refreshed duration holds");f.Step();Check(Math.Abs(f.A.CombatDefense-20)<.001,"Refreshed duration expires");
                Check(events.Count(e=>e.Type==PrototypeCombatEventType.NextAttackReserved)==3&&events.Count(e=>e.Type==PrototypeCombatEventType.NextAttackConsumed)==3,"No duplicate reservations");
            }
        }
        private static void Yuki(int star)
        {
            using(var f=new Fixture())
            {
                Equip(f.A,"yuki",star);f.B.Stats.MaxHealth=100000;f.Start();Hold(f.B);var active=Skill(f.A,"web.yuki.active");
                for(int n=1;n<=4;n++){Attacks(f,f.A,n);Check(!active.NextBasicReserved&&active.CastCount==0,"Yuki first four no reserve");}
                Attacks(f,f.A,5);Check(active.NextBasicReserved&&active.CastCount==0,"Fifth reserves, not head damage");
                Hold(f.A);Ticks(f,20);Check(active.NextBasicReserved&&active.CastCount==0,"Yuki CC preserves pending head");Release(f.A);
                int start=f.Events.Count;Attacks(f,f.A,6);var events=f.Events.Skip(start).ToList();
                int head=events.FindIndex(e=>e.Type==PrototypeCombatEventType.DamageDealt&&e.SkillId=="web.yuki.active");int cc=events.FindIndex(e=>e.Type==PrototypeCombatEventType.StatusApplied&&e.EffectKey=="yuki.head.cc");int button=events.FindIndex(e=>e.Type==PrototypeCombatEventType.StatusConsumed&&e.EffectKey=="yuki.buttons");int extra=events.FindIndex(e=>e.Type==PrototypeCombatEventType.DamageDealt&&e.SkillId=="web.yuki.passive");
                Check(head>=0&&cc>head&&button>cc&&extra>button,"Web head damage -> CC -> button consume -> button damage");
                Check(events[head].Amount==PrototypeDamageCalculator.RoundAmount(100*new[]{2,2.5,4}[star-1])&&events[extra].Amount==PrototypeDamageCalculator.RoundAmount(100*new[]{.5,.75,1.5}[star-1]),"Head/button star coefficients in same hit");
                Check(!active.NextBasicReserved&&active.CastCount==1&&f.A.StatusStacks("yuki.buttons")==0&&f.A.ChargeRefillPending("yuki.buttons"),"Independent reservation and charge state");
                Hold(f.A);Ticks(f,24);Check(f.B.StatusStacks("yuki.head.cc")==1&&f.A.StatusStacks("yuki.buttons")==0,"CC/refill before.5");f.Step();Check(f.B.StatusStacks("yuki.head.cc")==0&&f.A.StatusStacks("yuki.buttons")==2,"CC expires and resource refills at.5");
                Release(f.A);Attacks(f,f.A,10);Check(active.NextBasicReserved&&active.CastCount==1,"Tenth reserves next head");Attacks(f,f.A,11);Check(active.CastCount==2&&!active.NextBasicReserved,"Eleventh head exact next cycle");
            }
        }
        private static void Buttons(int star)
        {
            using(var f=new Fixture())
            {
                Equip(f.A,"yuki",star,"passive");f.A.Stats.AttackSpeed=4;f.B.Stats.MaxHealth=100000;f.Start();Hold(f.B);Check(f.A.StatusStacks("yuki.buttons")==2,"Initial two charges");
                Attacks(f,f.A,1);Check(f.A.StatusStacks("yuki.buttons")==1&&!f.A.ChargeRefillPending("yuki.buttons"),"First consumes one, no refill yet");
                Attacks(f,f.A,2);Check(f.A.StatusStacks("yuki.buttons")==0&&f.A.ChargeRefillPending("yuki.buttons"),"Second depletes, schedules refill");double due=f.Combat.ElapsedSeconds+.5;int cast=Skill(f.A,"web.yuki.passive").CastCount;
                Attacks(f,f.A,3);Check(Skill(f.A,"web.yuki.passive").CastCount==cast&&f.A.StatusStacks("yuki.buttons")==0,"Empty-charge attack has no extra damage");
                Hold(f.A);while(f.Combat.ElapsedSeconds+.020001<due)f.Step();Check(f.A.StatusStacks("yuki.buttons")==0,"No early refill");f.Step();Check(f.A.StatusStacks("yuki.buttons")==2&&!f.A.ChargeRefillPending("yuki.buttons"),"Exactly two at deadline, no duplicate/deferred refill");
                Ticks(f,100);Check(f.A.StatusStacks("yuki.buttons")==2,"Refill does not repeat or exceed cap");
                Release(f.A);Attacks(f,f.A,5);Check(f.A.ChargeRefillPending("yuki.buttons"),"Next depletion schedules one new refill");
                f.Combat.ResetBattle();Check(f.A.StatusStacks("yuki.buttons")==0&&!f.A.ChargeRefillPending("yuki.buttons"),"Reset clears resource/pending timer");f.Combat.StartBattle();Check(f.A.StatusStacks("yuki.buttons")==2,"Next combat starts with exactly two");
            }
        }
        private static void ChargeLifecycle()
        {
            var original=PrototypeWebRoster.Load().CreateGameData().Definition("yuki").Abilities;
            var copy=JsonUtility.FromJson<PrototypeAbilitySettings>(JsonUtility.ToJson(original));
            var charge=copy.Skills.Single(s=>s.Id=="web.yuki.passive").Charge;
            Check(charge.Key=="yuki.buttons"&&charge.Capacity==2&&charge.RefillSeconds==.5f,"Charge serialization round trip");
            foreach(bool death in new[]{false,true})using(var f=new Fixture())
            {
                Equip(f.A,"yuki",1);f.B.Stats.MaxHealth=100000;f.Start();Hold(f.B);Attacks(f,f.A,2);
                Check(f.A.ChargeRefillPending("yuki.buttons"),"Pending refill before terminal transition");
                if(death)f.A.SetHealth(0);else f.Combat.FinishAsDraw();
                Ticks(f,50);
                Check(f.A.StatusStacks("yuki.buttons")==0&&!f.A.ChargeRefillPending("yuki.buttons"),"Death/finish cancels refill permanently");
            }
        }
        private static void ReservationWhileMoving()
        {
            foreach(var id in Ids)using(var f=new Fixture())
            {
                Equip(f.A,id,1);f.B.Stats.MaxHealth=100000;f.Start();Hold(f.B);int count=id=="hyunwoo"?3:5;Attacks(f,f.A,count);f.A.Stats.AttackRange=1;int wait=0;while(!f.A.IsMoving&&wait++<100)f.Step();
                Check(f.A.IsMoving&&Skill(f.A,"web."+id+".active").NextBasicReserved,"Movement preserves pending attack");
                f.Combat.ResetBattle();Check(!Skill(f.A,"web."+id+".active").NextBasicReserved,"Reset clears pending attack");
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
            Check(Math.Abs(active.Definition.Effects[0].AttackRatio-(id=="hyunwoo"?new[]{1.25,1.8,3}[star-1]:new[]{2,2.5,4}[star-1]))<.0001,"Merged active coefficient");
            Check(Math.Abs((id=="hyunwoo"?passive.Definition.Effects[0].BuffAmount:passive.Definition.Effects[0].AttackRatio)-(id=="hyunwoo"?new[]{10,20,35}[star-1]:new[]{.5,.75,1.5}[star-1]))<.0001,"Merged passive coefficient");
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
                    {string id=new[]{"hyunwoo","yuki","jenny","kenneth"}[(row+team+seed)%4];var unit=f.Add(team==0?"A":"B",row,team==0?1:4,data.CombatStats(id,1+seed%3));unit.ConfigureAbilities(data.Definition(id).Abilities,1+seed%3);}
                    f.Combat.Initialize(3,6,f.Units,BattlefieldPrototype.Position,seed);f.Combat.StartBattle();int ticks=0;
                    while(f.Combat.State==PrototypeCombatState.Fighting&&ticks<3000)
                    {ticks++;f.Step();var cells=new HashSet<Vector2Int>();foreach(var unit in f.Units)if(unit.IsAlive)Check(cells.Add(unit.Cell)&&!double.IsNaN(unit.CombatAttackPower)&&!double.IsInfinity(unit.CombatAttackPower),"Mixed team occupancy/finite stats");}
                    if(f.Combat.State==PrototypeCombatState.Fighting)
                    {
                        Check(ticks==3000&&f.Combat.StallCount==0&&f.Units.Sum(u=>u.Statistics.DamageDealt)>0,"60s active combat, not a silent stall");
                        // Core fixture uses the same deadline operation as PrototypeRunController.
                        f.Combat.FinishAsDraw();timeoutDraws++;
                        Check(f.Combat.Winner==null,"Existing 60s Draw rule");
                    }
                    Check(f.Combat.State==PrototypeCombatState.Finished,"Mixed battle resolves by elimination or existing deadline");string result=f.Combat.Winner+"|"+ticks+"|"+f.Combat.StatisticsSummary();Check(previous==null||previous==result,"Mixed same seed deterministic");previous=result;
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
