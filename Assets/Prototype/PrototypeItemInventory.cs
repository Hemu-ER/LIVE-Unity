using System.Collections.Generic;
using System.Linq;

namespace LIVE.Prototype
{
    public enum PrototypeItemAcquisition { Rejected, Stored, AutoSold }
    public sealed class PrototypeOwnedItem
    {
        public long InstanceId { get; }
        public string DefinitionId { get; internal set; }
        // UnitId zero means inventory; otherwise Slot is the owned unit's equipment slot.
        public long UnitId { get; internal set; }
        public int Slot { get; internal set; }
        internal PrototypeOwnedItem(long id,string definition,int slot) { InstanceId=id;DefinitionId=definition;Slot=slot; }
    }
    // Persistent ownership ledger. Mutations are internal and routed through RunModel phase checks.
    public sealed class PrototypeItemInventory
    {
        public const int Capacity=12, EquipmentCapacity=3;
        private readonly List<PrototypeOwnedItem> items=new List<PrototypeOwnedItem>();
        private long nextId=1;
        public IReadOnlyList<PrototypeOwnedItem> Items=>items.AsReadOnly();
        public int FreeSlots=>Capacity-items.Count(i=>i.UnitId==0);
        public PrototypeOwnedItem Find(long id)=>items.Find(i=>i.InstanceId==id);
        public PrototypeOwnedItem AtInventory(int slot)=>items.Find(i=>i.UnitId==0&&i.Slot==slot);
        public PrototypeOwnedItem AtEquipment(long unit,int slot)=>unit==0?null:items.Find(i=>i.UnitId==unit&&i.Slot==slot);
        public IEnumerable<PrototypeOwnedItem> Equipped(long unit)=>items.Where(i=>unit!=0&&i.UnitId==unit).OrderBy(i=>i.Slot);
        internal PrototypeOwnedItem Add(string definition)
        {
            if(FreeSlots==0)return null;
            int slot=0;while(AtInventory(slot)!=null)slot++;
            var item=new PrototypeOwnedItem(nextId++,definition,slot);items.Add(item);return item;
        }
        internal void Remove(PrototypeOwnedItem item)=>items.Remove(item);
        internal bool ReturnUnit(long unit)
        {
            var equipment=Equipped(unit).ToArray();if(FreeSlots<equipment.Length)return false;
            foreach(var item in equipment)Return(item);return true;
        }
        internal bool Return(PrototypeOwnedItem item)
        {
            if(item==null||item.UnitId==0||FreeSlots==0)return false;
            int slot=0;while(AtInventory(slot)!=null)slot++;item.UnitId=0;item.Slot=slot;return true;
        }
        internal bool Combine(long first,long second,PrototypeItemCatalog catalog)
        {
            var a=Find(first);var b=Find(second);
            if(a==null||b==null||a==b||a.UnitId!=0||b.UnitId!=0)return false;
            string result=catalog.Combine(a.DefinitionId,b.DefinitionId);if(result==null)return false;
            a.DefinitionId=result;Remove(b);return true;
        }
        internal bool Equip(long itemId,long unit,int requestedSlot,PrototypeItemCatalog catalog)
        {
            var source=Find(itemId);if(source==null||unit<=0||requestedSlot < -1||requestedSlot>=EquipmentCapacity)return false;
            if(source.UnitId==unit&&requestedSlot==source.Slot)return true;
            // A unit may keep at most one base item. The second base always combines in that slot,
            // even if all three slots are full; an explicit occupied completed slot is not a combine target.
            var basic=Equipped(unit).FirstOrDefault(i=>i!=source&&!catalog.Find(i.DefinitionId).Completed);
            if(!catalog.Find(source.DefinitionId).Completed&&basic!=null)
            {
                if(requestedSlot>=0&&requestedSlot!=basic.Slot&&AtEquipment(unit,requestedSlot)!=null)return false;
                string result=catalog.Combine(basic.DefinitionId,source.DefinitionId);if(result==null)return false;
                basic.DefinitionId=result;Remove(source);return true;
            }
            int slot=requestedSlot;
            if(slot<0) { slot=0;while(slot<EquipmentCapacity&&AtEquipment(unit,slot)!=null)slot++; }
            if(slot>=EquipmentCapacity||AtEquipment(unit,slot)!=null)return false;
            source.UnitId=unit;source.Slot=slot;return true;
        }
    }
}
