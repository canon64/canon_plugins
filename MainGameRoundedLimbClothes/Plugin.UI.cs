using System;
using System.Globalization;
using System.Linq;
using MainGameUiInputCapture;
using UnityEngine;

namespace MainGameRoundedLimbClothes
{
    public sealed partial class Plugin
    {
        const string InputOwner=Guid+".editor";
        bool makerWindow;
        Rect window=new Rect(40,100,470,490);
        RoundedLimbController[] uiActors=Array.Empty<RoundedLimbController>();
        RoundedLimbController editing;
        readonly string[] fields=new string[4];
        int ownHotControl;
        Action pendingUi;
        void FlushUi(){if(Event.current!=null&&Event.current.type==EventType.Layout){var action=pendingUi;pendingUi=null;action?.Invoke();}}
        void BeginEditing(RoundedLimbController controller)
        {
            if(editing!=null&&editing!=controller)editing.CancelEdit();
            editing=controller;editing.BeginEdit();ResetFields();
        }
        void ResetFields()
        {if(editing!=null)for(int i=0;i<4;++i)fields[i]=editing.Settings.Get(i).ToString("0.###",CultureInfo.InvariantCulture);}
        void CancelEditing()
        {pendingUi=null;if(editing!=null)editing.CancelEdit();editing=null;}
        void RegisterWindowInput()
        {if(!applicationFocused)return;UiInputCaptureApi.SetInputBlockRects(InputOwner,new[]{window});UiInputCaptureApi.SetIdleCursorUnlock(InputOwner,true);}
        void ReleaseInput()
        {
            UiInputCaptureApi.Sync(InputOwner,"drag",false);UiInputCaptureApi.EndOwner(InputOwner);
            UiInputCaptureApi.ClearInputBlockRects(InputOwner);UiInputCaptureApi.SetIdleCursorUnlock(InputOwner,false);
            if(ownHotControl!=0&&GUIUtility.hotControl==ownHotControl)GUIUtility.hotControl=0;
            ownHotControl=0;
        }
        void OnGUI()
        {
            FlushUi();
            if(!Allowed||!makerWindow||!applicationFocused)return;
            window.width=Math.Min(470,Math.Max(300,Screen.width-20));
            window.height=Math.Min(490,Math.Max(300,Screen.height-20));
            window.x=Mathf.Clamp(window.x,0,Math.Max(0,Screen.width-window.width));
            window.y=Mathf.Clamp(window.y,0,Math.Max(0,Screen.height-window.height));
            bool oldEnabled=GUI.enabled;var oldMatrix=GUI.matrix;var oldColor=GUI.color;int oldDepth=GUI.depth;
            try
            {
                GUI.enabled=true;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.depth=-20000;
                window=GUI.Window(0x524c434c,window,DrawWindow,"丸い手足：位置の調整");RegisterWindowInput();
            }
            finally{GUI.enabled=oldEnabled;GUI.matrix=oldMatrix;GUI.color=oldColor;GUI.depth=oldDepth;}
        }
        void DrawWindow(int id)
        {
            DrawEditor();
            GUI.DragWindow(new Rect(0,0,window.width,24));
            CaptureDrag();
        }
        void DrawTab()
        {
            if(!Allowed||!applicationFocused)return;
            bool oldEnabled=GUI.enabled;
            try{GUI.enabled=true;DrawTabContent();}finally{GUI.enabled=oldEnabled;}
        }
        void DrawTabContent()
        {
            FlushUi();
            if(!Allowed)return;
            GUILayout.BeginVertical();
            GUILayout.Label("対象キャラ");
            foreach(var c in uiActors)
            {
                if(c==null)continue;
                string name=c.ChaControl.chaFile.parameter.fullname;
                if(GUILayout.Button((editing==c?"● ":"")+name,GUILayout.ExpandWidth(false),GUILayout.Height(28)))pendingUi=()=>BeginEditing(c);
            }
            DrawEditor();
            GUILayout.EndVertical();
            // The enclosing UnifiedUi / PlaybackBar host owns its complete input rectangle.
            // This owner explicitly retains a slider drag even after the cursor leaves that rectangle.
            UiInputCaptureApi.SetIdleCursorUnlock(InputOwner,true);
            CaptureDrag();
        }
        void DrawEditor()
        {
            if(editing==null||!editing.Allowed){GUILayout.Label("許可された対象キャラがいません。");return;}
            GUILayout.Label(editing.Status);
            GUILayout.Label("0%：付け根側　100%：肘・膝側（丸く閉じられる範囲）");
            var value=editing.Settings.Copy();bool changed=false;
            GUILayout.BeginVertical(GUI.skin.box);GUILayout.Label("手袋：二の腕");
            bool link=GUILayout.Toggle(value.LinkArms,"腕の左右を連動");
            if(link!=value.LinkArms){value.LinkArms=link;if(link)value.RightArm=value.LeftArm;changed=true;}
            changed|=DrawPosition(0,"左腕",value);changed|=DrawPosition(1,"右腕",value);GUILayout.EndVertical();
            GUILayout.BeginVertical(GUI.skin.box);GUILayout.Label("靴下：太もも");
            link=GUILayout.Toggle(value.LinkLegs,"脚の左右を連動");
            if(link!=value.LinkLegs){value.LinkLegs=link;if(link)value.RightLeg=value.LeftLeg;changed=true;}
            changed|=DrawPosition(2,"左脚",value);changed|=DrawPosition(3,"右脚",value);GUILayout.EndVertical();
            if(changed){editing.Preview(value);ResetLinkedFields(value);}
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("決定",GUILayout.Width(85),GUILayout.Height(28)))pendingUi=()=>{if(editing==null||!editing.Commit())return;editing=null;makerWindow=false;ReleaseInput();};
            if(GUILayout.Button("閉じる",GUILayout.Width(85),GUILayout.Height(28)))
            {pendingUi=()=>{CancelEditing();makerWindow=false;ReleaseInput();};}
            GUILayout.EndHorizontal();
        }
        bool DrawPosition(int index,string label,PositionSettings value)
        {
            GUILayout.BeginHorizontal();GUILayout.Label(label,GUILayout.Width(38));
            float before=value.Get(index);
            float next=GUILayout.HorizontalSlider(before,0,100,GUILayout.MinWidth(100),GUILayout.MaxWidth(235));
            string field=GUILayout.TextField(fields[index]??before.ToString(CultureInfo.InvariantCulture),GUILayout.Width(68));
            GUILayout.Label("%",GUILayout.Width(20));GUILayout.EndHorizontal();
            if(next!=before){value.Set(index,next);fields[index]=next.ToString("0.###",CultureInfo.InvariantCulture);return true;}
            if(field==fields[index])return false;
            fields[index]=field;
            if(float.TryParse(field,NumberStyles.Float,CultureInfo.InvariantCulture,out next)&&!float.IsNaN(next)&&!float.IsInfinity(next)&&next>=0&&next<=100)
            {value.Set(index,next);return next!=before;}
            return false;
        }
        void ResetLinkedFields(PositionSettings attempted)
        {
            if(editing==null)return;
            // Preserve unfinished numeric input. Only update the other side when it really changed.
            for(int i=0;i<4;++i)
                if(editing.Settings.Get(i)!=attempted.Get(i)||
                    (i<2?attempted.LinkArms:attempted.LinkLegs)&&float.TryParse(fields[i],NumberStyles.Float,CultureInfo.InvariantCulture,out float parsed)&&parsed!=editing.Settings.Get(i))
                    fields[i]=editing.Settings.Get(i).ToString("0.###",CultureInfo.InvariantCulture);
        }
        void CaptureDrag()
        {
            var e=Event.current;if(e==null)return;
            if(e.rawType==EventType.MouseDown&&GUIUtility.hotControl!=0)ownHotControl=GUIUtility.hotControl;
            bool dragging=ownHotControl!=0&&GUIUtility.hotControl==ownHotControl;
            if(e.rawType==EventType.MouseUp){ownHotControl=0;dragging=false;}
            UiInputCaptureApi.Sync(InputOwner,"drag",dragging);
        }
    }
}
