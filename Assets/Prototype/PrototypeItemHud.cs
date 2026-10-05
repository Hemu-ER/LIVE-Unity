using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace LIVE.Prototype
{
    public sealed partial class PrototypeRunHud
    {
        public static readonly Rect ItemArea=new Rect(1630,104,266,610);
        public static readonly Rect EquipmentArea=new Rect(24,534,264,180);
        private readonly Button[] inventoryButtons=new Button[12],equipmentButtons=new Button[3];
        private Button itemCombine,itemSell,itemEquip,itemReturn,itemGrant,itemCycle;
        private Text itemInfo,itemNotice,itemHeading;
        private long firstItem,secondItem;
        private int grantIndex;
        private PrototypeRunModel itemRun;
        public Button[] ItemButtons=>inventoryButtons;
        public Button[] EquipmentButtons=>equipmentButtons;
        public Button ItemCombineButton=>itemCombine;
        public Button ItemEquipButton=>itemEquip;
        public Button ItemSellButton=>itemSell;
        public Button ItemReturnButton=>itemReturn;
        public Button ItemGrantButton=>itemGrant;
        public string ItemInfoText=>itemInfo.text;
        private void BuildItems()
        {
            Box("Items panel",ItemArea,Panel);itemHeading=Label("Items heading",new Rect(1640,108,246,28),18);
            for(int i=0;i<12;i++)
            {int slot=i;inventoryButtons[i]=Button("Item inventory "+i,new Rect(1640+(i%2)*124,142+(i/2)*48,118,46),()=>SelectItem(controller.Run.Items.AtInventory(slot)?.InstanceId??0));inventoryButtons[i].GetComponentInChildren<Text>().fontSize=14;}
            itemInfo=Label("Item description",new Rect(1640,434,246,86),15);
            itemEquip=Button("Equip item",new Rect(1640,524,118,36),()=>controller.EquipItem(firstItem,selectedId));Caption(itemEquip,"Equip / Transfer");
            itemReturn=Button("Unequip item",new Rect(1764,524,118,36),()=>controller.UnequipItem(firstItem));Caption(itemReturn,"Return to bag");
            itemCombine=Button("Combine items",new Rect(1640,566,118,36),()=>{if(controller.CombineItems(firstItem,secondItem))secondItem=0;});Caption(itemCombine,"Combine 2");
            itemSell=Button("Sell item",new Rect(1764,566,118,36),()=>{if(controller.SellItem(firstItem))firstItem=secondItem=0;});
            itemNotice=Label("Item feedback",new Rect(1640,608,246,42),14);
            itemCycle=Button("Cycle test item",new Rect(1640,662,44,38),()=>grantIndex=(grantIndex+1)%7);Caption(itemCycle,">");
            itemGrant=Button("Grant test item",new Rect(1690,662,192,38),()=>controller.AcquireItem(controller.Run.ItemCatalog.Items.Where(i=>!i.Completed).ElementAt(grantIndex).Id));
            foreach(var b in new[]{itemEquip,itemReturn,itemCombine,itemSell,itemGrant})b.GetComponentInChildren<Text>().fontSize=14;
            Box("Equipment panel",EquipmentArea,Panel);Label("Equipment heading",new Rect(34,538,244,24),17).text="EQUIPMENT · 3 slots";
            for(int i=0;i<3;i++)
            {int slot=i;equipmentButtons[i]=Button("Equipment "+i,new Rect(34,568+i*46,244,40),()=>{
                var equipped=controller.Run.Items.AtEquipment(selectedId,slot);
                if(firstItem!=0&&firstItem!=equipped?.InstanceId)controller.EquipItem(firstItem,selectedId,slot);
                else SelectItem(equipped?.InstanceId??0);
            });equipmentButtons[i].GetComponentInChildren<Text>().fontSize=16;}
        }
        private void SelectItem(long id)
        {
            if(id==0||id==firstItem){firstItem=secondItem=0;return;}
            if(id==secondItem){secondItem=0;return;}
            if(firstItem==0)firstItem=id;else secondItem=id;
        }
        private void RefreshItems()
        {
            var run=controller.Run;bool prep=run.Phase==PrototypeRunPhase.Prep;
            if(itemRun!=run){itemRun=run;firstItem=secondItem=0;}
            if(run.Items.Find(firstItem)==null)firstItem=0;if(run.Items.Find(secondItem)==null)secondItem=0;
            itemHeading.text=$"ITEMS  {12-run.Items.FreeSlots}/12";
            for(int i=0;i<12;i++)
            {
                var item=run.Items.AtInventory(i);Caption(inventoryButtons[i],item==null?"—":ItemCaption(item));inventoryButtons[i].interactable=prep&&item!=null;
                Highlight(inventoryButtons[i],item!=null&&(item.InstanceId==firstItem||item.InstanceId==secondItem));
            }
            for(int i=0;i<3;i++)
            {
                var item=run.Items.AtEquipment(selectedId,i);Caption(equipmentButtons[i],item==null?$"{i+1}  ·  Empty":ItemCaption(item));
                equipmentButtons[i].interactable=prep&&run.Player.Find(selectedId)!=null;
                Highlight(equipmentButtons[i],item!=null&&item.InstanceId==firstItem);
            }
            var selected=run.Items.Find(firstItem);var other=run.Items.Find(secondItem);
            var def=selected==null?null:run.ItemCatalog.Find(selected.DefinitionId);
            itemInfo.text=def==null?"Click an item. Select two bases to combine. Select unit + item to equip.":def.DisplayName+" · "+(def.Completed?"Completed":"Base")+"\n"+def.Summary+(def.Completed?"\nUnique effect: pending":"")+(other!=null?"\n+ "+run.ItemCatalog.Find(other.DefinitionId).DisplayName:"");
            itemEquip.interactable=prep&&selected!=null&&run.Player.Find(selectedId)!=null;
            itemReturn.interactable=prep&&selected!=null&&selected.UnitId!=0&&run.Items.FreeSlots>0;
            itemCombine.interactable=prep&&selected!=null&&other!=null&&selected.UnitId==0&&other.UnitId==0&&run.ItemCatalog.Combine(selected.DefinitionId,other.DefinitionId)!=null;
            itemSell.interactable=prep&&selected!=null;Caption(itemSell,def==null?"Sell item":$"Sell · {def.SalePrice} C");
            itemGrant.interactable=itemCycle.interactable=prep;Caption(itemGrant,"TEST + "+run.ItemCatalog.Items.Where(i=>!i.Completed).ElementAt(grantIndex).DisplayName);
            itemNotice.text=run.LastMessage;
        }
        private string ItemCaption(PrototypeOwnedItem item)
        {
            var def=controller.Run.ItemCatalog.Find(item.DefinitionId);
            return (def.Completed?"◆ ":"◇ ")+def.DisplayName;
        }
    }
}
