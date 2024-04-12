using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using Quintessential;
using Quintessential.Settings;
using SDL2;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;

namespace SigmarSolver;

//using PartType = class_139;
//using Permissions = enum_149;
//using BondType = enum_126;
//using BondSite = class_222;
//using Bond = class_277;
//using AtomTypes = class_175;
//using PartTypes = class_191;
using Texture = class_256;
public class MainClass : QuintessentialMod
{
	public static MethodInfo PrivateMethod<T>(string method) => typeof(T).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
	
	static Texture hintCursor;
	const string HintField = "SigmarSolver:Hint";

	public override Type SettingsType => typeof(MySettings);
	public static QuintessentialMod MainClassAsMod;
	public static bool PressedHintKey() => MySettings.Instance.hintKey.Pressed();
	public class MySettings
	{
		//[SettingsLabel("Boolean Setting")]
		//public bool booleanSetting = true;
		public static MySettings Instance => MainClassAsMod.Settings as MySettings;

		[SettingsLabel("Show a hint for the current solitaire.")]
		public Keybinding hintKey = new() { Key = "D" };
	}
	public override void ApplySettings()
	{
		base.ApplySettings();
		var SET = (MySettings)Settings;
		//var booleanSetting = SET.booleanSetting;
	}
	public override void Load()
	{
		MainClassAsMod = this;
		Settings = new MySettings();
	}
	public override void LoadPuzzleContent()
	{
		hintCursor = class_235.method_615("sigmarSolver/textures/cursor_hint");
	}
	public override void Unload()
	{
		//
	}
	public override void PostLoad()
	{
		On.SolitaireScreen.method_50 += SolitaireScreen_Method_50;
	}

	struct SigmarHint
	{
		public int count = 0;
		public HexIndex[] hints = new HexIndex[2]{ new(), new() };

		public SigmarHint()
		{
			this.count = 0;
		}
		public SigmarHint(HexIndex hint1)
		{
			this.count = 1;
			hints[0] = hint1;
		}
		public SigmarHint(HexIndex hint1, HexIndex hint2)
		{
			this.count = 2;
			hints[0] = hint1;
			hints[1] = hint2;
		}

		public void drawHint()
		{
			Vector2 offset(HexIndex hex) => class_187.field_1743.method_491(hex, new Vector2(687f, 506f) + class_115.field_1433 / 2 - new Vector2(1516f, 922f) / 2) + new Vector2(-2f, -11f);

			for (int i = 0; i < this.count; i++)
			{
				class_135.method_272(hintCursor, offset(this.hints[i]) - hintCursor.field_2056.ToVector2() / 2);
			}
		}
	}


	public static void SolitaireScreen_Method_50(On.SolitaireScreen.orig_method_50 orig, SolitaireScreen screen_self, float timeDelta)
	{
		var screen_dyn = new DynamicData(screen_self);

		if (Input.IsLeftClickPressed() || Input.IsMiddleClickPressed() || Input.IsRightClickPressed() || Input.IsSdlKeyPressed(SDL.enum_160.SDLK_ESCAPE))
		{
			screen_dyn.Set(HintField, new SigmarHint());
		}

		orig(screen_self, timeDelta);

		var data = screen_dyn.Get(HintField);
		SigmarHint sigmarHint = data == null ? new SigmarHint() : (SigmarHint) data;

		bool allowedToStartNewGame = (bool) PrivateMethod<SolitaireScreen>("method_1894").Invoke(screen_self, new object[0]);

		if (PressedHintKey() && allowedToStartNewGame)
		{
			//generate a new hint from the current solitaire game
			SolitaireState solitaireState = (SolitaireState) PrivateMethod<SolitaireScreen>("method_1889").Invoke(screen_self, new object[0]);
			sigmarHint = solveGameState(new DynamicData(solitaireState).Get<SolitaireGameState>("field_3900"));
		}

		screen_dyn.Set(HintField, sigmarHint);

		sigmarHint.drawHint();
	}

	static SigmarHint solveGameState(SolitaireGameState gameState)
	{

		if (gameState.field_3864.Count() == 0) return new SigmarHint();
		if (gameState.field_3864.Count() == 1) return new SigmarHint(gameState.field_3864.First().Key);
		if (gameState.field_3864.Count() >= 2)
		{
			HexIndex a = new();
			HexIndex b = new();
			foreach (var kvp in gameState.field_3864)
			{
				a = b;
				b = kvp.Key;
			}
			return new SigmarHint(a, b);
		}









		return new SigmarHint();
	}
}
