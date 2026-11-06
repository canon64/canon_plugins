using System.Linq;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    internal sealed partial class HotelModeSession
    {
        private void DrawElevatorMenu()
        {
            GUILayout.Label("エレベーター　行き先を選択");
            _walkingScroll = GUILayout.BeginScrollView(_walkingScroll);
            HotelFloorDefinition selected = null;
            foreach (var destination in _elevatorDestinations)
            {
                if (destination.id == _floor.Definition.id)
                    GUILayout.Label(HotelElevatorRoutes.Label(destination) + "　現在の階");
                else if (GUILayout.Button(HotelElevatorRoutes.Label(destination), GUILayout.ExpandWidth(false)))
                    selected = destination;
            }
            bool close = GUILayout.Button("閉じる", GUILayout.ExpandWidth(false));
            GUILayout.EndScrollView();
            if (selected == null && !close) return;
            _walkingMenu = false; WalkingMenuChanged();
            if (selected != null)
            {
                var destination = _floors.Single(f => f.Definition == selected);
                TravelToFloor(destination, selected.arrival, selected.yaw);
            }
        }
    }
}
