using UnityEngine;

namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private readonly GadgetBarConnection _handcuffsBar=new GadgetBarConnection("com.kks.maingame.vrhandcuffs",
            "左クリック：初回だけ手錠を視点前に呼び出し→以後ギズモ表示／非表示 / 右クリック：削除・手首IKを復元");
        private void DrawHandcuffsButton(Rect rect) { DrawGadgetBarButton(rect,"手錠",_handcuffsBar); }
    }
}
