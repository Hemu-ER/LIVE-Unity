using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace LIVE.Prototype
{
    // Presentation only. All commands are delegated to the existing controller API.
    public sealed class PrototypeRunHud : MonoBehaviour
    {
        public const float DesignWidth = 1920, DesignHeight = 1080;
        public static readonly Rect BoardArea = new Rect(310, 104, 1300, 610);
        public static readonly Rect HudArea = new Rect(24, 16, 1872, 68);
        public static readonly Rect BenchArea = new Rect(24, 736, 1872, 104);
        public static readonly Rect ShopArea = new Rect(24, 856, 1448, 200);
        public static readonly Rect ActionsArea = new Rect(1490, 856, 406, 200);
        private PrototypeRunController controller;
        private BattlefieldPrototype board;
        private long selectedId;
        private Canvas canvas;
        private RectTransform root;
        private Font font;
        private GameObject ownedEvents;
        private Text heading, credits, mastery, timer, message, details;
        private GameObject inspector;
        private Button[] shop = new Button[5], bench = new Button[8], cells = new Button[9];
        private Button reroll, invest, sell, ready, clear, reset;
        private readonly Dictionary<PrototypeUnit, Text> labels = new Dictionary<PrototypeUnit, Text>();
        private readonly List<PrototypeUnit> stale = new List<PrototypeUnit>();
        private static readonly Color Panel = new Color(.055f,.075f,.105f,.98f);
        private static readonly Color Normal = new Color(.11f,.15f,.21f);
        private static readonly Color Accent = new Color(.25f,.77f,.83f);
        public Canvas Canvas => canvas;
        public long SelectedId => selectedId;
        public Button[] ShopButtons => shop;
        public Button[] BenchButtons => bench;
        public Button[] BoardButtons => cells;
        public Button ReadyButton => ready;
        public Button SellButton => sell;
        public Button RerollButton => reroll;
        public Button InvestButton => invest;
        public Button ResetButton => reset;
        public string CreditsText => credits.text;
        public string PhaseText => heading.text;
        public string MasteryText => mastery.text;
        public string TimerText => timer.text;
        public string SelectionText => details.text;
        public bool InspectorVisible => inspector.activeSelf;

        public void Initialize(PrototypeRunController runController, BattlefieldPrototype battlefield)
        {
            controller=runController; board=battlefield;
            // The battlefield camera clears only its viewport. Clear the whole frame first
            // so hidden/changed overlay widgets never leave pixels outside that viewport.
            var backdrop=new GameObject("UI backdrop camera",typeof(Camera));backdrop.transform.SetParent(transform,false);
            var background=backdrop.GetComponent<Camera>();background.clearFlags=CameraClearFlags.SolidColor;
            background.backgroundColor=new Color(.035f,.05f,.085f);background.cullingMask=0;background.depth=board.View.depth-1;
            font=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Apple SD Gothic Neo","Noto Sans CJK KR"},24);
            var go=new GameObject("Playtest Canvas",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));
            go.transform.SetParent(transform,false);canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=100;
            root=new GameObject("1920x1080 layout",typeof(RectTransform)).GetComponent<RectTransform>();root.SetParent(go.transform,false);
            root.anchorMin=root.anchorMax=root.pivot=new Vector2(.5f,.5f);root.sizeDelta=new Vector2(DesignWidth,DesignHeight);
            if(EventSystem.current==null)
            { ownedEvents=new GameObject("Playtest EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));ownedEvents.transform.SetParent(transform,false);ownedEvents.GetComponent<InputSystemUIInputModule>().AssignDefaultActions(); }
            Build();Refresh();
        }
        public static float Scale(int width,int height)=>Mathf.Max(.01f,Mathf.Min(width/DesignWidth,height/DesignHeight));
        public static Vector2 Offset(int width,int height){float s=Scale(width,height);return new Vector2((width-DesignWidth*s)/2,(height-DesignHeight*s)/2);}
        public static Rect CameraViewport(int width,int height)
        {
            float s=Scale(width,height);var o=Offset(width,height);
            return new Rect((o.x+BoardArea.x*s)/Mathf.Max(1,width),1-(o.y+BoardArea.yMax*s)/Mathf.Max(1,height),BoardArea.width*s/Mathf.Max(1,width),BoardArea.height*s/Mathf.Max(1,height));
        }
        private RectTransform Box(string name,Rect rect,Color color)
        {
            var obj=new GameObject(name,typeof(RectTransform),typeof(Image));var rt=obj.GetComponent<RectTransform>();rt.SetParent(root,false);Place(rt,rect);
            var image=obj.GetComponent<Image>();image.color=color;image.raycastTarget=false;return rt;
        }
        public static void Place(RectTransform rt,Rect rect)
        { rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0,1);rt.anchoredPosition=new Vector2(rect.x,-rect.y);rt.sizeDelta=rect.size; }
        private Text Label(string name,Rect rect,int size=24,TextAnchor align=TextAnchor.MiddleLeft)
        {
            var obj=new GameObject(name,typeof(RectTransform),typeof(Text));var text=obj.GetComponent<Text>();text.transform.SetParent(root,false);Place(text.rectTransform,rect);
            text.font=font;text.fontSize=size;text.color=new Color(.9f,.93f,.96f);text.alignment=align;text.raycastTarget=false;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;return text;
        }
        private Button Button(string name,Rect rect,Action action)
        {
            var rt=Box(name,rect,Normal);var image=rt.GetComponent<Image>();image.raycastTarget=true;
            var button=rt.gameObject.AddComponent<Button>();button.targetGraphic=image;
            var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.35f,1.35f,1.35f);colors.pressedColor=Accent;colors.disabledColor=new Color(.42f,.46f,.52f,.8f);button.colors=colors;
            var caption=Label(name+" label",new Rect(8,4,rect.width-16,rect.height-8),23,TextAnchor.MiddleCenter);caption.transform.SetParent(rt,false);Place(caption.rectTransform,new Rect(8,4,rect.width-16,rect.height-8));
            button.onClick.AddListener(()=>{action();Refresh();});return button;
        }
        private static void Caption(Button button,string value)=>button.GetComponentInChildren<Text>().text=value;
        private void Build()
        {
            Box("HUD",HudArea,Panel);heading=Label("Round and phase",new Rect(44,24,530,52),28);
            credits=Label("Credits",new Rect(650,24,250,52));mastery=Label("Mastery",new Rect(920,24,590,52));timer=Label("Timer",new Rect(1520,24,170,52),26);
            reset=Button("Reset Run",new Rect(1720,30,152,40),()=>{selectedId=0;controller.ResetRun();});Caption(reset,"Reset Run");reset.GetComponentInChildren<Text>().fontSize=18;
            message=Label("Round result",new Rect(540,684,840,40),22,TextAnchor.MiddleCenter);
            Box("Bench panel",BenchArea,Panel);Label("Bench heading",new Rect(42,742,180,28),18).text="BENCH";
            for(int i=0;i<8;i++){int slot=i;bench[i]=Button("Bench "+i,new Rect(42+i*231,778,217,50),()=>ClickSlot(controller.Run.Player.AtBench(slot),PrototypeUnitLocation.Bench,slot,0));}
            Box("Shop panel",ShopArea,Panel);Label("Shop heading",new Rect(42,864,300,26),18).text="SHOP";
            for(int i=0;i<5;i++){int slot=i;shop[i]=Button("Shop "+i,new Rect(42+i*283,902,269,136),()=>controller.Buy(slot));}
            Box("Actions panel",ActionsArea,Panel);
            reroll=Button("Reroll",new Rect(1506,872,180,50),()=>controller.Reroll());
            invest=Button("Mastery invest",new Rect(1698,872,182,50),()=>controller.Invest());
            sell=Button("Sell selected",new Rect(1506,934,180,42),()=>{if(controller.Sell(selectedId))selectedId=0;});
            clear=Button("Clear selection",new Rect(1698,934,182,42),()=>selectedId=0);Caption(clear,"Clear selection");clear.GetComponentInChildren<Text>().fontSize=18;
            ready=Button("Ready",new Rect(1506,988,374,52),()=>{if(controller.Ready())selectedId=0;});Caption(ready,"READY");ready.image.color=new Color(.12f,.37f,.4f);
            for(int row=0;row<3;row++)for(int col=0;col<3;col++)
            {int r=row,c=col;var btn=Button("Blue cell "+r+","+c,new Rect(0,0,1,1),()=>ClickSlot(controller.Run.Player.AtBoard(r,c),PrototypeUnitLocation.Board,r,c));btn.image.color=new Color(1,1,1,.001f);cells[row*3+col]=btn;}
            inspector=Box("Selected unit",new Rect(24,194,264,330),Panel).gameObject;
            details=Label("Selected stats",new Rect(40,204,232,300),22);details.transform.SetParent(inspector.transform,false);Place(details.rectTransform,new Rect(16,10,232,300));
        }
        private void ClickSlot(PrototypeOwnedUnit occupant,PrototypeUnitLocation location,int first,int second)
        {
            if(controller.Run.Phase!=PrototypeRunPhase.Prep)return;
            if(occupant!=null){selectedId=selectedId==occupant.InstanceId?0:occupant.InstanceId;return;}
            if(selectedId!=0&&controller.Move(selectedId,location,first,second))selectedId=0;
        }
        private void LateUpdate()=>Refresh();
        public void Refresh()=>RefreshForSize(Screen.width,Screen.height);
        public void RefreshForSize(int width,int height)
        {
            if(controller==null||controller.Run==null)return;
            root.localScale=Vector3.one*Scale(width,height);
            var run=controller.Run;var player=run.Player;bool prep=run.Phase==PrototypeRunPhase.Prep;
            if(!prep||player.Find(selectedId)==null)selectedId=0;
            heading.text=$"LIVE    ROUND {run.Round}    {run.Phase}";credits.text=$"Credits  {player.Credits}";
            mastery.text=$"Mastery Lv {player.Mastery.Level}    "+(player.Mastery.Level==PrototypeMastery.MaxLevel?"MAX":$"{player.Mastery.CurrentExp}/{player.Mastery.NextLevelExp} EXP");timer.text=$"{Mathf.CeilToInt(run.RemainingSeconds)} s";
            message.text=run.Phase==PrototypeRunPhase.Result?run.LastMessage:"";
            for(int i=0;i<8;i++)
            {
                var unit=player.AtBench(i);Caption(bench[i],unit==null?"—":controller.Label(unit.DefinitionId,unit.Stars));bench[i].interactable=prep;
                bench[i].image.color=unit!=null&&unit.InstanceId==selectedId?new Color(.15f,.4f,.46f):Normal;
                Highlight(bench[i],unit!=null&&unit.InstanceId==selectedId);
            }
            for(int i=0;i<5;i++)
            {
                string id=run.Shop.Slot(i);var def=id==null?null:controller.Data.Definition(id);
                bool available=prep&&def!=null&&player.Credits>=def.Cost&&player.EmptyBench()>=0;
                shop[i].interactable=available;Caption(shop[i],def==null?"SOLD":$"{def.DisplayName}\n\n{def.Cost} Credits"+(available?"":prep?"  · Unavailable":"  · Locked"));
                shop[i].image.color=def==null?new Color(.065f,.08f,.105f):Normal;
            }
            reroll.interactable=prep&&player.Credits>=controller.Data.Rules.RerollCost;Caption(reroll,$"Reroll · {controller.Data.Rules.RerollCost}");
            invest.interactable=prep&&player.Credits>=controller.Data.Rules.InvestCost&&player.Mastery.Level<PrototypeMastery.MaxLevel;Caption(invest,$"Mastery +{controller.Data.Rules.InvestExp} EXP · {controller.Data.Rules.InvestCost}");invest.GetComponentInChildren<Text>().fontSize=16;
            var selected=player.Find(selectedId);sell.interactable=prep&&selected!=null;Caption(sell,selected==null?"Sell Selected":$"Sell · {PrototypeEconomy.SalePrice(controller.Data.Definition(selected.DefinitionId).Cost,selected.Stars)} Credits");
            clear.interactable=selected!=null;ready.interactable=prep;inspector.SetActive(selected!=null);
            if(selected!=null)
            {
                var def=controller.Data.Definition(selected.DefinitionId);var stats=controller.Data.CombatStats(selected.DefinitionId,selected.Stars);
                details.text=$"{def.DisplayName}\n{new string('*',selected.Stars)}   ·   Cost {def.Cost}\n\nHP   {stats.MaxHealth}\nAP   {stats.AttackPower}     AMP   {stats.SkillAmplification:0.#}\nDEF  {stats.Defense}\nAS   {stats.AttackSpeed:0.##}\nRange   {stats.AttackRange}";
            }
            UpdateBoard(width,height,prep);
        }
        private static void Highlight(Button button,bool selected)
        {var outline=button.GetComponent<Outline>();if(outline==null){outline=button.gameObject.AddComponent<Outline>();outline.effectColor=Accent;outline.effectDistance=new Vector2(2,-2);}outline.enabled=selected;}
        private Vector2 Logical(Vector3 world,int width,int height)
        {
            var point=board.View.WorldToViewportPoint(world);var viewport=CameraViewport(width,height);
            return (new Vector2((viewport.x+point.x*viewport.width)*width,(1-viewport.y-point.y*viewport.height)*height)-Offset(width,height))/Scale(width,height);
        }
        private void UpdateBoard(int width,int height,bool prep)
        {
            for(int row=0;row<3;row++)for(int col=0;col<3;col++)
            {
                var center=Logical(board.transform.TransformPoint(BattlefieldPrototype.Position(row,col)),width,height);
                var edge=Logical(board.transform.TransformPoint(BattlefieldPrototype.Position(row,col)+Vector3.right*1.54f),width,height);
                float size=Mathf.Abs(edge.x-center.x);var button=cells[row*3+col];Place((RectTransform)button.transform,new Rect(center.x-size/2,center.y-size/2,size,size));button.gameObject.SetActive(prep);
                var unit=controller.Run.Player.AtBoard(row,col);Highlight(button,unit!=null&&unit.InstanceId==selectedId);
                button.image.color=unit!=null&&unit.InstanceId==selectedId?new Color(.2f,.8f,.9f,.13f):new Color(1,1,1,.001f);
            }
            stale.Clear();foreach(var pair in labels)if(pair.Key==null||!pair.Key.gameObject.activeInHierarchy||Array.IndexOf(board.Units,pair.Key)<0)stale.Add(pair.Key);
            foreach(var unit in stale){Destroy(labels[unit].gameObject);labels.Remove(unit);}
            foreach(var unit in board.Units)
            {
                if(unit==null)continue;
                if(!labels.TryGetValue(unit,out var label)){label=Label("Unit name",new Rect(),20,TextAnchor.MiddleCenter);labels.Add(unit,label);}
                label.gameObject.SetActive(unit.IsAlive);label.text=unit.DisplayLabel;
                var point=Logical(unit.transform.position+Vector3.up*.99f,width,height);Place(label.rectTransform,new Rect(point.x-106,point.y-16,212,30));
            }
        }
        private void OnDestroy(){if(font!=null)Destroy(font);if(ownedEvents!=null)Destroy(ownedEvents);}
    }
}
