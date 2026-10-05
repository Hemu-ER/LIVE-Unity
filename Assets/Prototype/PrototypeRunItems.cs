using System;
using System.Collections.Generic;
using System.Linq;

namespace LIVE.Prototype
{
    public sealed partial class PrototypeRunModel
    {
        public PrototypeItemCatalog ItemCatalog { get; } = PrototypeItemCatalog.Load();
        public PrototypeItemInventory Items { get; private set; }
        private bool ItemFailure(string message) { LastMessage=message;return false; }
        private bool ItemChanged(bool success,string message)
        { LastMessage=success?message:"Item action rejected: check selection, recipe or available slots.";if(success)Revision++;return success; }
        // Rewards can arrive in any phase; only the newly awarded item is auto-sold on overflow.
        public PrototypeItemAcquisition AcquireItem(string definition)
        {
            var item=ItemCatalog.Find(definition);if(item==null)return PrototypeItemAcquisition.Rejected;
            bool full=Items.FreeSlots==0;
            if(full)Player.Credits+=item.SalePrice;else Items.Add(definition);
            LastMessage=full?$"New item auto-sold for {item.SalePrice} Credits.":"Acquired "+item.DisplayName;
            Revision++;return full?PrototypeItemAcquisition.AutoSold:PrototypeItemAcquisition.Stored;
        }
        public bool EquipItem(long item,long unit,int slot=-1)
        {
            if(!CanPrepare())return false;
            if(Player.Find(unit)==null)return ItemFailure("Select an owned unit.");
            return ItemChanged(Items.Equip(item,unit,slot,ItemCatalog),"Item equipped / combined.");
        }
        public bool UnequipItem(long item)
        { if(!CanPrepare())return false;return ItemChanged(Items.Return(Items.Find(item)),"Item returned to inventory."); }
        public bool CombineItems(long first,long second)
        { if(!CanPrepare())return false;return ItemChanged(Items.Combine(first,second,ItemCatalog),"Items combined."); }
        public bool SellItem(long id)
        {
            if(!CanPrepare())return false;var item=Items.Find(id);if(item==null)return ItemFailure("Select an item.");
            int price=ItemCatalog.Find(item.DefinitionId).SalePrice;Items.Remove(item);Player.Credits+=price;
            return ItemChanged(true,$"Item sold for {price} Credits.");
        }
        public PrototypeCombatStats DerivedStats(long unit)
        {
            var owned=Player.Find(unit);if(owned==null)throw new ArgumentException("Unknown owned unit.");
            return ItemCatalog.Derive(data.CombatStats(owned.DefinitionId,owned.Stars),Items.Equipped(unit));
        }
        // Match Player.MergeAll's survivor and cascade ordering before spending Credits or consuming shop/pool state.
        private bool CanReturnMergeEquipment(string purchase)
        {
            var shadow=Player.OwnedUnits.Select(u=>new PrototypeOwnedUnit(u.InstanceId,u.DefinitionId){Stars=u.Stars,Location=u.Location}).ToList();
            shadow.Add(new PrototypeOwnedUnit(long.MaxValue,purchase){Location=PrototypeUnitLocation.Bench});
            int free=Items.FreeSlots;
            while(true)
            {
                bool merged=false;
                foreach(var seed in shadow.OrderBy(u=>u.InstanceId).ToArray())
                {
                    if(seed.Stars>=3)continue;
                    var group=shadow.Where(u=>u.DefinitionId==seed.DefinitionId&&u.Stars==seed.Stars)
                        .OrderBy(u=>u.Location==PrototypeUnitLocation.Board?0:1).ThenBy(u=>u.InstanceId).Take(3).ToArray();
                    if(group.Length<3)continue;
                    free-=Items.Equipped(group[1].InstanceId).Count()+Items.Equipped(group[2].InstanceId).Count();
                    if(free<0)return false;
                    group[0].Stars++;shadow.Remove(group[1]);shadow.Remove(group[2]);merged=true;break;
                }
                if(!merged)return true;
            }
        }
    }
}
