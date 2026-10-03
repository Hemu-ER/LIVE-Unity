using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace LIVE.Prototype.Editor
{
    public static class PrototypeWebSecondBatchSmokeCheck
    {
        private static int assertions, timeoutDraws;
        private static readonly string[] Ids={"kenneth","abigail","sua","marcus"};
        public static void Validate(BattlefieldPrototype board)
        {
            assertions=0;timeoutDraws=0;
            Check(PrototypeCharacterSkills.Load().Definitions.Count(e=>e.Implemented)==20,"Exactly ten executable character pairs");
            for(int star=1;star<=3;star++){Kenneth(star);Abigail(star);Sua(star);Marcus(star);}
            foreach(string id in Ids)Shop(board,id);
            Simulations();
            Debug.Log($"WEB_SECOND_BATCH_SMOKE_CHECK_PASSED: {assertions} assertions; Kenneth/Abigail/Sua/Marcus 1-3 star independent active/passive checks, actual HP damage/shield/overkill, surviving-hit counts, original hit target, stack consumption, CC deferral, real shop flow and 24 seeded mixed simulations ({timeoutDraws} timeout draws).");
        }
        private static void Equip(PrototypeUnit unit,string id,int star,string kind=null)
        {
            var def=PrototypeWebRoster.Load().CreateGameData().Definition(id);
            var settings=JsonUtility.FromJson<PrototypeAbilitySettings>(JsonUtility.ToJson(def.Abilities));
            if(kind!=null)settings.Skills=settings.Skills.Where(s=>s.Id.EndsWith("."+kind)).ToArray();
            unit.ConfigureAbilities(settings,star);unit.Stats.MaxHealth=10000;unit.Stats.Defense=0;unit.Stats.AttackPower=100;unit.Stats.SkillAmplification=100;unit.Stats.AttackRange=6;unit.Stats.AttackSpeed=2;
        }
        private static void Hold(PrototypeUnit unit,float seconds=100)=>unit.ApplyStatus("test.hold",1,1,seconds,PrototypeStatusKind.CrowdControl);
        private static void Ticks(Fixture f,int n){for(int i=0;i<n;i++)f.Step();}
        private static int Casts(PrototypeUnit unit,string id)=>unit.Skills.Single(s=>s.Definition.Id==id).CastCount;
        private static void Attacks(Fixture f,PrototypeUnit a,int count)
        {int limit=0;while(a.IsAlive&&a.Statistics.BasicAttackCount<count&&limit++<2000)f.Step();Check(a.Statistics.BasicAttackCount==count,"Required attack count reached");}
        private static void Kenneth(int star)
        {
            double rage=new[]{.2,.3,.6}[star-1];int coefficient=new[]{1,2,4}[star-1];
            using(var f=new Fixture())
            {
                Equip(f.A,"kenneth",star,"active");f.B.Stats.MaxHealth=100000;f.Start();Hold(f.B);
                Attacks(f,f.A,4);Check(Casts(f.A,"web.kenneth.active")==0&&f.A.Shield==0,"Kenneth no proc before fifth");
                Attacks(f,f.A,5);Check(Casts(f.A,"web.kenneth.active")==1&&f.A.Shield==100*coefficient,"Fifth attack shield pre-buff AP");
                Check(Math.Abs(f.A.CombatAttackPower-100*(1+rage))<.001&&Math.Abs(f.A.EffectiveStats.AttackSpeed-2*(1+rage))<.001,"Kenneth AP/AS star ratio");
                Attacks(f,f.A,9);Check(Casts(f.A,"web.kenneth.active")==1,"No sixth-ninth repeat");Attacks(f,f.A,10);
                Check(Casts(f.A,"web.kenneth.active")==2&&f.A.Shield==100*coefficient+PrototypeDamageCalculator.RoundAmount(100*(1+rage)*coefficient),"Tenth attack shield uses current AP");
                Check(Math.Abs(f.A.CombatAttackPower-100*(1+rage))<.001,"Rage refresh not stack");
                Hold(f.A);Ticks(f,249);Check(f.A.CombatAttackPower>100,"Rage before5");f.Step();Check(Math.Abs(f.A.CombatAttackPower-100)<.001&&Math.Abs(f.A.EffectiveStats.AttackSpeed-2)<.001,"Rage5 second expiry");
                Check(f.Events.Count(e=>e.Type==PrototypeCombatEventType.ShieldApplied&&e.SkillId=="web.kenneth.active")==2,"Shield events attributed");
            }
            foreach(int mode in new[]{0,1,2})using(var f=new Fixture(false))
            {
                var a=f.Add("A",1,1);var b=f.Add("B",1,4);var spare=f.Add("B",0,5);Equip(a,"kenneth",star,"passive");b.Stats.Defense=100;f.Start();Hold(b);Hold(spare);a.SetHealth(5000);
                if(mode==1)b.ApplyShield(40);if(mode==2)b.SetHealth(10);
                f.Step();int actual=mode==0?50:10;int healing=PrototypeDamageCalculator.RoundAmount(actual*new[]{.1,.15,.25}[star-1]);
                Check(a.CurrentHealth==5000+healing,"Kenneth actual HP damage lifesteal, excludes shield/overkill");
                Check(a.Statistics.HealingDone==healing&&f.Events.Any(e=>e.Type==PrototypeCombatEventType.HealApplied&&e.SkillId=="web.kenneth.passive"),"Lifesteal event/stat");
                int hp=a.CurrentHealth;spare.ReceiveDamage(100,a,skillId:"test.skill");Check(a.CurrentHealth==hp,"Skill damage never basic lifesteal");
            }
            using(var f=new Fixture())
            {
                Equip(f.A,"kenneth",star,"passive");f.Start();f.A.SetHealth(5000);f.B.SetHealth(10);f.Step();
                Check(f.Combat.State==PrototypeCombatState.Finished&&f.A.CurrentHealth==5000+PrototypeDamageCalculator.RoundAmount(10*new[]{.1,.15,.25}[star-1]),"Killing blow lifesteal settles");
            }
        }
        private static void Abigail(int star)
        {
            double shred=new[]{.05,.08,.15}[star-1],spin=new[]{1,1.5,2.5}[star-1];
            using(var f=new Fixture(false))
            {
                var a=f.Add("A",1,1);var b=f.Add("B",1,4);var other=f.Add("B",0,5);Equip(a,"abigail",star);b.Stats.MaxHealth=other.Stats.MaxHealth=10000;b.Stats.Defense=100;
                f.Start();Hold(b);Hold(other);
                for(int hit=1;hit<=3;hit++)
                {Attacks(f,a,hit);Check(Math.Abs(b.CombatDefense-100*Math.Pow(1-shred,hit))<.001,"Shred current defense multiplicatively accumulates");if(hit<3)Check(Casts(a,"web.abigail.active")==0,"Abigail before third");}
                var hits=f.Events.Where(e=>e.Type==PrototypeCombatEventType.DamageDealt&&e.SkillId=="web.abigail.active").ToArray();
                Check(hits.Length==2&&hits.Single(e=>e.Target==b).Amount==PrototypeDamageCalculator.Mitigate(100*spin,100*Math.Pow(1-shred,3),0)&&hits.Single(e=>e.Target==other).Amount==PrototypeDamageCalculator.RoundAmount(100*spin),"Spin all living enemies after third shred");
                Hold(a);Ticks(f,300);Check(Math.Abs(b.CombatDefense-100*Math.Pow(1-shred,3))<.001,"Shred permanent");
            }
            using(var f=new Fixture(false))
            {
                var a=f.Add("A",1,1);var b=f.Add("B",1,4);var spare=f.Add("B",0,5);Equip(a,"abigail",star);f.Start();Hold(b);Hold(spare);b.SetHealth(1);f.Step();
                Check(!b.IsAlive&&Math.Abs(spare.CombatDefense-spare.Stats.Defense)<.001&&!f.Events.Any(e=>e.Type==PrototypeCombatEventType.BuffApplied&&e.SkillId=="web.abigail.passive"),"Killed original target excluded; shred not transferred to retarget");
                Attacks(f,a,3);Check(Casts(a,"web.abigail.active")==0,"Dead-target basic excluded from web counter");Attacks(f,a,4);Check(Casts(a,"web.abigail.active")==1,"Three surviving hits trigger spin");
            }
            using(var f=new Fixture())
            {
                Equip(f.A,"abigail",star,"active");f.B.Stats.MaxHealth=10000;f.Start();Hold(f.B);Attacks(f,f.A,3);Check(Casts(f.A,"web.abigail.active")==1,"Abigail active independent");
            }
            using(var f=new Fixture())
            {
                Equip(f.A,"abigail",star,"passive");f.B.Stats.Defense=100;f.Start();Hold(f.B);f.Step();Check(Math.Abs(f.B.CombatDefense-100*(1-shred))<.001,"Abigail passive independent");
            }
        }
        private static void Sua(int star)
        {
            double odyssey=new[]{1.15,1.6,2.8}[star-1],lifesteal=new[]{.15,.25,.4}[star-1],mind=new[]{.25,.45,.85}[star-1],heal=new[]{.008,.015,.03}[star-1];
            using(var f=new Fixture(false))
            {
                var a=f.Add("A",1,1);var b=f.Add("B",1,4);var low=f.Add("B",1,5);var outside=f.Add("B",0,5);Equip(a,"sua",star,"active");b.Stats.MaxHealth=10000;b.Stats.Defense=100;
                f.Start();foreach(var u in f.Units)Hold(u);a.SetHealth(5000);low.SetHealth(10);low.ApplyShield(20);
                Ticks(f,199);Check(Casts(a,"web.sua.active")==0,"Sua before4");f.Step();
                int first=PrototypeDamageCalculator.RoundAmount(100*odyssey/2);int total=first+10;
                Check(Casts(a,"web.sua.active")==1&&b.CurrentHealth==10000-first&&!low.IsAlive&&outside.CurrentHealth==outside.MaxHealth,"Sua4 target row and outside exclusion");
                Check(a.CurrentHealth==5000+PrototypeDamageCalculator.RoundAmount(total*lifesteal),"Sua heals summed actual HP damage excluding shield/overkill");
                Ticks(f,199);Check(Casts(a,"web.sua.active")==1,"Sua before8");f.Step();Check(Casts(a,"web.sua.active")==2,"Sua8 exact next period");
                Check(f.Events.Count(e=>e.Type==PrototypeCombatEventType.HealApplied&&e.SkillId=="web.sua.active")==2,"Odyssey heal attribution");
            }
            foreach(bool lethal in new[]{false,true})using(var f=new Fixture(false))
            {
                var a=f.Add("A",1,1);var b=f.Add("B",1,4);var spare=f.Add("B",0,5);Equip(a,"sua",star,"passive");b.Stats.Defense=100;
                f.Start();Hold(b);Hold(spare);a.SetHealth(5000);if(lethal)b.SetHealth(1);f.Step();
                Check(a.CurrentHealth==5000+PrototypeDamageCalculator.RoundAmount(10000*heal),"Sua maximum HP heal every basic, including killed target");
                if(!lethal)Check(b.CurrentHealth==b.MaxHealth-50-PrototypeDamageCalculator.RoundAmount(100*mind/2),$"Sua AMP additional damage independent star={star} HP={b.CurrentHealth} expected={b.MaxHealth-50-PrototypeDamageCalculator.RoundAmount(100*mind/2)}");
                else Check(!b.IsAlive&&spare.CurrentHealth==spare.MaxHealth,"Sua extra damage never retargets after kill");
                Attacks(f,a,2);Check(Casts(a,"web.sua.passive")==2,"Sua passive each basic");
            }
        }
        private static void Marcus(int star)
        {
            double quake=new[]{1.5,2,3}[star-1],shock=new[]{1.5,2,3.5}[star-1];
            using(var f=new Fixture(false))
            {
                var a=f.Add("A",1,1);var b=f.Add("B",1,4);var other=f.Add("B",0,5);Equip(a,"marcus",star,"active");b.Stats.MaxHealth=other.Stats.MaxHealth=100000;
                f.Start();Hold(b);Hold(other);Hold(a,10.5f);Ticks(f,499);Check(Casts(a,"web.marcus.active")==0,"Marcus before10");f.Step();Check(Casts(a,"web.marcus.active")==0,"CC delays quake");
                while(f.Combat.ElapsedSeconds<10.499)f.Step();Check(Casts(a,"web.marcus.active")==1,"CC expiry releases due quake");
                var hits=f.Events.Where(e=>e.Type==PrototypeCombatEventType.DamageDealt&&e.SkillId=="web.marcus.active").ToArray();Check(hits.Length==2&&hits.All(e=>e.Amount==PrototypeDamageCalculator.RoundAmount(100*quake)),"Quake AP coefficient all enemies");
                Check(b.StatusStacks("shock")==1&&other.StatusStacks("shock")==1,"Shock on both survivors");
                Hold(a);Ticks(f,49);Check(b.StatusStacks("marcus.cc")==1,"Quake CC before1");f.Step();Check(b.StatusStacks("marcus.cc")==0&&b.StatusStacks("shock")==1,"Quake CC expires1 but shock persists");while(f.Combat.ElapsedSeconds<19.979)f.Step();Check(Casts(a,"web.marcus.active")==1,"CC remains blocking");
            }
            using(var f=new Fixture())
            {
                Equip(f.A,"marcus",star,"active");f.A.Stats.AttackSpeed=.01f;f.B.Stats.MaxHealth=100000;f.Start();Hold(f.B);Ticks(f,499);Check(Casts(f.A,"web.marcus.active")==0,"Uncontrolled before10");f.Step();Check(Casts(f.A,"web.marcus.active")==1,"Uncontrolled at10");Ticks(f,500);Check(Casts(f.A,"web.marcus.active")==2,"Quake repeats20");
            }
            using(var f=new Fixture())
            {
                Equip(f.A,"marcus",star,"passive");f.A.Stats.AttackSpeed=1;f.B.Stats.MaxHealth=10000;f.B.Stats.AttackSpeed=.01f;f.Start();f.B.ApplyStatus("shock",1,1,1,permanent:true);f.Step();
                Check(f.B.CurrentHealth==10000-100-PrototypeDamageCalculator.RoundAmount(100*shock)&&f.B.StatusStacks("shock")==0,"Shock consumed for AP damage");
                Check(f.B.IsControlled&&Casts(f.A,"web.marcus.passive")==1,"Shock CC.5 and once");Ticks(f,24);Check(f.B.IsControlled,"Shock CC before.5");f.Step();Check(!f.B.IsControlled,"Shock CC expires.5");
                Attacks(f,f.A,2);Check(Casts(f.A,"web.marcus.passive")==1,"Consumed shock cannot fire again");
                Check(f.Events.Count(e=>e.Type==PrototypeCombatEventType.StatusConsumed&&e.EffectKey=="shock"&&e.SkillId=="web.marcus.passive")==1,"Consumption attributed once");
            }
        }
        private static void Shop(BattlefieldPrototype board,string id)
        {
            var c=board.RunController;c.SetDataMode(PrototypeDataMode.WebRoster);c.Data.Rules.StartingCredits=10000;c.ResetRun();
            foreach(var owned in c.Run.Player.OwnedUnits.ToArray())c.Sell(owned.InstanceId);
            bool bought=false;
            for(int roll=0;roll<1500&&!bought;roll++)
            {for(int slot=0;slot<c.Run.Shop.Count;slot++)if(c.Run.Shop.Slot(slot)==id){bought=c.Buy(slot);break;}if(!bought)c.Reroll();}
            Check(bought,"Real shop purchase "+id);var ownedUnit=c.Run.Player.OwnedUnits.Single();Check(ownedUnit.Location==PrototypeUnitLocation.Bench,"Purchased real unit enters bench");
            Check(c.Move(ownedUnit.InstanceId,PrototypeUnitLocation.Board,1,1)&&c.Ready(),"Deploy and Ready "+id);
            foreach(var unit in board.Units){unit.Stats.MaxHealth=100000;unit.SetHealth(100000);}
            for(int tick=0;tick<850;tick++)
            {c.Step(.02f);}
            var a=board.Units.Single(u=>u.Faction=="A");Check(Casts(a,"web."+id+".active")>0&&Casts(a,"web."+id+".passive")>0,"Actual purchased character uses both skills "+id);
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
                    {string id=Ids[(row+team+seed)%4];var unit=f.Add(team==0?"A":"B",row,team==0?1:4,data.CombatStats(id,1+seed%3));unit.ConfigureAbilities(data.Definition(id).Abilities,1+seed%3);}
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
        private static void Check(bool value,string message){assertions++;if(!value)throw new Exception("Web second batch smoke: "+message);}
    }
}
