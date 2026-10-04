using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace LIVE.Prototype.Editor
{
    public static class PrototypeLauraSmokeCheck
    {
        private static int assertions,timeoutDraws;
        public static void Validate(BattlefieldPrototype board)
        {
            assertions=timeoutDraws=0;
            for(int star=1;star<=3;star++){Rules(star);Lifecycle(star);Periodic(star);ShopMerge(board,"laura",star);}
            Simulations();
            Debug.Log($"LAURA_SMOKE_CHECK_PASSED: {assertions} assertions; 1-3 stars, reservation modifiers, alternate cadence, periodic/CC, movement/retarget/reset, real shop merges and24 deterministic simulations ({timeoutDraws} timeout draws).");
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
                Equip(f.A,"laura",star,"passive");f.B.Stats.MaxHealth=100000;f.Start();Hold(f.B);
                var passive=Skill(f.A,"web.laura.passive");
                Check(!passive.NextBasicReserved&&f.A.EffectiveStats.AttackSpeed==1,"No initial reservation/modifier");
                bool reserved=false,consumed=false,started=false,damage=false,eventStatsValid=true;float expectedBase=1;
                f.Combat.EventRaised+=e=>{
                    if(e.Type==PrototypeCombatEventType.NextAttackReserved){reserved=true;eventStatsValid &= f.A.EffectiveStats.AttackSpeed==expectedBase*2;}
                    if(e.Type==PrototypeCombatEventType.BasicAttackStarted&&f.A.Statistics.BasicAttackCount==2){started=true;eventStatsValid &= f.A.EffectiveStats.AttackSpeed==expectedBase*2;}
                    if(e.Type==PrototypeCombatEventType.NextAttackConsumed){consumed=true;eventStatsValid &= f.A.EffectiveStats.AttackSpeed==expectedBase;}
                    if(e.Type==PrototypeCombatEventType.DamageDealt&&e.SkillId=="web.laura.passive"){damage=true;eventStatsValid &= f.A.EffectiveStats.AttackSpeed==expectedBase;}
                };
                Attacks(f,f.A,1);double first=f.Combat.ElapsedSeconds;
                Check(reserved&&passive.NextBasicReserved&&passive.CastCount==0&&f.B.CurrentHealth==99900,"First basic reserves without extra damage");
                Attacks(f,f.A,2);double second=f.Combat.ElapsedSeconds;
                Check(consumed&&started&&damage&&!passive.NextBasicReserved&&passive.CastCount==1&&f.B.CurrentHealth==100000-200-PrototypeDamageCalculator.RoundAmount(200*new[]{1,1.5,2.5}[star-1]),"Second consumes exact AMP coefficient once");
                Attacks(f,f.A,3);double third=f.Combat.ElapsedSeconds;
                Check(passive.NextBasicReserved&&passive.CastCount==1,"Third re-arms, not second");
                Check(Math.Abs(second-first-1)<.021&&Math.Abs(third-second-.5)<.021,"Cooldown snapshots pre-attack AS like web");
                expectedBase=1.5f;
                f.A.ApplyBuff(new PrototypeSkillEffect{Type=PrototypeSkillEffectType.StatBuff,Stat=PrototypeBuffStat.AttackSpeed,Key="test.as",BuffAmount=.5f,Multiplicative=true,Permanent=true});
                Check(f.A.EffectiveStats.AttackSpeed==3,"Reservation multiplies other modifier");Attacks(f,f.A,4);Check(f.A.EffectiveStats.AttackSpeed==1.5f,"Consumption removes only reservation multiplier");
                Check(eventStatsValid,"AS event lifecycle at reserve/start/consume/damage, including unrelated buffs");
            }
        }
        private static void Lifecycle(int star)
        {
            using(var f=new Fixture())
            {
                Equip(f.A,"laura",star,"passive");var other=f.Add("B",0,5);f.B.Stats.MaxHealth=other.Stats.MaxHealth=100000;
                f.Start();Hold(f.B);Hold(other);Attacks(f,f.A,1);var passive=Skill(f.A,"web.laura.passive");
                f.A.Stats.AttackRange=1;int wait=0;while(!f.A.IsMoving&&wait++<100)f.Step();
                Check(f.A.IsMoving&&passive.NextBasicReserved&&f.A.EffectiveStats.AttackSpeed==2,"Movement retains reservation and AS");
                Hold(f.A);Ticks(f,75);Check(passive.NextBasicReserved&&passive.CastCount==0&&f.A.EffectiveStats.AttackSpeed==2,"CC cancels movement, not reservation");
                f.B.SetHealth(0);Check(passive.NextBasicReserved&&f.A.EffectiveStats.AttackSpeed==2,"External target death does not consume");
                f.A.Stats.AttackRange=6;Release(f.A);Attacks(f,f.A,2);Check(passive.CastCount==1&&!passive.NextBasicReserved&&f.A.EffectiveStats.AttackSpeed==1,"Retarget consumes on actual basic only");
                Attacks(f,f.A,3);other.SetHealth(1);Attacks(f,f.A,4);
                Check(f.Combat.State==PrototypeCombatState.Finished&&!passive.NextBasicReserved&&f.A.EffectiveStats.AttackSpeed==1,"Finish clears reservation modifier");
                f.Combat.ResetBattle();Check(f.A.EffectiveStats.AttackSpeed==1&&!Skill(f.A,"web.laura.passive").NextBasicReserved,"Reset clear");f.Combat.StartBattle();Check(f.A.EffectiveStats.AttackSpeed==1,"Next battle clean");
            }
            using(var f=new Fixture())
            {
                Equip(f.A,"laura",star,"passive");var other=f.Add("B",0,5);f.Start();Hold(f.B);Hold(other);
                Attacks(f,f.A,1);f.B.SetHealth(1);Attacks(f,f.A,2);
                var p=Skill(f.A,"web.laura.passive");Check(!f.B.IsAlive&&!p.NextBasicReserved&&p.CastCount==1&&f.A.EffectiveStats.AttackSpeed==1,"Unlike Justina, lethal basic consumes without transferring damage");
                Check(other.CurrentHealth==1000,"No retargeted bonus damage");Attacks(f,f.A,3);f.A.SetHealth(0);Check(!p.NextBasicReserved&&f.A.EffectiveStats.AttackSpeed==1,"Caster death removes modifier");
            }
        }
        private static void Periodic(int star)
        {
            using(var f=new Fixture())
            {
                Equip(f.A,"laura",star,"active");var other=f.Add("B",0,5);f.B.Stats.MaxHealth=other.Stats.MaxHealth=100000;
                f.Start();Hold(f.A);Hold(f.B);Hold(other);var active=Skill(f.A,"web.laura.active");
                Ticks(f,499);Check(active.CastCount==0,"No active before10 seconds");f.Step();
                int amount=PrototypeDamageCalculator.RoundAmount(200*new[]{1.5,2,3.5}[star-1]);
                Check(active.CastCount==1&&f.B.CurrentHealth==100000-amount&&other.CurrentHealth==100000-amount,"10 second AoE AMP all stars during caster CC");
                Check(f.B.StatusStacks("laura.twilight.cc")==1&&other.StatusStacks("laura.twilight.cc")==1,"All-enemy CC");Ticks(f,49);Check(f.B.StatusStacks("laura.twilight.cc")==1,"CC before1 sec");f.Step();Check(f.B.StatusStacks("laura.twilight.cc")==0,"CC expires exactly1 sec");
                Ticks(f,449);Check(active.CastCount==1,"No early second periodic");f.Step();Check(active.CastCount==2,"20 second next periodic");
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
            Check(Math.Abs(active.Definition.Effects[0].SkillRatio-new[]{1.5,2,3.5}[star-1])<.0001,"Merged active coefficient");
            Check(Math.Abs(passive.Definition.Effects[0].SkillRatio-new[]{1,1.5,2.5}[star-1])<.0001,"Merged passive coefficient");
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
                    {string id=(row==0&&team==0?"laura":new[]{"hyunwoo","yuki","justina","jenny"}[(row+team+seed)%4]);var unit=f.Add(team==0?"A":"B",row,team==0?1:4,data.CombatStats(id,1+seed%3));unit.ConfigureAbilities(data.Definition(id).Abilities,1+seed%3);}
                    f.Combat.Initialize(3,6,f.Units,BattlefieldPrototype.Position,seed);f.Combat.StartBattle();int ticks=0;
                    while(f.Combat.State==PrototypeCombatState.Fighting&&ticks<3000)
                    {ticks++;f.Step();var cells=new HashSet<Vector2Int>();foreach(var unit in f.Units)if(unit.IsAlive)Check(cells.Add(unit.Cell)&&!double.IsNaN(unit.CombatAttackPower)&&!double.IsInfinity(unit.CombatAttackPower)&&!float.IsNaN(unit.EffectiveStats.AttackSpeed)&&!float.IsInfinity(unit.EffectiveStats.AttackSpeed)&&unit.EffectiveStats.AttackSpeed>0,"Mixed team occupancy/finite stats");}
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
