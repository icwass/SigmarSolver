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
				solveGameState(boardDictionary, out sigmarHint, out var finishSound);
			}
		}

		screen_dyn.Set(HintField, sigmarHint);
		sigmarHint.drawHint();
	}

	static void solveGameState(Dictionary<HexIndex, AtomType> boardDictionary, out SigmarHint sigmarHint, out Sound finishSound)
	{
		// throw different solvers at the problem, each with a different timeout
		// timeouts have been tweaked to (try to) minimize the time spent solving known-solvable boards
		finishSound = class_238.field_1991.field_1862; // sim_step sound

		// first, SolverSimple - a basic depth-first search that the other four solvers are based on
		sigmarHint = new SigmarSolver(boardDictionary).solveGame(1);
		if (!sigmarHint.isEmpty) return;

		// SolverNoDoublecover - avoid making moves that are already planned for later
		class_238.field_1991.field_1839.method_28(1f); // glyph_bonding sound
		sigmarHint = new SigmarSolver(boardDictionary).solveGame(2);
		if (!sigmarHint.isEmpty) return;

		// SolverMirror - look at possible move-pairs in the reverse order
		class_238.field_1991.field_1846.method_28(1f); // glyph_triplex1 sound
		sigmarHint = new SigmarSolver(boardDictionary).solveGame(3);
		if (!sigmarHint.isEmpty) return;

		// SolverMemoize - keep track of bad board positions and backtrack immediately when we see them
		class_238.field_1991.field_1842.method_28(1f); // glyph_disposal sound
		sigmarHint = new SigmarSolver(boardDictionary).solveGame(4);
		if (!sigmarHint.isEmpty) return;

		// SolverLastChance - SolverSimple but with no time restraints
		class_238.field_1991.field_1860.method_28(1f); // sim_error sound
		sigmarHint = new SigmarSolver(boardDictionary).solveGame(0);
		finishSound = class_238.field_1991.field_1863; // sim_stop sound
	}
}
