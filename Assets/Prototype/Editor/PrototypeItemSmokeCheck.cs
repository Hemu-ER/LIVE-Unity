using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace LIVE.Prototype.Editor
{
    public static class PrototypeItemSmokeCheck
    {
        private static int assertions;
        private static void Check(bool condition,string message){assertions++;if(!condition)throw new Exception("Item smoke: "+message);}
        private static PrototypeRunModel NewRun()
        {var data=PrototypeGameData.Load();data.Rules.StartingCredits=100000;return new PrototypeRunModel(data,1729);}
        private static long Grant(PrototypeRunModel run,string id)
        {Check(run.AcquireItem(id)==PrototypeItemAcquisition.Stored,"Acquire item");return run.Items.Items.Last().InstanceId;}
        private static string Snapshot(PrototypeRunModel r)=>r.Revision+"|"+r.Player.Credits+"|"+string.Join(";",r.Items.Items.Select(i=>$"{i.InstanceId}:{i.DefinitionId}:{i.UnitId}:{i.Slot}"))+"|"+string.Join(";",r.Player.OwnedUnits.Select(u=>$"{u.InstanceId}:{u.DefinitionId}:{u.Stars}:{u.Location}"))+"|"+string.Join(",",Enumerable.Range(0,5).Select(i=>r.Shop.Slot(i)));
        public static void Validate(BattlefieldPrototype board)
        {
            assertions=0;var catalog=PrototypeItemCatalog.Load();
            var roundtrip=JsonUtility.FromJson<PrototypeItemCatalog>(JsonUtility.ToJson(catalog));roundtrip.Validate();
            Check(catalog.Items.Length==35&&catalog.Recipes.Length==28,"Catalog counts and serialization");
            string[] names={"오토-암즈","블레이드 부츠","스펙터","아오자이","미스릴 퀴버","레이더","서슬가시 체인","임세티","레버넌트","요명월","텔루리안 타임피스","천룡잠","용의 비늘","미스릴 크롭","배틀 슈트","팬텀 자켓","타이탄 아머","유령 신부의 드레스","가디언 슈트","길리 슈트","화령장","슈팅스타의 자켓","살라딘의 화살통","레가투스","블래스터 헬멧","운명의 주사위","프시케의 칼날","아이언 메이든"};
            var bases=catalog.Items.Where(i=>!i.Completed).ToArray();int recipe=0;
            for(int i=0;i<7;i++)for(int j=i;j<7;j++)
            {
                string result=catalog.Combine(bases[i].Id,bases[j].Id);
                Check(catalog.Find(result).DisplayName==names[recipe++]&&catalog.Combine(bases[j].Id,bases[i].Id)==result,"All named unordered recipes");
                for(int reverse=0;reverse<2;reverse++)
                {
                    var r=NewRun();long a=Grant(r,bases[i].Id),b=Grant(r,bases[j].Id);
                    Check(!r.CombineItems(a,a),"Cannot reuse same instance twice");
                    Check(r.CombineItems(reverse==0?a:b,reverse==0?b:a)&&r.Items.Items.Count==1&&r.Items.Items[0].DefinitionId==result,"Recipe consumes two distinct instances once");
                    long extra=Grant(r,bases[0].Id);string before=Snapshot(r);Check(!r.CombineItems(r.Items.Items[0].InstanceId,extra)&&Snapshot(r)==before,"Completed item cannot combine, no mutation");
                }
            }
            for(int k=0;k<7;k++)
            {
                var r=NewRun();long u=r.Player.OwnedUnits[0].InstanceId;var before=r.DerivedStats(u);long item=Grant(r,bases[k].Id);
                Check(r.EquipItem(item,u),"Equip base");var after=r.DerivedStats(u);
                Check(after.AttackPower==before.AttackPower+(k==0?10:0)&&Math.Abs(after.SkillAmplification-before.SkillAmplification-(k==1?15:0))<.001&&
                    after.MaxHealth==before.MaxHealth+(k==2?100:0)&&after.Defense==before.Defense+(k==3?10:0)&&
                    Math.Abs(after.AttackSpeed-before.AttackSpeed*(k==4?1.1:1))<.001&&Math.Abs(after.CriticalChance-Math.Min(1,before.CriticalChance+(k==5?.1:0)))<.001&&
                    Math.Abs(after.DefensePenetration-before.DefensePenetration-(k==6?8:0))<.001,"Exact seven base modifiers");
                Check(r.UnequipItem(item)&&JsonUtility.ToJson(r.DerivedStats(u))==JsonUtility.ToJson(before),"Unequip restores fresh base stats");
            }
            Ownership(catalog);AtomicMultiReturn();MergeSafety();ActualFlow(board);Ui(board);
            Debug.Log($"ITEM_SMOKE_CHECK_PASSED: {assertions} assertions; 7 bases, 28 unordered recipes, serialization, overflow, equipment/transfer/autocombine, derived stats, atomic sales/merges, phase lock, persistence, real combat and uGUI clicks.");
        }
        private static void Ownership(PrototypeItemCatalog catalog)
        {
            var r=NewRun();long u=r.Player.OwnedUnits[0].InstanceId;string complete=catalog.Recipes[0].Result;
            var baseStats=r.DerivedStats(u);
            for(int i=0;i<3;i++)Check(r.EquipItem(Grant(r,complete),u),"Duplicate completed equipment");
            long excess=Grant(r,complete);string before=Snapshot(r);Check(!r.EquipItem(excess,u)&&Snapshot(r)==before,"Three equipment slots maximum");
            Check(r.DerivedStats(u).AttackPower==baseStats.AttackPower+60,"Duplicate stats stack independently");
            Check(r.SellItem(r.Items.AtEquipment(u,2).InstanceId),"Sell equipped item");
            long a=Grant(r,"sheath");Check(r.EquipItem(a,u,2),"Base in third slot");long b=Grant(r,"gold_bracelet");
            Check(r.EquipItem(b,u,2)&&r.Items.AtEquipment(u,2).DefinitionId==catalog.Combine("sheath","gold_bracelet")&&r.Items.Find(b)==null,"Drop combine replaces same full equipment slot");
            r=NewRun();u=r.Player.OwnedUnits[0].InstanceId;a=Grant(r,"sheath");b=Grant(r,"sheath");Check(r.EquipItem(a,u)&&r.EquipItem(b,u)&&r.Items.Equipped(u).Count()==1,"Second base auto combines even with free slots");
            for(int i=0;i<12;i++)Grant(r,"wire");
            var ids=r.Items.Items.Select(x=>x.InstanceId).ToArray();int credits=r.Player.Credits;
            Check(r.AcquireItem("wire")==PrototypeItemAcquisition.AutoSold&&r.Player.Credits==credits+2,"Full bag auto sells only new base");
            Check(r.AcquireItem(complete)==PrototypeItemAcquisition.AutoSold&&r.Player.Credits==credits+7&&r.Items.Items.Select(x=>x.InstanceId).SequenceEqual(ids),"Full bag new completed +5, existing items intact");
            before=Snapshot(r);Check(!r.Sell(u)&&Snapshot(r)==before,"Full bag unit sale atomic rejection");Check(!r.UnequipItem(a)&&Snapshot(r)==before,"Full bag unequip atomic rejection");
            Check(r.SellItem(r.Items.AtInventory(0).InstanceId)&&r.Sell(u)&&r.Items.Find(a).UnitId==0&&r.Items.FreeSlots==0,"Sale returns equipment without auto-selling");
            r=NewRun();u=r.Player.OwnedUnits[0].InstanceId;a=Grant(r,"wire");b=Grant(r,"sheath");long c=Grant(r,"gold_bracelet");Check(r.EquipItem(a,u),"Persistence equipment");
            r.Ready();before=Snapshot(r);
            Check(!r.EquipItem(b,u)&&!r.UnequipItem(a)&&!r.CombineItems(b,c)&&!r.SellItem(a)&&!r.Sell(u)&&Snapshot(r)==before,"Combat rejects every item action and unit sale atomically");
            r.CompleteCombat(PrototypeRoundOutcome.Loss);Check(!r.SellItem(a),"Result also locked");r.Tick(100);r.Tick(100);
            Check(r.Phase==PrototypeRunPhase.Prep&&r.Items.Find(a).UnitId==u,"Round transition preserves equipment");
            r.Reset(1729);Check(r.Items.Items.Count==0,"New run clears items");
        }
        private static void AtomicMultiReturn()
        {
            var r=NewRun();long u=r.Player.OwnedUnits[0].InstanceId;string completed=r.ItemCatalog.Recipes[0].Result;
            for(int n=0;n<3;n++)Check(r.EquipItem(Grant(r,completed),u),"Three return items");
            for(int n=0;n<10;n++)Grant(r,"wire");string before=Snapshot(r);string def=r.Player.Find(u).DefinitionId;int pool=r.Pool.Available(def);
            Check(!r.Sell(u)&&Snapshot(r)==before&&r.Pool.Available(def)==pool&&r.Items.Equipped(u).Count()==3,"Two free slots cannot partially return three items or mutate pool");
            Check(r.SellItem(r.Items.AtInventory(0).InstanceId)&&r.Sell(u)&&r.Items.FreeSlots==0&&r.Items.Items.Count==12,"Exact capacity returns all three items");
            r=NewRun();u=r.Player.OwnedUnits[0].InstanceId;long a=Grant(r,"sheath");Check(r.EquipItem(a,u),"Transfer source equipped");
            Check(r.Buy(0),"Buy recipient from real shop");var recipient=r.Player.OwnedUnits.First(x=>x.InstanceId!=u);
            Check(r.EquipItem(a,recipient.InstanceId)&&r.Items.Equipped(u).Count()==0&&r.Items.Find(a).UnitId==recipient.InstanceId,"Transfer without inventory space needed");
            before=Snapshot(r);Check(!r.EquipItem(a,long.MaxValue)&&!r.EquipItem(a,recipient.InstanceId,3)&&Snapshot(r)==before,"Invalid owner/slot rejected atomically");
            Check(r.Move(recipient.InstanceId,PrototypeUnitLocation.Board,0,0)&&r.Items.Find(a).UnitId==recipient.InstanceId,"Bench to board preserves equipment");
            r.Ready();before=Snapshot(r);Check(!r.EquipItem(a,u)&&Snapshot(r)==before,"Combat blocks equipped transfer too");
        }
        private static int FindShop(PrototypeRunModel r,string id)
        {
            for(int n=0;n<2000;n++){for(int slot=0;slot<5;slot++)if(r.Shop.Slot(slot)==id)return slot;Check(r.Reroll(),"Roll for merge copy");}
            throw new Exception("Missing shop copy");
        }
        private static void MergeSafety()
        {
            var r=NewRun();var survivor=r.Player.OwnedUnits[0];string id=survivor.DefinitionId;
            Check(r.Buy(FindShop(r,id)),"Second copy purchased");var donor=r.Player.OwnedUnits.Single(u=>u.InstanceId!=survivor.InstanceId);
            long a=Grant(r,"wire"),b=Grant(r,"sheath");Check(r.EquipItem(a,survivor.InstanceId)&&r.EquipItem(b,donor.InstanceId),"Equip merge copies");
            Check(r.EquipItem(a,donor.InstanceId)&&r.Items.Equipped(donor.InstanceId).Count()==1&&r.Items.Find(a)==null,"Transfer auto combines two units' bases");
            for(int i=0;i<12;i++)Grant(r,"wire");int slot=FindShop(r,id);string before=Snapshot(r);int pool=r.Pool.Available(id);
            Check(!r.Buy(slot)&&Snapshot(r)==before&&r.Shop.Slot(slot)==id&&r.Pool.Available(id)==pool,"Merge purchase preflight rejects full bag without pool/shop/credit mutation");
            Check(r.SellItem(r.Items.AtInventory(0).InstanceId)&&r.Buy(slot),"Merge succeeds when return space available");
            Check(survivor.Stars==2&&r.Player.Find(donor.InstanceId)==null&&r.Items.Find(b).UnitId==0&&r.Items.FreeSlots==0,"Merge returns absorbed unit equipment");
        }
        private static void ActualFlow(BattlefieldPrototype board)
        {
            var c=board.RunController;c.SetDataMode(PrototypeDataMode.WebRoster);c.ResetRun();var owned=c.Run.Player.OwnedUnits[0];
            var basis=c.Run.DerivedStats(owned.InstanceId);c.AcquireItem("bulletproof_vest");long item=c.Run.Items.Items.Single().InstanceId;
            Check(c.EquipItem(item,owned.InstanceId)&&board.Units.Single(u=>u.Faction=="A").MaxHealth==basis.MaxHealth+100,"Prep derived stats reach real combat instance");
            c.Ready();Check(board.Units.Single(u=>u.Faction=="A").MaxHealth==basis.MaxHealth+100,"Ready snapshot retains modifiers");
            board.Units.Single(u=>u.Faction=="A").SetHealth(0);c.Step(.02f);
            Check(c.Run.Items.Find(item).UnitId==owned.InstanceId,"Combat death retains owned item");
            for(int n=0;n<2000&&c.Run.Phase!=PrototypeRunPhase.Prep;n++)c.Step(.02f);
            Check(c.Run.Phase==PrototypeRunPhase.Prep&&board.Units.Single(u=>u.Faction=="A").MaxHealth==basis.MaxHealth+100,"Next round rebuild uses persistent equipment");
            string expected=null;
            for(int repeat=0;repeat<2;repeat++)
            {
                c.ResetRun();owned=c.Run.Player.OwnedUnits[0];c.AcquireItem("quiver");c.EquipItem(c.Run.Items.Items.Single().InstanceId,owned.InstanceId);c.Ready();int ticks=0;
                while(c.Run.Phase==PrototypeRunPhase.Combat&&ticks++<3100)c.Step(.02f);
                string result=c.Run.Phase+":"+c.Run.History.Last().Outcome+":"+ticks+":"+board.Combat.StatisticsSummary();
                Check(c.Run.Phase==PrototypeRunPhase.Result&&(expected==null||expected==result),"Equipped deterministic combat terminates and repeats");expected=result;
            }
        }
        private static void Click(Button b)=>ExecuteEvents.Execute(b.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
        private static void Ui(BattlefieldPrototype board)
        {
            var c=board.RunController;var ui=board.GetComponent<PrototypeRunHud>();c.ResetRun();ui.Refresh();
            Check(ui.ItemButtons.Length==12&&ui.EquipmentButtons.Length==3,"UI slot counts");
            Click(ui.ItemGrantButton);Click(ui.ItemGrantButton);Click(ui.ItemButtons[0]);Click(ui.ItemButtons[1]);Click(ui.ItemCombineButton);
            Check(c.Run.Items.Items.Count==1&&c.Run.ItemCatalog.Find(c.Run.Items.Items[0].DefinitionId).Completed,"uGUI two-base combine");
            Click(ui.BoardButtons[4]);Click(ui.ItemEquipButton);Check(c.Run.Items.Equipped(ui.SelectedId).Count()==1,"uGUI equipment");
            Click(ui.ItemReturnButton);Check(c.Run.Items.Items.Single().UnitId==0,"uGUI unequip");
            Click(ui.ItemSellButton);Check(c.Run.Items.Items.Count==0,"uGUI sale");
            foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1600,900),new Vector2Int(1280,720)})
            {
                ui.RefreshForSize(size.x,size.y);foreach(var zone in new[]{PrototypeRunHud.ItemArea,PrototypeRunHud.EquipmentArea})
                foreach(var old in new[]{PrototypeRunHud.BoardArea,PrototypeRunHud.HudArea,PrototypeRunHud.BenchArea,PrototypeRunHud.ShopArea,PrototypeRunHud.ActionsArea})Check(!zone.Overlaps(old),"Item panels preserve existing layout");
            }
            Click(ui.ReadyButton);Check(ui.ItemButtons.All(b=>!b.interactable)&&ui.EquipmentButtons.All(b=>!b.interactable)&&!ui.ItemGrantButton.interactable,"Combat UI locks items");c.SetDataMode(PrototypeDataMode.WebRoster);ui.Refresh();
        }
    }
}
