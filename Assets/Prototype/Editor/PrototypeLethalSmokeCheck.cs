using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace LIVE.Prototype.Editor
{
    public static class PrototypeLethalSmokeCheck
    {
        private static int assertions, timeoutDraws;
        private static readonly string[] Ids={"jenny","ian"};
        public static void Validate(BattlefieldPrototype board)
        {
            assertions=timeoutDraws=0;
            Check(PrototypeCharacterSkills.Load().Definitions.Count(e=>e.Implemented)==26,"Exactly twelve executable pairs");
            for(int star=1;star<=3;star++){Persona(star);Survival("jenny",star);Survival("ian",star);}
            MovingReplacement();ReplacementPriority();ReservationSurvivesReplacement();
            foreach(var id in Ids)Shop(board,id);
            Simulations();
            Debug.Log($"LETHAL_SMOKE_CHECK_PASSED: {assertions} assertions; Jenny/Ian 1-3 stars, reservation/consumption/reset, lethal replacement ordering, permanent stages, invulnerability/lifesteal, no death/release/retarget/early finish, movement interruption, replacement priority, real shop and 24 deterministic simulations ({timeoutDraws} timeout draws).");
        }
        private static void Equip(PrototypeUnit unit,string id,int star,string kind=null)
        {
            var settings=JsonUtility.FromJson<PrototypeAbilitySettings>(JsonUtility.ToJson(PrototypeWebRoster.Load().CreateGameData().Definition(id).Abilities));
            if(kind!=null)settings.Skills=settings.Skills.Where(s=>s.Id.EndsWith("."+kind)).ToArray();
            unit.ConfigureAbilities(settings,star);unit.Stats.MaxHealth=10000;unit.Stats.Defense=0;unit.Stats.AttackPower=100;unit.Stats.SkillAmplification=100;unit.Stats.AttackRange=6;unit.Stats.AttackSpeed=1;
        }
        private static void Hold(PrototypeUnit u)=>u.ApplyStatus("test.hold",1,1,100,PrototypeStatusKind.CrowdControl);
        private static void Release(PrototypeUnit u)=>u.ApplyStatus("test.hold",-1,1,1);
        private static void Ticks(Fixture f,int n){for(int i=0;i<n;i++)f.Step();}
        private static PrototypeSkillRuntime Skill(PrototypeUnit u,string id)=>u.Skills.Single(s=>s.Definition.Id==id);
        private static int Casts(PrototypeUnit u,string id)=>Skill(u,id).CastCount;
        private static void Attacks(Fixture f,PrototypeUnit u,int n)
        {int ticks=0;while(u.Statistics.BasicAttackCount<n&&ticks++<2000)f.Step();Check(u.Statistics.BasicAttackCount==n,"Attack cadence continues");}
        private static void Persona(int star)
        {
            using(var f=new Fixture())
            {
                Equip(f.A,"jenny",star,"active");f.B.Stats.MaxHealth=100000;f.B.Stats.Defense=100;f.Start();Hold(f.B);
                var skill=Skill(f.A,"web.jenny.active");Attacks(f,f.A,1);Check(!skill.NextBasicReserved&&skill.CastCount==0,"First hit no Persona");
                Attacks(f,f.A,2);Check(skill.NextBasicReserved&&skill.CastCount==0&&f.B.CurrentHealth==99900,"Second hit only reserves, no extra damage");
                Hold(f.A);Ticks(f,100);Check(skill.NextBasicReserved&&skill.CastCount==0,"Reservation waits through CC/time");Release(f.A);
                Attacks(f,f.A,3);int extra=PrototypeDamageCalculator.RoundAmount(100*new[]{1.5,2,3}[star-1]/2);
                Check(!skill.NextBasicReserved&&skill.CastCount==1&&f.B.CurrentHealth==100000-150-extra,"Third consumes exactly once with AMP star ratio");
                Attacks(f,f.A,4);Check(skill.NextBasicReserved&&skill.CastCount==1,"Fourth reserves again");Attacks(f,f.A,5);Check(!skill.NextBasicReserved&&skill.CastCount==2,"Fifth consumes second reservation");
                Check(f.Events.Count(e=>e.Type==PrototypeCombatEventType.NextAttackReserved)==2&&f.Events.Count(e=>e.Type==PrototypeCombatEventType.NextAttackConsumed)==2,"Reservation/consumption events match");
                Attacks(f,f.A,6);Check(skill.NextBasicReserved,"Sixth pending");f.Combat.ResetBattle();Check(!Skill(f.A,"web.jenny.active").NextBasicReserved,"Reset clears reservation");
            }
            using(var f=new Fixture(false))
            {
                var a=f.Add("A",1,1);var first=f.Add("B",1,4);var next=f.Add("B",0,5);Equip(a,"jenny",star,"active");first.Stats.MaxHealth=10000;next.Stats.MaxHealth=10000;
                f.Start();Hold(first);Hold(next);Attacks(f,a,2);first.SetHealth(1);Attacks(f,a,3);
                Check(!first.IsAlive&&next.CurrentHealth==next.MaxHealth&&!Skill(a,"web.jenny.active").NextBasicReserved,"Consumed on killing hit, never transferred to new target");
            }
        }
        private static void Survival(string id,int star)
        {
            using(var f=new Fixture())
            {
                Equip(f.A,id,star);f.B.Stats.MaxHealth=100000;f.Start();Hold(f.A);Hold(f.B);f.Step();
                string lethalId="web."+id+(id=="jenny"?".passive":".active");var cell=f.A.Cell;var locked=f.B.Target;
                if(id=="ian")Check(Math.Abs(f.A.CombatAttackPower-80)<.001&&f.A.StatusStacks("ian.form")==1,"Ian bound AP.8 state");
                f.A.ReceiveDamage(100,f.B);Check(Skill(f.A,lethalId).CastCount==0&&f.A.CurrentHealth==9900,"Nonlethal never replaces");
                int hp=PrototypeDamageCalculator.RoundAmount(10000*(id=="jenny"?new[]{.2,.3,.5}[star-1]:new[]{.4,.5,.7}[star-1]));
                int eventStart=f.Events.Count;
                f.A.ReceiveDamage(999999,f.B);
                Check(f.A.IsAlive&&f.A.CurrentHealth==hp&&Skill(f.A,lethalId).CastCount==1,"First lethal exact revive HP");
                Check(f.A.Cell==cell&&f.Combat.Grid.Occupant(cell)==f.A&&f.B.Target==locked&&locked==f.A,"Lethal preserves cell and locked enemy target");
                Check(f.Combat.State==PrototypeCombatState.Fighting&&f.B.Statistics.Kills==0&&!f.Events.Any(e=>e.Type==PrototypeCombatEventType.UnitDied),"No death/kill/early finish");
                var events=f.Events.Skip(eventStart).ToList();int cast=events.FindIndex(e=>e.Type==PrototypeCombatEventType.SkillCast);int heal=events.FindIndex(e=>e.Type==PrototypeCombatEventType.HealApplied);int replaced=events.FindIndex(e=>e.Type==PrototypeCombatEventType.LethalDamageReplaced);int damage=events.FindIndex(e=>e.Type==PrototypeCombatEventType.DamageDealt);
                Check(cast>=0&&heal>cast&&replaced>heal&&damage>replaced,"Replacement transaction precedes final damage notification");
                Check(events.FindLastIndex(e=>e.Type==PrototypeCombatEventType.BuffApplied)<replaced,"Follow-up buffs applied before replacement completion");
                double speed=1+(id=="jenny"?new[]{.3,.5,1}[star-1]:new[]{.5,.75,1.5}[star-1]);
                Check(Math.Abs(f.A.EffectiveStats.AttackSpeed-speed)<.001,"Revive AS star coefficient");
                if(id=="jenny")
                {
                    Check(f.A.HasStatus(PrototypeStatusKind.Invulnerable),"Jenny revive invulnerable");f.A.ReceiveDamage(999999,f.B);Check(f.A.CurrentHealth==hp&&Skill(f.A,lethalId).CastCount==1,"Invulnerability blocks second damage entirely");
                    Ticks(f,74);Check(f.A.HasStatus(PrototypeStatusKind.Invulnerable),"Jenny before1.5s");f.Step();Check(!f.A.HasStatus(PrototypeStatusKind.Invulnerable),"Jenny exactly1.5s expiry");
                }
                else
                {
                    Check(Math.Abs(f.A.CombatAttackPower-120)<.001&&f.A.StatusStacks("ian.form")==2,"Ian replaces.8 with1.2, not.96");
                    Check(!f.A.HasStatus(PrototypeStatusKind.Invulnerable),"Ian has no invented invulnerability");
                    f.A.ReceiveDamage(100,f.B);Check(f.A.CurrentHealth==hp-100,"Ian takes damage after liberation");
                }
                Ticks(f,300);Check(Math.Abs(f.A.EffectiveStats.AttackSpeed-speed)<.001,"Post-revive AS permanent");
                Release(f.A);int before=f.A.CurrentHealth;Attacks(f,f.A,1);
                if(id=="ian")
                {
                    int healed=PrototypeDamageCalculator.RoundAmount(120*new[]{.2,.3,.5}[star-1]);Check(f.A.CurrentHealth==before+healed,"Ian actual basic damage lifesteal");
                    before=f.A.CurrentHealth;f.B.ReceiveDamage(100,f.A,skillId:"test.skill");Check(f.A.CurrentHealth==before,"Ian skill damage has no lifesteal");
                }
                f.A.ReceiveDamage(999999,f.B);Check(!f.A.IsAlive&&Skill(f.A,lethalId).CastCount==1,"Second lethal normal death");
                Check(f.Combat.Grid.IsFree(cell)&&f.B.Statistics.Kills==1&&f.Combat.State==PrototypeCombatState.Finished&&f.Events.Count(e=>e.Type==PrototypeCombatEventType.UnitDied)==1,"Only final death releases/ends/counts kill");
                f.Combat.ResetBattle();f.Combat.StartBattle();Check(f.A.IsAlive&&Skill(f.A,lethalId).CastCount==0,"Reset restores replacement charge");
                if(id=="ian")Check(Math.Abs(f.A.CombatAttackPower-80)<.001&&f.A.StatusStacks("ian.form")==1,"Reset returns bound state");
            }
        }
        private static void ReservationSurvivesReplacement()
        {
            using(var f=new Fixture())
            {
                Equip(f.A,"jenny",1);f.B.Stats.MaxHealth=100000;f.Start();Hold(f.B);Attacks(f,f.A,2);
                f.A.ReceiveDamage(999999,f.B);Check(Skill(f.A,"web.jenny.active").NextBasicReserved,"Revive preserves reserved next hit");Attacks(f,f.A,3);
                Check(Casts(f.A,"web.jenny.active")==1&&!Skill(f.A,"web.jenny.active").NextBasicReserved,"Reserved hit resolves after replacement");
            }
            using(var f=new Fixture())
            {
                Equip(f.A,"jenny",1);f.A.Stats.MaxHealth=1;f.Start();int reentries=0;
                f.Combat.EventRaised += e=>
                {
                    if(e.Type==PrototypeCombatEventType.SkillCast&&e.SkillId=="web.jenny.passive")
                    {reentries++;f.A.ReceiveDamage(999999,f.B);}
                };
                f.A.ReceiveDamage(999999,f.B);
                Check(reentries==1&&f.A.IsAlive&&f.A.CurrentHealth==1&&Casts(f.A,"web.jenny.passive")==1,"Replacement atomic against observer reentry; tiny HP floor");
                Check(f.Combat.Grid.Occupant(f.A.Cell)==f.A&&f.Combat.State==PrototypeCombatState.Fighting,"Tiny HP replacement never releases/finishes");
            }
        }
        private static void MovingReplacement()
        {
            foreach(var id in Ids)using(var f=new Fixture())
            {
                Equip(f.A,id,1);f.A.Stats.AttackRange=1;f.Start();Hold(f.B);f.Step();Check(f.A.IsMoving,"Moving setup");var cell=f.A.Cell;f.A.ReceiveDamage(999999,f.B);
                Check(f.A.IsAlive&&!f.A.IsMoving&&f.Combat.Grid.Occupant(cell)==f.A,"Interrupt motion without releasing occupied cell");
                int blocked=0;for(int r=0;r<3;r++)for(int c=0;c<6;c++)if(!f.Combat.Grid.IsFree(new Vector2Int(r,c)))blocked++;
                Check(blocked==2,"Only movement reservation removed");
                Ticks(f,10);Check(f.Combat.State==PrototypeCombatState.Fighting&&f.A.IsAlive,"Combat continues after moving replacement");
            }
        }
        private static void ReplacementPriority()
        {
            using(var f=new Fixture())
            {
                var first=new PrototypeSkillDefinition {Id="first",Trigger=PrototypeSkillTrigger.OnLethalDamage,Target=PrototypeSkillTarget.Self,Execution=PrototypeSkillExecution.Instant,OncePerCombat=true,CanRunWhileControlled=true,Effects=new[]{new PrototypeSkillEffect {Type=PrototypeSkillEffectType.Heal,BaseDamage=100}}};
                var second=JsonUtility.FromJson<PrototypeSkillDefinition>(JsonUtility.ToJson(first));second.Id="second";
                f.A.ConfigureAbilities(new PrototypeAbilitySettings {Skills=new[]{first,second}});f.Start();
                f.A.ReceiveDamage(999999,f.B);Check(f.A.CurrentHealth==100&&Skill(f.A,"first").CastCount==1&&Skill(f.A,"second").CastCount==0,"Only first successful replacement resolves");
                f.A.ReceiveDamage(999999,f.B);Check(f.A.IsAlive&&Skill(f.A,"second").CastCount==1,"Independent remaining replacement available next hit");
            }
        }
        private static void Shop(BattlefieldPrototype board,string id)
        {
            var c=board.RunController;c.SetDataMode(PrototypeDataMode.WebRoster);c.Data.Rules.StartingCredits=10000;c.ResetRun();
            foreach(var owned in c.Run.Player.OwnedUnits.ToArray())c.Sell(owned.InstanceId);
            int upgrades=0;
            while(c.Data.Odds(c.Run.Player.Mastery.Level)[c.Data.Definition(id).Cost-1]==0&&upgrades++<200)
                Check(c.Invest(),"Use real mastery investment to unlock character cost");
            bool bought=false;
            for(int roll=0;roll<1500&&!bought;roll++)
            {for(int slot=0;slot<c.Run.Shop.Count;slot++)if(c.Run.Shop.Slot(slot)==id){bought=c.Buy(slot);break;}if(!bought)c.Reroll();}
            Check(bought,"Real shop purchase "+id);var ownedUnit=c.Run.Player.OwnedUnits.Single();Check(ownedUnit.Location==PrototypeUnitLocation.Bench,"Purchased real unit enters bench");
            Check(c.Move(ownedUnit.InstanceId,PrototypeUnitLocation.Board,1,1)&&c.Ready(),"Deploy and Ready "+id);
            foreach(var unit in board.Units){unit.Stats.MaxHealth=100000;unit.SetHealth(100000);}
            for(int tick=0;tick<650;tick++)
            {c.Step(.02f);if(tick==80){var live=board.Units.Single(u=>u.Faction=="A");live.ReceiveDamage(int.MaxValue,null);}}
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
                    {string id=Ids[(row+team+seed)%2];var unit=f.Add(team==0?"A":"B",row,team==0?1:4,data.CombatStats(id,1+seed%3));unit.ConfigureAbilities(data.Definition(id).Abilities,1+seed%3);}
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
        private static void Check(bool value,string message){assertions++;if(!value)throw new Exception("Lethal smoke: "+message);}
    }
}
