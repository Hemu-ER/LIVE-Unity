using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace LIVE.Prototype.Editor
{
    public static class PrototypeUiSmokeCheck
    {
        private static int assertions;
        private static void Check(bool value,string message){assertions++;if(!value)throw new Exception("UI smoke: "+message);}
        private static void Click(Button button)
        {
            ExecuteEvents.Execute(button.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
        }
        public static void Validate(BattlefieldPrototype board)
        {
            assertions=0;var c=board.RunController;var ui=board.GetComponent<PrototypeRunHud>();
            c.SetDataMode(PrototypeDataMode.WebRoster);c.Data.Rules.StartingCredits=100;c.ResetRun();ui.Refresh();
            Check(ui!=null&&ui.Canvas!=null&&ui.ShopButtons.Length==5&&ui.BenchButtons.Length==8,"uGUI five cards/eight slots");
            var backdrop=board.transform.Find("UI backdrop camera").GetComponent<Camera>();
            Check(backdrop.cullingMask==0&&backdrop.rect==new Rect(0,0,1,1)&&backdrop.depth<board.View.depth&&backdrop.clearFlags==CameraClearFlags.SolidColor,"Full-frame clear precedes partial battlefield viewport");
            Check(EventSystem.current!=null&&ui.Canvas.GetComponent<GraphicRaycaster>()!=null,"Pointer input and raycaster installed");
            foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1600,900),new Vector2Int(1280,720)})
            {
                ui.RefreshForSize(size.x,size.y);float scale=PrototypeRunHud.Scale(size.x,size.y);var offset=PrototypeRunHud.Offset(size.x,size.y);
                var zones=new[]{PrototypeRunHud.HudArea,PrototypeRunHud.BoardArea,PrototypeRunHud.BenchArea,PrototypeRunHud.ShopArea,PrototypeRunHud.ActionsArea};
                foreach(var r in zones)Check(r.xMin*scale+offset.x>=0&&r.yMin*scale+offset.y>=0&&r.xMax*scale+offset.x<=size.x&&r.yMax*scale+offset.y<=size.y,"Zone within resolution "+size);
                for(int i=0;i<zones.Length;i++)for(int j=i+1;j<zones.Length;j++)Check(!zones[i].Overlaps(zones[j]),"Major zones do not overlap "+size);
                foreach(var buttons in new[]{ui.ShopButtons,ui.BenchButtons})
                for(int i=0;i<buttons.Length;i++)
                {var rt=(RectTransform)buttons[i].transform;Check(rt.sizeDelta.x*scale>100&&rt.sizeDelta.y*scale>=32,"Usable physical click size "+size);if(i>0){var prev=(RectTransform)buttons[i-1].transform;Check(rt.anchoredPosition.x>=prev.anchoredPosition.x+prev.sizeDelta.x,"Cards do not overlap");}}
            }
            ui.Refresh();Canvas.ForceUpdateCanvases();
            Check(ui.CreditsText.Contains(c.Run.Player.Credits.ToString())&&ui.MasteryText.Contains("EXP")&&ui.TimerText.Contains("s"),"Live credits/mastery/timer");
            var hitPoint=((RectTransform)ui.ShopButtons[0].transform).TransformPoint(((RectTransform)ui.ShopButtons[0].transform).rect.center);
            var hits=new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,hitPoint)},hits);
            Check(hits.Count>0&&hits[0].gameObject==ui.ShopButtons[0].gameObject,"Graphic raycast reaches visible shop card, not an overlay");
            string id=c.Run.Shop.Slot(0);Check(ui.ShopButtons[0].GetComponentInChildren<Text>().text.Contains(c.Data.Definition(id).DisplayName),"Actual roster DisplayName");
            int money=c.Run.Player.Credits;Click(ui.ShopButtons[0]);Check(c.Run.Player.Credits==money-c.Data.Definition(id).Cost&&c.Run.Shop.Slot(0)==null,"Pointer purchase delegates to model");
            Check(!ui.ShopButtons[0].interactable&&ui.ShopButtons[0].GetComponentInChildren<Text>().text=="SOLD","Sold state");
            var owned=c.Run.Player.OwnedUnits.First(u=>u.Location==PrototypeUnitLocation.Bench);Click(ui.BenchButtons[owned.BenchSlot]);
            Check(ui.SelectedId==owned.InstanceId&&ui.InspectorVisible&&ui.SelectionText.Contains(c.Data.Definition(owned.DefinitionId).DisplayName)&&ui.SelectionText.Contains("AMP"),"Bench selection updates inspector");
            Check(ui.BenchButtons[owned.BenchSlot].GetComponent<Outline>().enabled,"Selection outline");
            Click(ui.BoardButtons[0]);Check(owned.Location==PrototypeUnitLocation.Board&&owned.Row==0&&owned.Column==0&&ui.SelectedId==0,"Blue board placement");
            Click(ui.BoardButtons[0]);Click(ui.BoardButtons[1]);Check(owned.Column==1,"Select placed unit and move board cell");
            Click(ui.BoardButtons[1]);int empty=c.Run.Player.EmptyBench();Click(ui.BenchButtons[empty]);Check(owned.Location==PrototypeUnitLocation.Bench,"Return to empty bench");
            Click(ui.BenchButtons[empty]);money=c.Run.Player.Credits;Click(ui.SellButton);Check(c.Run.Player.Find(owned.InstanceId)==null&&c.Run.Player.Credits>money&&!ui.InspectorVisible,"Sell clears selection");
            money=c.Run.Player.Credits;Click(ui.RerollButton);Check(c.Run.Player.Credits==money-c.Data.Rules.RerollCost,"Reroll cost via existing API");
            money=c.Run.Player.Credits;Click(ui.InvestButton);Check(c.Run.Player.Credits==money-c.Data.Rules.InvestCost,"Mastery investment via API");
            Click(ui.ReadyButton);Check(c.Run.Phase==PrototypeRunPhase.Combat,"Ready starts combat");
            Check(!ui.ReadyButton.interactable&&!ui.RerollButton.interactable&&!ui.InvestButton.interactable&&!ui.SellButton.interactable&&ui.ShopButtons.All(b=>!b.interactable)&&ui.BenchButtons.All(b=>!b.interactable),"Combat locks prep controls");
            money=c.Run.Player.Credits;Click(ui.RerollButton);Check(c.Run.Player.Credits==money,"Disabled pointer action cannot mutate model");
            int ticks=0;while(c.Run.Phase==PrototypeRunPhase.Combat&&ticks++<3100)c.Step(.02f);
            ui.Refresh();Check(c.Run.Phase==PrototypeRunPhase.Result,"Combat -> Result");int round=c.Run.Round;
            while(c.Run.Phase!=PrototypeRunPhase.Prep&&ticks++<4000)c.Step(.02f);
            ui.Refresh();Check(c.Run.Phase==PrototypeRunPhase.Prep&&c.Run.Round==round+1&&ui.ReadyButton.interactable,"Result -> next Prep controls recover");
            Click(ui.ResetButton);Check(c.Run.Round==1&&c.Run.Phase==PrototypeRunPhase.Prep&&ui.SelectedId==0,"Reset Run");
            while(c.Run.Player.Credits>=c.Data.Rules.RerollCost)c.Reroll();ui.Refresh();Check(!ui.RerollButton.interactable,"Insufficient credits disables reroll");
            c.SetDataMode(PrototypeDataMode.WebRoster);ui.Refresh();
            Debug.Log($"UI_SMOKE_CHECK_PASSED: {assertions} assertions; 1920/1600/1280 layouts, uGUI pointer commands, roster names, selection, controls and complete phase loop.");
        }
    }
}
