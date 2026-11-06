using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ActionGame;
using ActionGame.MapSound;
using Cysharp.Threading.Tasks;
using Illusion.Extensions;
using Manager;
using RootMotion.FinalIK;
using Sound;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Illusion.Game;

public static class Utils
{
	public static class IKLoader
	{
		public static void Execute(FullBodyBipedIK ik, List<List<string>> dataList)
		{
			Transform[] componentsInChildren = ik.GetComponentsInChildren<Transform>(includeInactive: true);
			int num = 0;
			List<string> list = dataList[num++];
			int num2 = 0;
			ik.solver.IKPositionWeight = float.Parse(list[num2++]);
			ik.solver.iterations = int.Parse(list[num2++]);
			num2 = 0;
			foreach (List<string> item in dataList.Skip(num))
			{
				if (ik.solver.effectors.Length <= num2)
				{
					break;
				}
				num2++;
				int num3 = 0;
				IKEffector eff = ik.solver.GetEffector(Illusion.Utils.Enum<FullBodyBipedEffector>.Cast(item[num3++]));
				if (eff == null)
				{
					continue;
				}
				eff.positionWeight = float.Parse(item[num3++]);
				eff.rotationWeight = float.Parse(item[num3++]);
				string findFrame2 = item[num3++];
				if (findFrame2 == "null")
				{
					eff.target = null;
				}
				else
				{
					componentsInChildren.FirstOrDefault((Transform p) => p.name == findFrame2).SafeProc(delegate(Transform frame)
					{
						eff.target = frame;
					});
				}
				if (eff.target != null)
				{
					eff.target.localPosition = item[num3++].GetVector3();
					eff.target.localEulerAngles = item[num3++].GetVector3();
				}
			}
			num += num2;
			num2 = 0;
			foreach (List<string> item2 in dataList.Skip(num))
			{
				if (ik.solver.chain.Length <= num2)
				{
					break;
				}
				FBIKChain fBIKChain = ik.solver.chain[num2++];
				int num4 = 0;
				fBIKChain.pull = float.Parse(item2[num4++]);
				fBIKChain.reach = float.Parse(item2[num4++]);
				fBIKChain.push = float.Parse(item2[num4++]);
				fBIKChain.pushParent = float.Parse(item2[num4++]);
				fBIKChain.reachSmoothing = Illusion.Utils.Enum<FBIKChain.Smoothing>.Cast(item2[num4++]);
				fBIKChain.pushSmoothing = Illusion.Utils.Enum<FBIKChain.Smoothing>.Cast(item2[num4++]);
				fBIKChain.bendConstraint.weight = float.Parse(item2[num4++]);
				string findFrame = item2[num4++];
				if (findFrame == "null")
				{
					fBIKChain.bendConstraint.bendGoal = null;
				}
				else
				{
					fBIKChain.bendConstraint.bendGoal = componentsInChildren.FirstOrDefault((Transform p) => p.name == findFrame);
				}
				if (fBIKChain.bendConstraint.bendGoal != null)
				{
					fBIKChain.bendConstraint.bendGoal.localPosition = item2[num4++].GetVector3();
					fBIKChain.bendConstraint.bendGoal.localEulerAngles = item2[num4++].GetVector3();
				}
			}
		}
	}

	public static class Scene
	{
		public static void GameEnd(bool isCheck = true)
		{
			ExitDialog.GameEnd(isCheck);
		}

		public static void ReturnTitleForget(bool skipCheck = false, bool isForce = false)
		{
			ReturnTitle(skipCheck, isForce).Forget();
		}

		public static async UniTask<bool> ReturnTitle(bool skipCheck = false, bool isForce = false)
		{
			string sceneName = "Title";
			bool isBaseTitle = Manager.Scene.LoadSceneName == sceneName;
			if (isBaseTitle)
			{
				skipCheck = true;
			}
			bool? answer = null;
			if (!skipCheck || isForce)
			{
				ConfirmDialog.Status status = ConfirmDialog.status;
				status.Title = "タイトルへの切替確認";
				status.Sentence = "タイトルへ戻りますか？";
				status.Yes = delegate
				{
					answer = true;
				};
				status.No = delegate
				{
					answer = false;
				};
				ConfirmDialog.Load();
				await UniTask.WaitUntil(() => answer.HasValue);
			}
			else
			{
				answer = true;
			}
			bool num = answer ?? false;
			if (num)
			{
				gotoTitle().Forget();
			}
			return num;
			async UniTaskVoid gotoTitle()
			{
				if (isBaseTitle)
				{
					await Manager.Scene.UnloadAddSceneAsync(null);
				}
				else
				{
					Manager.Scene.isReturnTitle = true;
					await Manager.Scene.UnloadByOrderSceneAsync(sceneName, async delegate(bool isFind)
					{
						if (!isFind)
						{
							await Manager.Scene.LoadReserveAsync(new Manager.Scene.Data
							{
								levelName = sceneName,
								isFade = true
							}, isLoadingImageDraw: true);
						}
					});
				}
			}
		}
	}

	public static class UniRx
	{
		public static class FPSCounter
		{
			private const int BufferSize = 5;

			public static IReadOnlyReactiveProperty<float> Current { get; private set; }

			static FPSCounter()
			{
				Current = (from x in (from _ in Observable.EveryUpdate()
						select Time.deltaTime).Buffer(5, 1)
					select 1f / x.Average()).ToReadOnlyReactiveProperty();
			}
		}

		public static void FixPerspectiveObject<T>(T o, Camera camera) where T : UnityEngine.Component
		{
			Transform transform = o.transform;
			Func<float> distance = () => (transform.position - camera.transform.position).magnitude;
			Vector3 baseScale = transform.localScale / distance();
			(from _ in o.UpdateAsObservable().TakeWhile((Unit _) => camera != null)
				select baseScale * distance()).Subscribe(delegate(Vector3 scale)
			{
				transform.localScale = scale;
			});
		}
	}

	public static class Bundle
	{
		public static void LoadSprite(string assetBundleName, string assetName, Image image, bool isTexSize, string spAnimeName = null)
		{
			AssetBundleLoadAssetOperation assetBundleLoadAssetOperation = AssetBundleManager.LoadAsset(assetBundleName, assetName, typeof(Sprite));
			Sprite sprite = assetBundleLoadAssetOperation.GetAsset<Sprite>();
			if (sprite == null)
			{
				Texture2D asset = assetBundleLoadAssetOperation.GetAsset<Texture2D>();
				sprite = Sprite.Create(asset, new Rect(0f, 0f, asset.width, asset.height), Vector2.zero);
			}
			image.sprite = sprite;
			RectTransform rectTransform = image.rectTransform;
			Vector2 vector = (isTexSize ? new Vector2(sprite.rect.width, sprite.rect.height) : rectTransform.sizeDelta);
			if (!spAnimeName.IsNullOrEmpty())
			{
				Animator component = image.GetComponent<Animator>();
				component.enabled = true;
				component.runtimeAnimatorController = AssetBundleManager.LoadAsset(assetBundleName, spAnimeName, typeof(RuntimeAnimatorController)).GetAsset<RuntimeAnimatorController>();
				Func<float, float, float> obj = (float x, float y) => x / y;
				Func<float, float, bool> func = (float a, float b) => a > b && Mathf.FloorToInt(a - 1f) > 0;
				float num = obj(vector.x, vector.y);
				float num2 = obj(vector.y, vector.x);
				if (func(num, num2))
				{
					rectTransform.sizeDelta = new Vector2(vector.y, vector.y);
				}
				else if (func(num2, num))
				{
					rectTransform.sizeDelta = new Vector2(vector.x, vector.x);
				}
				else
				{
					rectTransform.sizeDelta = new Vector2(vector.x, vector.y);
				}
				AssetBundleManager.UnloadAssetBundle(assetBundleName, isUnloadForceRefCount: false);
			}
			else
			{
				rectTransform.sizeDelta = new Vector2(vector.x, vector.y);
			}
			AssetBundleManager.UnloadAssetBundle(assetBundleName, isUnloadForceRefCount: false);
		}
	}

	public static class Layout
	{
		public class EnabledScope : GUI.Scope
		{
			private readonly bool enabled;

			public EnabledScope()
			{
				enabled = GUI.enabled;
			}

			public EnabledScope(bool enabled)
			{
				this.enabled = GUI.enabled;
				GUI.enabled = enabled;
			}

			protected override void CloseScope()
			{
				GUI.enabled = enabled;
			}
		}

		public class ColorScope : GUI.Scope
		{
			private readonly Color[] colors;

			public ColorScope()
			{
				colors = new Color[3]
				{
					GUI.color,
					GUI.backgroundColor,
					GUI.contentColor
				};
			}

			public ColorScope(params Color[] colors)
			{
				this.colors = new Color[3]
				{
					GUI.color,
					GUI.backgroundColor,
					GUI.contentColor
				};
				foreach (var item in colors.Take(this.colors.Length).Select((Color color, int index) => new { color, index }))
				{
					switch (item.index)
					{
					case 0:
						GUI.color = item.color;
						break;
					case 1:
						GUI.backgroundColor = item.color;
						break;
					case 2:
						GUI.contentColor = item.color;
						break;
					}
				}
			}

			public ColorScope(Colors colors)
			{
				this.colors = new Color[3]
				{
					GUI.color,
					GUI.backgroundColor,
					GUI.contentColor
				};
				if (colors.color.HasValue)
				{
					GUI.color = colors.color.Value;
				}
				if (colors.backgroundColor.HasValue)
				{
					GUI.backgroundColor = colors.backgroundColor.Value;
				}
				if (colors.contentColor.HasValue)
				{
					GUI.contentColor = colors.contentColor.Value;
				}
			}

			protected override void CloseScope()
			{
				int num = 0;
				GUI.color = colors[num++];
				GUI.backgroundColor = colors[num++];
				GUI.contentColor = colors[num++];
			}
		}
	}

	public static class ScreenShot
	{
		public static string Path
		{
			get
			{
				StringBuilder stringBuilder = new StringBuilder(256);
				stringBuilder.Append(UserData.Create("cap"));
				DateTime now = DateTime.Now;
				stringBuilder.Append(now.Year.ToString("0000"));
				stringBuilder.Append(now.Month.ToString("00"));
				stringBuilder.Append(now.Day.ToString("00"));
				stringBuilder.Append(now.Hour.ToString("00"));
				stringBuilder.Append(now.Minute.ToString("00"));
				stringBuilder.Append(now.Second.ToString("00"));
				stringBuilder.Append(now.Millisecond.ToString("000"));
				stringBuilder.Append(".png");
				return stringBuilder.ToString();
			}
		}

		public static IEnumerator CaptureGSS(List<ScreenShotCamera> ssCamList, string path, Texture capMark, int capRate = 1)
		{
			if (ssCamList.IsNullOrEmpty() || path.IsNullOrEmpty())
			{
				yield break;
			}
			yield return new WaitForEndOfFrame();
			Capture(delegate(RenderTexture rt)
			{
				Graphics.Blit(ssCamList[0].renderTexture, rt);
				foreach (ScreenShotCamera item in ssCamList.Skip(1))
				{
					Graphics.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), item.renderTexture);
				}
				if (capMark != null)
				{
					DrawCapMark(capMark, null);
				}
			}, path, capRate);
			yield return null;
		}

		public static IEnumerator CaptureCameras(List<Camera> cameraList, string path, Texture capMark, int capRate = 1)
		{
			yield return new WaitForEndOfFrame();
			Capture(delegate(RenderTexture rt)
			{
				Graphics.SetRenderTarget(rt);
				GL.Clear(clearDepth: true, clearColor: true, Color.black);
				Graphics.SetRenderTarget(null);
				foreach (Camera item in cameraList.Where((Camera p) => p != null))
				{
					bool enabled = item.enabled;
					RenderTexture targetTexture = item.targetTexture;
					Rect rect = item.rect;
					item.enabled = true;
					item.targetTexture = rt;
					item.Render();
					item.targetTexture = targetTexture;
					item.rect = rect;
					item.enabled = enabled;
				}
				if (capMark != null)
				{
					Graphics.SetRenderTarget(rt);
					DrawCapMark(capMark, null);
					Graphics.SetRenderTarget(null);
				}
			}, path, capRate);
			yield return null;
		}

		public static void Capture(Action<RenderTexture> proc, string path, int capRate = 1)
		{
			int num = ((capRate == 0) ? 1 : capRate);
			Texture2D texture2D = new Texture2D(Screen.width * num, Screen.height * num, TextureFormat.RGB24, mipChain: false);
			RenderTexture temporary = RenderTexture.GetTemporary(texture2D.width, texture2D.height, 24, RenderTextureFormat.Default, RenderTextureReadWrite.Default, (QualitySettings.antiAliasing == 0) ? 1 : QualitySettings.antiAliasing);
			proc(temporary);
			RenderTexture.active = temporary;
			texture2D.ReadPixels(new Rect(0f, 0f, texture2D.width, texture2D.height), 0, 0);
			texture2D.Apply();
			RenderTexture.active = null;
			byte[] bytes = texture2D.EncodeToPNG();
			RenderTexture.ReleaseTemporary(temporary);
			UnityEngine.Object.Destroy(texture2D);
			texture2D = null;
			File.WriteAllBytes(path, bytes);
		}

		public static void DrawCapMark(Texture tex, Vector2? pos)
		{
			float num = (float)Screen.width / 1280f;
			if (!pos.HasValue)
			{
				pos = new Vector2(1152f, 688f);
			}
			Graphics.DrawTexture(new Rect(pos.Value.x * num, pos.Value.y * num, (float)tex.width * num, (float)tex.height * num), tex);
		}
	}

	public static class Sound
	{
		private class SystemSEComparer : IEqualityComparer<SystemSE>
		{
			public bool Equals(SystemSE x, SystemSE y)
			{
				return x == y;
			}

			public int GetHashCode(SystemSE obj)
			{
				return (int)obj;
			}
		}

		private class BGMComparer : IEqualityComparer<BGM>
		{
			public bool Equals(BGM x, BGM y)
			{
				return x == y;
			}

			public int GetHashCode(BGM obj)
			{
				return (int)obj;
			}
		}

		public class SettingBGM : Setting
		{
			public override string bundle
			{
				get
				{
					return base.bundle;
				}
				set
				{
					base.bundle = value;
					base.asset = Path.GetFileNameWithoutExtension(value);
				}
			}

			public SettingBGM()
			{
				Initialize();
			}

			public SettingBGM(int bgmNo)
			{
				Setting(Convert((BGM)bgmNo));
			}

			public SettingBGM(BGM bgm, float fadeTime = 0.8f)
			{
				Setting(Convert(bgm), fadeTime);
			}

			public SettingBGM(string bundle)
			{
				Setting(bundle);
			}

			private void Setting(string bundle, float fadeTime = 0.8f)
			{
				this.bundle = bundle;
				Initialize(fadeTime);
			}

			public static string Convert(BGM bgmNo)
			{
				return "sound/data/bgm/" + Cast(bgmNo) + AssetBundleManager.Extension;
			}

			private void Initialize(float fadeTime = 0.8f)
			{
				base.loader.type = Manager.Sound.Type.BGM;
				base.loader.fadeTime = fadeTime;
				base.loader.isAssetEqualPlay = false;
			}
		}

		public class Setting
		{
			public Manager.Sound.Loader loader => _loader;

			private Manager.Sound.Loader _loader { get; } = new Manager.Sound.Loader();


			public virtual string bundle
			{
				get
				{
					return _loader.bundle;
				}
				set
				{
					_loader.bundle = value;
				}
			}

			public virtual string asset
			{
				get
				{
					return _loader.asset;
				}
				set
				{
					_loader.asset = value;
				}
			}

			public Setting()
			{
			}

			public Setting(Manager.Sound.Type type)
			{
				_loader.type = type;
			}

			public Setting(SystemSE se)
				: this(Manager.Sound.Type.SystemSE)
			{
				asset = Cast(se);
			}
		}

		private static Dictionary<int, FootStepSE> _footStepAreaTypes = new Dictionary<int, FootStepSE>();

		private static Dictionary<int, FootStepSE> _footStepDefaultData = null;

		private static string _footstepAssetBundleCommonPath = "action/list/sound/se/footstep/";

		private static string _footstepAsset = "mapdef";

		private static bool _initializedSoundInfo = false;

		private static IReadOnlyDictionary<SystemSE, string> SystemSEAssetTable { get; } = new Dictionary<SystemSE, string>(new SystemSEComparer())
		{
			{
				SystemSE.get_point,
				"se_ks_system_000"
			},
			{
				SystemSE.register,
				"se_ks_system_001"
			},
			{
				SystemSE.result_gauge,
				"se_ks_system_002"
			},
			{
				SystemSE.result_end,
				"se_ks_system_003"
			},
			{
				SystemSE.change_time,
				"se_ks_system_004"
			},
			{
				SystemSE.ok_s,
				"se_ks_system_005_A"
			},
			{
				SystemSE.ok_l,
				"se_ks_system_005_B"
			},
			{
				SystemSE.cancel,
				"se_ks_system_006"
			},
			{
				SystemSE.change_prev,
				"se_ks_system_007"
			},
			{
				SystemSE.sel,
				"se_ks_system_008"
			},
			{
				SystemSE.photo,
				"se_ks_system_009"
			},
			{
				SystemSE.minmap_o,
				"se_ks_system_010"
			},
			{
				SystemSE.not_push,
				"se_ks_system_011"
			},
			{
				SystemSE.window_o,
				"se_ks_system_012"
			},
			{
				SystemSE.save,
				"se_ks_system_013"
			},
			{
				SystemSE.load,
				"se_ks_system_014"
			},
			{
				SystemSE.shop_buy_fail,
				"se_ks_system_015"
			},
			{
				SystemSE.moneybox_throw,
				"se_ks_system_016"
			},
			{
				SystemSE.get_point_add,
				"se_ks_system_018"
			},
			{
				SystemSE.lovers_call,
				"se_ks_system_019"
			},
			{
				SystemSE.lovers_call_fail,
				"se_ks_system_020"
			},
			{
				SystemSE.menu_open,
				"se_ks_system_021"
			},
			{
				SystemSE.menu_close,
				"se_ks_system_022"
			},
			{
				SystemSE.map_warp,
				"se_ks_system_023"
			},
			{
				SystemSE.get_topic,
				"se_ks_system_024"
			},
			{
				SystemSE.change_day_time,
				"se_ks_system_025"
			},
			{
				SystemSE.delete,
				"se_ks_system_026"
			}
		};


		private static IReadOnlyDictionary<BGM, string> BGMAssetTable { get; } = new Dictionary<BGM, string>(new BGMComparer())
		{
			{
				BGM.Title,
				"kks_song_00"
			},
			{
				BGM.Custom,
				"kks_bgm_18"
			},
			{
				BGM.Communication,
				"kks_bgm_07"
			},
			{
				BGM.Encounter,
				"kks_bgm_06"
			},
			{
				BGM.Lover,
				"kks_bgm_08"
			},
			{
				BGM.HScene,
				"kks_bgm_02"
			},
			{
				BGM.HScenePeep,
				"kks_bgm_11"
			},
			{
				BGM.Sad,
				"kks_bgm_17"
			},
			{
				BGM.Reminiscence,
				"kks_bgm_13"
			},
			{
				BGM.MorningMenu,
				"kks_bgm_12"
			},
			{
				BGM.SystemMenu,
				"kks_bgm_12"
			},
			{
				BGM.MapMoveMorning,
				"kks_bgm_00"
			},
			{
				BGM.MapMoveNoon,
				"kks_bgm_04"
			},
			{
				BGM.MapMoveEvening,
				"kks_bgm_05"
			},
			{
				BGM.MapMoveNight,
				"kks_bgm_15"
			},
			{
				BGM.Follow,
				"kks_bgm_09"
			},
			{
				BGM.InfomationCenter,
				"kks_bgm_10"
			},
			{
				BGM.SecretBeach,
				"kks_bgm_14"
			},
			{
				BGM.FemaleOnly,
				"kks_bgm_11"
			},
			{
				BGM.Romantic,
				"kks_bgm_16"
			},
			{
				BGM.Dark,
				"kks_bgm_18"
			},
			{
				BGM.FolkDance,
				"kks_bgm_19"
			},
			{
				BGM.Esthetic,
				"kks_bgm_03"
			},
			{
				BGM.ShopDaytime,
				"kks_bgm_01"
			},
			{
				BGM.ShopNight,
				"kks_bgm_03"
			},
			{
				BGM.Hukuya,
				"kks_bgm_01"
			},
			{
				BGM.Omiyageya,
				"kks_bgm_01"
			},
			{
				BGM.Hotel,
				"kks_bgm_01"
			},
			{
				BGM.Minshuku,
				"kks_bgm_01"
			},
			{
				BGM.Gasshukujo,
				"kks_bgm_01"
			},
			{
				BGM.Title2,
				"kks_bgm_20"
			},
			{
				BGM.Homepage,
				"kks_bgm_21"
			},
			{
				BGM.SuiteRoom,
				"kks_bgm_22"
			},
			{
				BGM.SuiteRoomDate,
				"kks_bgm_23"
			},
			{
				BGM.Custom2,
				"kks_bgm_24"
			}
		};


		private static IReadOnlyDictionary<SystemSE, string> seBundleTable { get; set; } = null;


		public static FootStepSE FootStepAreaDefaultType { get; set; }

		public static Dictionary<int, FootStepSE> FootStepAreaTypes => _footStepAreaTypes;

		public static Action<int, Transform> FootStepPlayCall { get; set; }

		public static Dictionary<int, FootStepSE> FootStepDefaultData
		{
			get
			{
				if (_footStepDefaultData == null)
				{
					LoadFootDef();
				}
				return _footStepDefaultData;
			}
		}

		public static Func<Transform, int, bool, Threshold, AudioSource> SEPlayCall { get; set; }

		public static Action<AudioSource> SEStopCall { get; set; }

		public static Dictionary<int, BGMArea.PlayInfo> MapBGMTable { get; set; }

		public static Dictionary<string, GameObject> EnvAreaTable { get; set; }

		public static Dictionary<int, AudioClip> EnvClipTable { get; set; }

		public static Dictionary<int, AudioClip> SEClipTable { get; set; }

		public static Dictionary<string, float> SEClipVolum { get; set; }

		private static string Cast(SystemSE se)
		{
			if (!SystemSEAssetTable.TryGetValue(se, out var value))
			{
				return string.Empty;
			}
			return value;
		}

		private static string Cast(BGM bgm)
		{
			if (!BGMAssetTable.TryGetValue(bgm, out var value))
			{
				return string.Empty;
			}
			return value;
		}

		public static string GetBGMName(BGM bgm)
		{
			return Cast(bgm);
		}

		public static Sound_Component GetBGM()
		{
			if (Manager.Sound.currentBGM != null)
			{
				return Manager.Sound.currentBGM.GetComponent<Sound_Component>();
			}
			return null;
		}

		public static AudioSource Get(Manager.Sound.Type type, AssetBundleData data)
		{
			return Manager.Sound.CreateCache(type, data);
		}

		public static AudioSource Get(Manager.Sound.Type type, AssetBundleManifestData data)
		{
			return Manager.Sound.CreateCache(type, data);
		}

		public static AudioSource Get(Manager.Sound.Type type, string bundle, string asset, string manifest = null)
		{
			return Manager.Sound.CreateCache(type, bundle, asset, manifest);
		}

		public static void Remove(Manager.Sound.Type type, string bundle, string asset, string manifest = null)
		{
			Manager.Sound.ReleaseCache(type, bundle, asset, manifest);
		}

		public static AudioSource Get(SystemSE se)
		{
			return Get(Manager.Sound.Type.SystemSE, GetSeBundlePath(se), Cast(se));
		}

		public static void Remove(SystemSE se)
		{
			Remove(Manager.Sound.Type.SystemSE, GetSeBundlePath(se), Cast(se));
		}

		public static AudioSource Play(SystemSE se)
		{
			AudioSource audioSource = Get(se);
			if (audioSource != null)
			{
				audioSource.Play();
			}
			return audioSource;
		}

		public static AudioSource Play3D(Vector3 position, AudioClip clip, float fadeTime = 0f)
		{
			AudioSource audioSource = Play(Manager.Sound.Type.GameSE3D, clip, fadeTime);
			if (audioSource != null)
			{
				audioSource.transform.position = position;
			}
			return audioSource;
		}

		public static AudioSource Play(Manager.Sound.Type type, AudioClip clip, float fadeTime = 0f)
		{
			AudioSource audio = Manager.Sound.Play(type, clip, fadeTime);
			if (audio == null)
			{
				return null;
			}
			(from __ in audio.UpdateAsObservable()
				where !audio.isPlaying
				select __).Take(1).Subscribe(delegate
			{
				UnityEngine.Object.Destroy(audio.gameObject);
			});
			return audio;
		}

		private static string GetSeBundlePath(SystemSE se)
		{
			LoadBundleCastTable();
			if (!seBundleTable.TryGetValue(se, out var value))
			{
				return string.Empty;
			}
			return value;
		}

		private static void LoadBundleCastTable()
		{
			if (seBundleTable != null)
			{
				return;
			}
			Dictionary<SystemSE, string> dictionary = new Dictionary<SystemSE, string>();
			foreach (string item in from s in CommonLib.GetAssetBundleNameListFromPathExternal("sound/data/systemse/", subdirCheck: true)
				orderby s
				select s)
			{
				AudioClip[] allAssets = AssetBundleManager.LoadAllAsset(item, typeof(AudioClip)).GetAllAssets<AudioClip>();
				foreach (AudioClip audioClip in allAssets)
				{
					foreach (KeyValuePair<SystemSE, string> item2 in SystemSEAssetTable)
					{
						if (item2.Value == audioClip.name)
						{
							dictionary[item2.Key] = item;
						}
					}
					Resources.UnloadAsset(audioClip);
				}
				AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: false);
			}
			seBundleTable = dictionary;
		}

		public static async UniTask GetBGMandVolume(Action<(string, float)> bgmAndVolume)
		{
			(string, float) obj = (string.Empty, 1f);
			Sound_Component now = GetBGM();
			if (now != null)
			{
				AudioSource audioSource = now.GetComponent<AudioSource>();
				FadePlayer fadePlayer = audioSource.GetComponent<FadePlayer>();
				await UniTask.WaitWhile(() => fadePlayer != null && !(fadePlayer.nowState is FadePlayer.Playing));
				obj.Item1 = now.bundle;
				obj.Item2 = audioSource.volume;
			}
			bgmAndVolume?.Invoke(obj);
		}

		public static async UniTask GetFadePlayerWhileNull(string bgm, float volume)
		{
			SettingBGM s = new SettingBGM(bgm);
			AudioSource source = Play(s);
			FadePlayer player = null;
			if (source != null)
			{
				await UniTask.WaitWhile(() => (player = source.GetComponent<FadePlayer>()) == null);
			}
			if (player != null)
			{
				player.currentVolume = volume;
			}
			else
			{
				if (!(Manager.Sound.currentBGM != null))
				{
					return;
				}
				player = Manager.Sound.currentBGM.GetComponent<FadePlayer>();
				if (player != null)
				{
					player.currentVolume = volume;
					return;
				}
				AudioSource component = Manager.Sound.currentBGM.GetComponent<AudioSource>();
				if (component != null)
				{
					component.volume = volume;
				}
			}
		}

		public static void InitMapSoundInfo()
		{
			if (!_initializedSoundInfo)
			{
				InitMapBGM();
				InitMapEnv();
				InitEnvClipTable();
				InitSEClipTable();
				_initializedSoundInfo = true;
			}
		}

		private static void LoadFootDef()
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(_footstepAssetBundleCommonPath);
			assetBundleNameListFromPath.Sort();
			_footStepDefaultData = new Dictionary<int, FootStepSE>();
			foreach (string item in assetBundleNameListFromPath)
			{
				FootSEMapDefData footSEMapDefData = CommonLib.LoadAsset<FootSEMapDefData>(item, _footstepAsset);
				AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: true);
				if (footSEMapDefData == null)
				{
					continue;
				}
				foreach (FootSEMapDefData.Param item2 in footSEMapDefData.param)
				{
					if (!_footStepDefaultData.ContainsKey(item2.id))
					{
						_footStepDefaultData.Add(item2.id, FootStepSE.DefGround);
					}
					_footStepDefaultData[item2.id] = item2.kind;
				}
			}
		}

		private static void InitMapBGM()
		{
			MapBGMTable = new Dictionary<int, BGMArea.PlayInfo>();
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath("action/list/sound/bgm/");
			assetBundleNameListFromPath.Sort();
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(text);
				if (!AssetBundleCheck.IsFile(text, fileNameWithoutExtension))
				{
					continue;
				}
				AssetBundleLoadAssetOperation assetBundleLoadAssetOperation = AssetBundleManager.LoadAsset(assetBundleNameListFromPath[i], fileNameWithoutExtension, typeof(ExcelData));
				if (assetBundleLoadAssetOperation == null)
				{
					continue;
				}
				ExcelData asset = assetBundleLoadAssetOperation.GetAsset<ExcelData>();
				if (asset == null)
				{
					continue;
				}
				int maxCell = asset.MaxCell;
				for (int j = 1; j < maxCell; j++)
				{
					List<string> row = asset.GetRow(j);
					int num = 0;
					if (int.TryParse(row.ElementAtOrDefault(num++), out var result))
					{
						row.ElementAtOrDefault(num++);
						if (int.TryParse(row.ElementAtOrDefault(num++), out var result2) && bool.TryParse(row.ElementAtOrDefault(num++), out var result3) && float.TryParse(row.ElementAtOrDefault(num++), out var result4))
						{
							BGMArea.PlayInfo value = new BGMArea.PlayInfo
							{
								BGMID = result2,
								EnableVolumeModification = result3,
								Volume = result4
							};
							MapBGMTable[result] = value;
						}
					}
				}
			}
		}

		private static void InitMapEnv()
		{
			EnvAreaTable = new Dictionary<string, GameObject>();
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath("map/sound/");
			assetBundleNameListFromPath.Sort();
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string assetBundleName = assetBundleNameListFromPath[i];
				if (!AssetBundleCheck.IsFile(assetBundleName))
				{
					continue;
				}
				AssetBundleLoadAssetOperation assetBundleLoadAssetOperation = AssetBundleManager.LoadAllAsset(assetBundleName, typeof(GameObject));
				if (assetBundleLoadAssetOperation == null)
				{
					continue;
				}
				GameObject[] allAssets = assetBundleLoadAssetOperation.GetAllAssets<GameObject>();
				if (((IReadOnlyCollection<GameObject>)(object)allAssets).IsNullOrEmpty())
				{
					continue;
				}
				GameObject[] array = allAssets;
				foreach (GameObject gameObject in array)
				{
					EnvArea3D component = gameObject.GetComponent<EnvArea3D>();
					EnvLineArea3D component2 = gameObject.GetComponent<EnvLineArea3D>();
					if (!(component == null) || !(component2 == null))
					{
						EnvAreaTable[gameObject.name] = gameObject;
					}
				}
			}
		}

		private static void InitEnvClipTable()
		{
			EnvClipTable = new Dictionary<int, AudioClip>();
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath("action/list/sound/se/env/");
			assetBundleNameListFromPath.Sort();
			string text = "envlist";
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string assetBundleName = assetBundleNameListFromPath[i];
				if (!AssetBundleCheck.IsFile(assetBundleName, text))
				{
					continue;
				}
				EnvSEData envSEData = CommonLib.LoadAsset<EnvSEData>(assetBundleName, text);
				AssetBundleManager.UnloadAssetBundle(assetBundleName, isUnloadForceRefCount: true);
				if (envSEData == null)
				{
					continue;
				}
				foreach (EnvSEData.ClipData clipData in envSEData.param.clipDatas)
				{
					AudioClip asset = new AssetBundleData(clipData.AssetBundleName, clipData.AssetName).GetAsset<AudioClip>();
					EnvClipTable[clipData.ID] = asset;
				}
			}
		}

		private static void InitSEClipTable()
		{
			SEClipTable = new Dictionary<int, AudioClip>();
			SEClipVolum = new Dictionary<string, float>();
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath("action/list/sound/se/action/");
			assetBundleNameListFromPath.Sort();
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(text);
				if (!AssetBundleCheck.IsFile(text, fileNameWithoutExtension))
				{
					continue;
				}
				AssetBundleLoadAssetOperation assetBundleLoadAssetOperation = AssetBundleManager.LoadAsset(text, fileNameWithoutExtension, typeof(ExcelData));
				if (assetBundleLoadAssetOperation.IsEmpty())
				{
					AssetBundleManager.UnloadAssetBundle(text, isUnloadForceRefCount: true);
					continue;
				}
				ExcelData asset = assetBundleLoadAssetOperation.GetAsset<ExcelData>();
				if (asset == null)
				{
					continue;
				}
				int count = asset.list.Count;
				for (int j = 1; j < count; j++)
				{
					List<string> row = asset.GetRow(j);
					int num = 0;
					if (int.TryParse(row.ElementAtOrDefault(num++), out var result))
					{
						row.ElementAtOrDefault(num++);
						string bundle = row.ElementAtOrDefault(num++);
						string asset2 = row.ElementAtOrDefault(num++);
						row.ElementAtOrDefault(num++);
						if (!float.TryParse(row.ElementAtOrDefault(num++), out var result2))
						{
							result2 = 1f;
						}
						AudioClip asset3 = new AssetBundleData(bundle, asset2).GetAsset<AudioClip>();
						SEClipTable[result] = asset3;
						if (asset3 != null)
						{
							SEClipVolum[asset3.name] = result2;
						}
					}
				}
			}
		}

		public static bool isPlay(SystemSE se)
		{
			return Manager.Sound.IsPlay(Manager.Sound.Type.SystemSE, Cast(se));
		}

		public static AudioSource Play(Setting s)
		{
			return Manager.Sound.Play(s.loader);
		}

		public static void Play(Setting s, Action<AudioSource> action)
		{
			Manager.Sound.Play(s.loader, action);
		}
	}

	public static class UI
	{
		public class PlaySE
		{
			public bool use { private get; set; }

			public PlaySE()
				: this(use: true)
			{
			}

			public PlaySE(bool use)
			{
				this.use = use;
			}

			public void Play(SystemSE se)
			{
				if (use)
				{
					Sound.Play(se);
				}
			}
		}

		public static class TagSelection
		{
			public interface ICursorTagElement
			{
				Image cursor { get; }

				Selectable selectable { get; }
			}

			public static IDisposable BindToGroup(Action<int> action, params Toggle[] toggles)
			{
				return (from list in toggles.Select((Toggle toggle) => toggle.OnValueChangedAsObservable()).CombineLatest()
					select list.IndexOf(item: true) into sel
					where sel >= 0
					select sel).Subscribe(delegate(int sel)
				{
					action?.Invoke(sel);
				});
			}

			public static CompositeDisposable BindToEnter(bool isExit, Image cursor, params Selectable[] selectables)
			{
				CompositeDisposable compositeDisposable = new CompositeDisposable();
				foreach (Selectable item in selectables)
				{
					(from _ in item.OnPointerEnterAsObservable().TakeUntilDestroy(cursor)
						where item.IsInteractable()
						select _).Subscribe(delegate
					{
						cursor.enabled = true;
						CursorFrame.Set(cursor.rectTransform, item.GetComponent<RectTransform>());
					}).AddTo(compositeDisposable);
					if (isExit)
					{
						item.OnPointerExitAsObservable().TakeUntilDestroy(cursor).Subscribe(delegate
						{
							cursor.enabled = false;
						})
							.AddTo(compositeDisposable);
					}
				}
				return compositeDisposable;
			}

			public static CompositeDisposable BindToEnter(bool isExit, params ICursorTagElement[] elements)
			{
				CompositeDisposable compositeDisposable = new CompositeDisposable();
				ICursorTagElement[] array = elements;
				foreach (ICursorTagElement item in array)
				{
					CursorFrame.Set(item.cursor.rectTransform, item.selectable.GetComponent<RectTransform>());
					(from _ in item.selectable.OnPointerEnterAsObservable().TakeUntilDestroy(item.cursor)
						select item.cursor).Subscribe(delegate(Image cursor)
					{
						cursor.enabled = true;
						if (!isExit)
						{
							foreach (ICursorTagElement item2 in elements.Where((ICursorTagElement x) => x.cursor != cursor))
							{
								item2.cursor.enabled = false;
							}
						}
					}).AddTo(compositeDisposable);
					if (isExit)
					{
						item.selectable.OnPointerExitAsObservable().TakeUntilDestroy(item.cursor).Subscribe(delegate
						{
							item.cursor.enabled = false;
						})
							.AddTo(compositeDisposable);
					}
				}
				return compositeDisposable;
			}
		}

		public static class CursorFrame
		{
			private static RectTransform.Axis[] _axes;

			private static RectTransform.Axis[] axes => _axes ?? (_axes = Enum.GetValues(typeof(RectTransform.Axis)).Cast<RectTransform.Axis>().ToArray());

			public static void Set(RectTransform cursor, RectTransform target, RectTransform size = null)
			{
				cursor.position = target.position;
				if (size == null)
				{
					size = target;
				}
				SetSize(cursor, size);
			}

			public static void SetSize(RectTransform cursor, RectTransform size)
			{
				for (int i = 0; i < axes.Length; i++)
				{
					cursor.SetSizeWithCurrentAnchors(axes[i], size.rect.size[i]);
				}
			}

			public static void Set(RectTransform cursor, RectTransform target, ref Vector3 velocity, float? smoothMoveTime = null, float? smoothSizeTime = null)
			{
				if (!smoothMoveTime.HasValue)
				{
					cursor.position = target.position;
				}
				else
				{
					cursor.position = Smooth.Damp(cursor.position, target.position, ref velocity, smoothMoveTime.Value);
				}
				if (smoothSizeTime.HasValue)
				{
					Vector3 velocity2 = velocity;
					SetSize(cursor, target, ref velocity2, smoothSizeTime.Value);
					if (!smoothMoveTime.HasValue)
					{
						velocity = velocity2;
					}
				}
			}

			public static void SetSize(RectTransform cursor, RectTransform target, ref Vector3 velocity, float smoothTime)
			{
				for (int i = 0; i < axes.Length; i++)
				{
					float currentVelocity = velocity[i];
					float size = Smooth.Damp(cursor.rect.size[i], target.rect.size[i], ref currentVelocity, smoothTime);
					velocity[i] = currentVelocity;
					cursor.SetSizeWithCurrentAnchors(axes[i], size);
				}
			}
		}

		public static class Smooth
		{
			public static float Damp(float current, float target, ref float currentVelocity, float smoothTime)
			{
				return Mathf.SmoothDamp(current, target, ref currentVelocity, smoothTime, float.PositiveInfinity, Time.unscaledDeltaTime);
			}

			public static Vector2 Damp(Vector2 current, Vector2 target, ref Vector2 currentVelocity, float smoothTime)
			{
				return Vector2.SmoothDamp(current, target, ref currentVelocity, smoothTime, float.PositiveInfinity, Time.unscaledDeltaTime);
			}

			public static Vector3 Damp(Vector3 current, Vector3 target, ref Vector3 currentVelocity, float smoothTime)
			{
				return Vector3.SmoothDamp(current, target, ref currentVelocity, smoothTime, float.PositiveInfinity, Time.unscaledDeltaTime);
			}
		}
	}

	public static class Voice
	{
		public class Setting
		{
			public string assetBundleName;

			public string assetName;

			public Manager.Voice.Type type;

			public int no;

			public float pitch = 1f;

			public Transform voiceTrans;

			public float delayTime;

			public float fadeTime;

			public bool isAsync = true;

			public int settingNo = -1;

			public bool isPlayEndDelete = true;

			public bool isBundleUnload;

			public bool is2D;
		}

		public static AudioSource Get(int voiceNo, AssetBundleData data)
		{
			if (SingletonInitializer<Manager.Voice>.initialized)
			{
				return Manager.Voice.CreateCache(voiceNo, data);
			}
			return null;
		}

		public static AudioSource Get(int voiceNo, AssetBundleManifestData data)
		{
			if (SingletonInitializer<Manager.Voice>.initialized)
			{
				return Manager.Voice.CreateCache(voiceNo, data);
			}
			return null;
		}

		public static AudioSource Get(int voiceNo, string bundle, string asset, string manifest = null)
		{
			if (SingletonInitializer<Manager.Voice>.initialized)
			{
				return Manager.Voice.CreateCache(voiceNo, bundle, asset, manifest);
			}
			return null;
		}

		public static void Remove(int voiceNo, string bundle, string asset, string manifest = null)
		{
			if (SingletonInitializer<Manager.Voice>.initialized)
			{
				Manager.Voice.ReleaseCache(voiceNo, bundle, asset, manifest);
			}
		}

		public static AudioSource Play(Setting s)
		{
			Manager.Voice.Loader loader = new Manager.Voice.Loader();
			loader.no = s.no;
			loader.bundle = s.assetBundleName;
			loader.asset = s.assetName;
			loader.pitch = s.pitch;
			loader.fadeTime = s.fadeTime;
			loader.settingNo = s.settingNo;
			loader.voiceTrans = s.voiceTrans;
			if (SingletonInitializer<Manager.Voice>.initialized)
			{
				return Manager.Voice.Play(loader);
			}
			return null;
		}

		public static AudioSource OncePlay(Setting s)
		{
			Manager.Voice.Loader loader = new Manager.Voice.Loader();
			loader.no = s.no;
			loader.bundle = s.assetBundleName;
			loader.asset = s.assetName;
			loader.pitch = s.pitch;
			loader.fadeTime = s.fadeTime;
			loader.settingNo = s.settingNo;
			loader.voiceTrans = s.voiceTrans;
			if (SingletonInitializer<Manager.Voice>.initialized)
			{
				return Manager.Voice.OncePlay(loader);
			}
			return null;
		}

		public static AudioSource OncePlayChara(Setting s)
		{
			Manager.Voice.Loader loader = new Manager.Voice.Loader();
			loader.no = s.no;
			loader.bundle = s.assetBundleName;
			loader.asset = s.assetName;
			loader.pitch = s.pitch;
			loader.fadeTime = s.fadeTime;
			loader.settingNo = s.settingNo;
			loader.voiceTrans = s.voiceTrans;
			if (SingletonInitializer<Manager.Voice>.initialized)
			{
				return Manager.Voice.OncePlayChara(loader);
			}
			return null;
		}
	}
}
