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

	public static bool playInOrder = false;
	static long boardNum = -1;

	public static bool enableSolver1 = true;
	public static bool enableSolver2 = false;
	public static bool enableSolver3 = false;
	public static bool enableSolver4 = false;
	public static bool enableSolver5 = false;
	public static bool enableSolver6 = false;

	public static bool enableDataLogging = false;
	public class MySettings
	{
		//[SettingsLabel("Boolean Setting")]
		//public bool booleanSetting = true;
		public static MySettings Instance => MainClassAsMod.Settings as MySettings;

		[SettingsLabel("Play normal Sigmar's Garden games in-order")]
		public bool playInOrder = false;

		[SettingsLabel("Enable data logging in log.txt")]
		public bool enableDataLogging = false;

		[SettingsLabel("Enable Solver1 (Default solver)")]
		public bool enableSolver1 = true;

		[SettingsLabel("Enable Solver2 (Avoids making moves that are already planned)")]
		public bool enableSolver2 = false;

		[SettingsLabel("Enable Solver3 (Keeps track of unsolvable board-positions)")]
		public bool enableSolver3 = false;

		[SettingsLabel("Enable Solver4 (Generates pairs-to-check in reverse order)")]
		public bool enableSolver4 = false;

		[SettingsLabel("Enable Solver5 (Generates pairs-to-check from the middle outward)")]
		public bool enableSolver5 = false;

		[SettingsLabel("Enable Solver6 (Keeps track of unsolvable move-histories)")]
		public bool enableSolver6 = false;

		[SettingsLabel("Show a hint for the current solitaire")]
		public Keybinding hintKey = new() { Key = "D" };
	}
	public override void ApplySettings()
	{
		base.ApplySettings();
		var SET = (MySettings)Settings;
		if (!playInOrder) boardNum = -1;
		playInOrder = SET.playInOrder;
		enableDataLogging = SET.enableDataLogging;
		enableSolver1 = SET.enableSolver1;
		enableSolver2 = SET.enableSolver2;
		enableSolver3 = SET.enableSolver3;
		enableSolver4 = SET.enableSolver4;
		enableSolver5 = SET.enableSolver5;
		enableSolver6 = SET.enableSolver6;

		if (!enableSolver1 && !enableSolver2 && !enableSolver3 && !enableSolver4 && !enableSolver5 && !enableSolver6)
		{
			SET.enableSolver1 = true;
			enableSolver1 = true;
			base.ApplySettings();
		}
	}
	public override void Load()
	{
		MainClassAsMod = this;
		Settings = new MySettings();
		IL.class_198.method_537 += DecidePlayOrder;
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
	private static void DecidePlayOrder(ILContext il)
	{
		ILCursor cursor = new ILCursor(il);
		// skip ahead to roughly where the "seek to board data" code occurs
		cursor.Goto(18);

		// jump ahead to just after the multiplication and conversion to int64
		if (!cursor.TryGotoNext(MoveType.After, instr => instr.Match(OpCodes.Conv_I8))) return;

		// load the number of boards onto the stack
		cursor.Emit(OpCodes.Ldloc_1);

		// then run the new code
		cursor.EmitDelegate((long seek, int totalBoards) =>
		{
			if (playInOrder)
			{
				boardNum = (boardNum + 1) % totalBoards;
				return boardNum * 3 * 55;
			}
			else
			{
				return seek;
			}
		});
	}

	public class SigmarHint
	{
		static readonly HexIndex NewGameHex = new HexIndex(3, -7);
		static readonly HexIndex ExitHex = new HexIndex(9, 6);
		HexIndex[] hints = new HexIndex[0];

		public bool isEmpty => hints.Length == 0;

		public static SigmarHint NewGame => new SigmarHint(NewGameHex);
		public static SigmarHint Exit => new SigmarHint(ExitHex);
		public static SigmarHint TimeOut => new SigmarHint();
		public SigmarHint()	{ }
		public SigmarHint(HexIndex hint) { this.hints = new HexIndex[1] { hint }; }
		public SigmarHint(HexIndex hint1, HexIndex hint2) { this.hints = new HexIndex[2] { hint1, hint2 }; }
		public SigmarHint(HexIndex[] hints) { this.hints = hints; }

		override public string ToString()
		{
			string str = "(";
			foreach (var hint in this.hints)
			{
				str += " " + hint.Q + ", " + hint.R + ";";
			}
			return str + ")";
		}

		public void drawHint()
		{
			for (int i = 0; i < this.hints.Count(); i++)
			{
				var hex = this.hints[i];
				var offset = class_187.field_1743.method_491(hex, new Vector2(687f, 506f) + class_115.field_1433 / 2 - new Vector2(1516f, 922f) / 2) + new Vector2(-2f, -11f);
				if (hex == ExitHex)
				{
					offset -= new Vector2(53, 9);
				}
				else if (hex == NewGameHex)
				{
					offset += new Vector2(11, 11);
				}
				class_135.method_272(hintCursor, offset - hintCursor.field_2056.ToVector2() / 2);
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

		if ((PressedHintKey()) && allowedToStartNewGame)
		{
			//generate a new hint from the current solitaire game
			SolitaireState solitaireState = (SolitaireState) PrivateMethod<SolitaireScreen>("method_1889").Invoke(screen_self, new object[0]);
			var stateData = new DynamicData(solitaireState).Get<SolitaireGameState>("field_3900");
			var boardDictionary = stateData == null ? new() : stateData.field_3864 ?? new();

			if (enableDataLogging) Logger.Log("");
			if (playInOrder && enableDataLogging) Logger.Log("Board Number: " + (boardNum+1));

			if (boardDictionary.Count() == 0)
			{
				sigmarHint = SigmarHint.NewGame;
				class_238.field_1991.field_1863.method_28(1f); // sim_stop sound
			}
			else
			{
				var tempHint = SigmarHint.TimeOut;
				if (enableSolver1)
				{
					tempHint = new SigmarSolver(boardDictionary).solveGame(1);
					if (!tempHint.isEmpty) sigmarHint = tempHint;
					class_238.field_1991.field_1846.method_28(1f); // glyph_triplex1 sound
				}
				if (enableSolver2)
				{
					tempHint = new SigmarSolver(boardDictionary).solveGame(2);
					if (!tempHint.isEmpty) sigmarHint = tempHint;
					class_238.field_1991.field_1843.method_28(1f); // glyph_duplication sound
				}
				if (enableSolver3)
				{
					tempHint = new SigmarSolver(boardDictionary).solveGame(3);
					if (!tempHint.isEmpty) sigmarHint = tempHint;
					class_238.field_1991.field_1844.method_28(1f); // glyph_projection sound
				}
				if (enableSolver4)
				{
					tempHint = new SigmarSolver(boardDictionary).solveGame(4);
					if (!tempHint.isEmpty) sigmarHint = tempHint;
					class_238.field_1991.field_1847.method_28(1f); // glyph_triplex2 sound
				}
				if (enableSolver5)
				{
					tempHint = new SigmarSolver(boardDictionary).solveGame(5);
					if (!tempHint.isEmpty) sigmarHint = tempHint;
					class_238.field_1991.field_1848.method_28(1f); // glyph_triplex3 sound
				}
				if (enableSolver6)
				{
					tempHint = new SigmarSolver(boardDictionary).solveGame(6);
					if (!tempHint.isEmpty) sigmarHint = tempHint;
					class_238.field_1991.field_1845.method_28(1f); // glyph_purification sound
				}
				if (sigmarHint.isEmpty)
				{
					class_238.field_1991.field_1860.method_28(1f); // sim_error sound
					sigmarHint = new SigmarSolver(boardDictionary).solveGame(0);
					class_238.field_1991.field_1863.method_28(1f); // sim_stop sound
				}
				if (enableDataLogging) Logger.Log("");
			}
		}

		screen_dyn.Set(HintField, sigmarHint);
		sigmarHint.drawHint();
	}
}
