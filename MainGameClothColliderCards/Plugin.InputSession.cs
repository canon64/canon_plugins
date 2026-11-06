using UnityEngine;
using MainGameUiInputCapture;
using SessionGate = CanonPlugins.Local.SessionGate;
namespace MainGameClothColliderCards
{
    public sealed partial class Plugin
    {
        private const string MakerInputOwner=Guid+".maker";
        private readonly Rect[] _makerInputRects=new Rect[1];
        private bool _makerDragHeld;
        private void CaptureMakerInput(bool visible)
        {
            if(!visible || !SessionGate.Allowed || !Application.isFocused){
                _makerDragHeld=false;UiInputCaptureApi.Sync(MakerInputOwner,"window",false);
                UiInputCaptureApi.ClearInputBlockRects(MakerInputOwner);UiInputCaptureApi.SetIdleCursorUnlock(MakerInputOwner,false);return;
            }
            _makerInputRects[0]=_makerWindow;
            UiInputCaptureApi.SetInputBlockRects(MakerInputOwner,_makerInputRects);
            UiInputCaptureApi.SetIdleCursorUnlock(MakerInputOwner,true);
            var mouse=new Vector2(Input.mousePosition.x,Screen.height-Input.mousePosition.y);
            if(Input.GetMouseButtonDown(0) && _makerWindow.Contains(mouse))_makerDragHeld=true;
            if(!Input.GetMouseButton(0))_makerDragHeld=false;
            UiInputCaptureApi.Sync(MakerInputOwner,"window",_makerDragHeld);
        }
        private void OnApplicationFocus(bool focused){if(!focused)CaptureMakerInput(false);}
        private void OnDisable()=>CaptureMakerInput(false);
    }
}
