using HarmonyLib;

namespace MainGirlShoulderIkStabilizer;

internal static class ShoulderIkStabilizerPatches
{
	[HarmonyPostfix]
	[HarmonyPatch(typeof(HSceneProc), "LateUpdate")]
	private static void Postfix_HSceneProcLateUpdate(HSceneProc __instance)
	{
		ShoulderIkStabilizerPlugin.Instance?.OnAfterHSceneLateUpdate(__instance);
	}
}
