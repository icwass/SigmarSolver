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
//using Texture = class_256;

struct SigmarAtom
{
	public readonly byte ID = atom_null; // must be unique
	readonly byte matchType = match_________none; // used to decide what type of matching to do
	readonly byte matchID = 0; // used to help simplify match logic

	public const byte numberOfAtomIDs = minUnusedID;
	public bool isEmpty => ID == atom_null;
	public bool isInvalidAtom => matchType == match_________none;
	public bool isGold => ID == atom_gold;
	public bool isQuintessence => ID == atom_quintessence;
	public int atomsNeededForMatch()
	{
		switch (ID)
		{
			case atom_gold: return 1;
			case atom_quintessence: return 5;
			default: return 2;
		}
	}

	public SigmarAtom() { }
	public SigmarAtom(byte ID, byte matchType, byte matchID = 0)
	{
		this.ID = ID;
		this.matchType = matchType;
		this.matchID = matchID;
	}
	public SigmarAtom(byte ID)
	{
		if (atomGenerator.ContainsKey(ID)) this = atomGenerator[ID];
	}
	public SigmarAtom(AtomType type)
	{
		if (atomtypeTranslator.ContainsKey(type)) this = new SigmarAtom(atomtypeTranslator[type]);
	}

	override public string ToString()
	{
		string[] table = new string[17] { " ", "🜔", "🜁", "🜃", "🜂", "🜄", "☿", "☉", "☽", "♀", "♂", "♃", "♄", "🜍", "🜞", "…", "✶" };
		return (ID < 17) ? table[ID] : "?";
	}

	// IDs
	public const byte         atom_null = 0;
	public const byte         atom_salt = 1;
	public const byte          atom_air = 2;
	public const byte        atom_earth = 3;
	public const byte         atom_fire = 4;
	public const byte        atom_water = 5;
	public const byte  atom_quicksilver = 6;
	public const byte         atom_gold = 7;
	public const byte       atom_silver = 8;
	public const byte       atom_copper = 9;
	public const byte         atom_iron = 10;
	public const byte          atom_tin = 11;
	public const byte         atom_lead = 12;
	public const byte        atom_vitae = 13;
	public const byte         atom_mors = 14;
	public const byte       atom_repeat = 15; // reserved but not used
	public const byte atom_quintessence = 16;
	public const byte       minUnusedID = 17;

	// matchTypes
	const byte match_________none = 0b_0000_0000;
	const byte match_________self = 0b_1000_0000;
	const byte match____singleton = 0b_0100_0000;
	const byte match__cannot_pair = 0b_0010_0000;
	// unassigned                   0b_0001_0000;

	const byte match___projection = 0b_0000_0001;
	const byte match__duplication = 0b_0000_0010;
	const byte match____animismus = 0b_0000_0100;
	const byte match__unification = 0b_0000_1000;

	const byte mask____pair_flags = match___projection | match__duplication | match____animismus;
	const byte mask___quint_flags = match__unification;

	const byte match_____________ = match_________none;
	const byte match________berlo = match_________self | match__duplication;


	// matchIDs
	const byte matchID_________none = 0x00;
	const byte matchID_____leftHalf = 0x0F;
	const byte matchID___rightRight = 0xF0;

	const byte matchID_________salt = 0b_01_1111;
	const byte matchID__________air = 0b_00_0001;
	const byte matchID________water = 0b_00_0010;
	const byte matchID_________fire = 0b_00_0100;
	const byte matchID________earth = 0b_00_1000;
	const byte matchID_quintessence = 0b_10_0000;
	const byte match___quintessence = matchID_quintessence | matchID__________air | matchID________water | matchID_________fire | matchID________earth;

	// note: define atoms so the following statement is always true: (X matches with Y) => (X.matchType & Y.matchType != 0)
	static Dictionary<byte, SigmarAtom> atomGenerator = new()
	{
		//
		{ atom_quicksilver, new SigmarAtom( atom_quicksilver, match_____________ | match___projection, matchID_____leftHalf)},
		{        atom_lead,	new SigmarAtom(        atom_lead, match_________self | match___projection, matchID___rightRight)},
		{         atom_tin,	new SigmarAtom(         atom_tin, match_________self | match___projection, matchID___rightRight)},
		{        atom_iron,	new SigmarAtom(        atom_iron, match_________self | match___projection, matchID___rightRight)},
		{      atom_copper,	new SigmarAtom(      atom_copper, match_________self | match___projection, matchID___rightRight)},
		{      atom_silver,	new SigmarAtom(      atom_silver, match_________self | match___projection, matchID___rightRight)},
		{        atom_gold,	new SigmarAtom(        atom_gold, match__cannot_pair | match____singleton, matchID_________none)},
		{        atom_salt,	new SigmarAtom(        atom_salt, match________berlo | match_____________, matchID_________salt)},
		{         atom_air,	new SigmarAtom(         atom_air, match________berlo | match__unification, matchID__________air)},
		{       atom_water,	new SigmarAtom(       atom_water, match________berlo | match__unification, matchID________water)},
		{        atom_fire,	new SigmarAtom(        atom_fire, match________berlo | match__unification, matchID_________fire)},
		{       atom_earth,	new SigmarAtom(       atom_earth, match________berlo | match__unification, matchID________earth)},
		{atom_quintessence, new SigmarAtom(atom_quintessence, match__cannot_pair | match__unification, matchID_quintessence)},
		{       atom_vitae,	new SigmarAtom(       atom_vitae, match____animismus, matchID_____leftHalf)},
		{        atom_mors,	new SigmarAtom(        atom_mors, match____animismus, matchID___rightRight)},
		{      atom_repeat, new SigmarAtom(      atom_repeat, match_____________)},
	};
	static Dictionary<AtomType, byte> atomtypeTranslator = new()
	{
		{class_175.field_1680,  atom_quicksilver},
		{class_175.field_1681,         atom_lead},
		{class_175.field_1683,          atom_tin},
		{class_175.field_1684,         atom_iron},
		{class_175.field_1682,       atom_copper},
		{class_175.field_1685,       atom_silver},
		{class_175.field_1686,         atom_gold},
		{class_175.field_1675,         atom_salt},
		{class_175.field_1676,          atom_air},
		{class_175.field_1679,        atom_water},
		{class_175.field_1678,         atom_fire},
		{class_175.field_1677,        atom_earth},
		{class_175.field_1690, atom_quintessence},
		{class_175.field_1687,        atom_vitae},
		{class_175.field_1688,         atom_mors},
		{class_175.field_1689,       atom_repeat},
		// any other atomType is not allowed
	};
	public static Dictionary<byte, byte> blockID = new()
	{
		{        atom_null, atom_null},
		{ atom_quicksilver, atom_null},
		{        atom_lead, atom_null},
		{         atom_tin, atom_lead},
		{        atom_iron, atom_tin},
		{      atom_copper, atom_iron},
		{      atom_silver, atom_copper},
		{        atom_gold, atom_silver},
		{        atom_salt, atom_null},
		{         atom_air, atom_null},
		{       atom_water, atom_null},
		{        atom_fire, atom_null},
		{       atom_earth, atom_null},
		{atom_quintessence, atom_null},
		{       atom_vitae, atom_null},
		{        atom_mors, atom_null},
		{      atom_repeat, atom_null}
	};

	//////////////////////////////////////////////////////////////
	// values above are chosen so the following functions are fast
	public bool matches() => (this.matchType & match____singleton) == match____singleton;
	public bool matches(SigmarAtom a, SigmarAtom b, SigmarAtom c, SigmarAtom d)
	{
		var matchFlagAND = this.matchType & a.matchType & b.matchType & c.matchType & d.matchType;
		// we now check for what kind of flags we have
		// only check flags that are relevant for quintets of atoms
		switch (matchFlagAND & mask___quint_flags)
		{
			case match__unification: return (this.matchID | a.matchID | b.matchID | c.matchID | d.matchID) == match___quintessence;
			// any other case is not a valid match
			default: return false;
		}
	}
	public bool matchesWith(SigmarAtom x)
	{
		// cannot make a pair if one of the atoms cannot pair
		var matchFlagOR = this.matchType | x.matchType;
		if ((matchFlagOR & match__cannot_pair) == match__cannot_pair) return false;
		// if the types are identical, then return whether a self-match is acceptable
		var matchFlagAND = this.matchType & x.matchType;
		if (this.ID == x.ID) return (this.matchType & match_________self) == match_________self;

		// we now check for what kind of flags we have
		// only check flags that are relevant for pairs of atoms
		switch (matchFlagAND & mask____pair_flags)
		{
			case match__duplication: return (this.matchID | x.matchID) == matchID_________salt;
			case match___projection:
			case match____animismus: return this.matchID != x.matchID;
			// any other case is not a valid match
			default: return false;
		}
	}
}

class SigmarSolver
{
	////////////////////////////////////////////////////////////////////////////////////////////////////
	// board data/methods
	SigmarAtom[] original_board = new SigmarAtom[145];
	SigmarAtom[] current_board = new SigmarAtom[145];
	List<SigmarAtom[]>[] unsolvablePositions = new List<SigmarAtom[]>[145];
	const byte boardFirstSpace = 12;
	const byte boardLastSpace = 132;
	// Each index in the array maps to the following current_board spaces:
	//                                                                                                          000
	//									        001   002   003   004   005    006   007   008   009   010   011
	//									    //-------------------------------\\
	//									  // 012   013   014   015   016   017 \\ 018   019   020   021   022
	//								   // 023   024   025   026   027   028   029 \\ 030   031   032   033
	//							    // 034   035   036   037   038   039   040   041 \\ 042   043   044
	//							 // 045   046   047   048   049   050   051   052   053 \\ 054   055
	//						  // 056   057   058   059   060   061   062   063   064   065 \\ 066
	//					   || 067   068   069   070   071   072   073   074   075   076   077 ||
	//					 078 \\  079   080   081   082   083   084   085   086   087   088 //
	//                089   090 \\  091   092   093   094   095   096   097   098   099 //
	//             100   101   102 \\  103   104   105   106   107   108   109   110 //
	//          111   112   113   114 \\  115   116   117   118   119   120   121 //
	//       122   123   124   125   126 \\  127   128   129   130   131   132 //
	//                                     \\-------------------------------//
	//    133   134   135   136   137   138    139   140   141   142   143
	// 144
	//
	// we include extra empty spaces so it's easy to check a marble's surroundings for other marbles:
	//
	//  -12   -11
	//     \ /
	// -1 - X - +1
	//     / \
	//  +11   +12
	static int convertToBoardIndex(HexIndex hex) => 67 + hex.Q - hex.R * 11;
	static HexIndex convertToHexIndex(int index) => new HexIndex((index + 10) % 11, 7 - ((index + 10) / 11));
	bool IsEmpty(int index) => current_board[index].isEmpty;
	bool marbleIsFree(int index)
	{
		// assumes 12 <= index <= 132
		int surroundings = (IsEmpty(index+1) ? 1 : 0) | (IsEmpty(index-11) ? 2 : 0);
		surroundings |= surroundings << 6 | (IsEmpty(index - 12) ? 4 : 0) | (IsEmpty(index - 1) ? 8 : 0) | (IsEmpty(index + 11) ? 16 : 0) | (IsEmpty(index + 12) ? 32 : 0);
		return (surroundings << 2 & surroundings << 1 & surroundings) != 0;
	}
	bool marbleIsNotBlocked(int index)
	{
		var blockID = SigmarAtom.blockID[original_board[index].ID];
		return blockID == SigmarAtom.atom_null || remainingAtomsOfType[blockID] == 0;
	}

	////////////////////////////////////////////////////////////////////////////////////////////////////
	// misc data/methods
	int[] remainingAtomsOfType;
	int atomsLeftOnBoard;
	bool boardWasSolved => atomsLeftOnBoard == 0;
	bool boardIsInvalid = false;

	bool boardIsObviouslyUnsolvable(int solverType)
	{
		int quints = remainingAtomsOfType[SigmarAtom.atom_quintessence];
		int[] cardinals = new int[4] {
			remainingAtomsOfType[SigmarAtom.atom_air],
			remainingAtomsOfType[SigmarAtom.atom_earth],
			remainingAtomsOfType[SigmarAtom.atom_fire],
			remainingAtomsOfType[SigmarAtom.atom_water]
		};
		int[] projectables = new int[5] {
			remainingAtomsOfType[SigmarAtom.atom_lead],
			remainingAtomsOfType[SigmarAtom.atom_tin],
			remainingAtomsOfType[SigmarAtom.atom_iron],
			remainingAtomsOfType[SigmarAtom.atom_copper],
			remainingAtomsOfType[SigmarAtom.atom_silver]
		};
		return cardinals.Any(x => x < quints) // too much quintessence
			|| remainingAtomsOfType[SigmarAtom.atom_salt] < cardinals.Select(x => (x - quints) % 2).Sum() // not enough salt
			|| remainingAtomsOfType[SigmarAtom.atom_quicksilver] < projectables.Select(x => x % 2).Sum() // not enough quicksilver
			|| remainingAtomsOfType[SigmarAtom.atom_quicksilver] > projectables.Sum() // too much quicksilver
			|| (solverType == 3 && currentPositionIsBad())
		;
	}
	void catalogBadBoard()
	{
		var badBoard = new SigmarAtom[145];
		for (int i = 12; i <= 132; i++)
		{
			badBoard[i] = new SigmarAtom(current_board[i].ID);
		}
		unsolvablePositions[atomsLeftOnBoard].Add(badBoard);
		//if (unsolvablePositions[atomsLeftOnBoard].Count % 10 == 1) Logger.Log("Bad boards of size " + atomsLeftOnBoard + " : " + unsolvablePositions[atomsLeftOnBoard].Count);
	}
	bool currentPositionIsBad()
	{
		foreach (var badBoard in unsolvablePositions[atomsLeftOnBoard])
		{
			bool matches = true;
			for (int i = 12; i <= 132; i++)
			{
				if (badBoard[i].ID != current_board[i].ID)
				{
					matches = false;
					break;
				}
			}
			if (matches) return true;
		}
		return false;
	}

	void printState()
	{
		// print atom counts
		string str = "";
		for (int i = 1; i < remainingAtomsOfType.Length; i++)
		{
			str += "    " + new SigmarAtom((byte)i).ToString() + " : " + remainingAtomsOfType[i];
		}
		Log(str);
		// print current_board
		str = "";
		for (int i = 1; i <= 133; i++)
		{
			var hex = convertToHexIndex(i);
			if (hex.Q == 0)
			{
				Log(str);
				Log("");
				str = "";
				for (int k = -5; k < hex.R; k++)
				{
					str += "\t";
				}
			}
			str += current_board[i].ToString() + "\t\t";
		}
	}

	float checkpointTime;
	void Checkpoint(string msg = "")
	{
		if (msg != "" && MainClass.enableDataLogging) Logger.Log("[SigmarSolver]" + msg + ": " + (Time.NowInSeconds() - checkpointTime));
		checkpointTime = Time.NowInSeconds();
	}

	////////////////////////////////////////////////////////////////////////////////////////////////////
	// constructor
	public SigmarSolver(Dictionary<HexIndex, AtomType> boardDictionary)
	{
		checkpointTime = Time.NowInSeconds();
		original_board = new SigmarAtom[145];
		current_board = new SigmarAtom[145];
		unsolvablePositions = new List<SigmarAtom[]>[145];

		for (int i = 0; i < current_board.Length; i++)
		{
			original_board[i] = new SigmarAtom();
			current_board[i] = original_board[i];
			unsolvablePositions[i] = new();
		}

		remainingAtomsOfType = new int[SigmarAtom.numberOfAtomIDs];
		atomsLeftOnBoard = 0;
		foreach (var kvp in boardDictionary)
		{
			var atom = new SigmarAtom(kvp.Value);
			if (atom.isEmpty) continue;
			if (atom.isInvalidAtom) boardIsInvalid = true;
			var k = convertToBoardIndex(kvp.Key);
			original_board[k] = atom;
			current_board[k] = atom;
			remainingAtomsOfType[atom.ID]++;
			atomsLeftOnBoard++;
		}
		//Checkpoint(" Constructor");
		//printState();
		//Checkpoint(" printState");
	}

	////////////////////////////////////////////////////////////////////////////////////////////////////
	// Move struct and related data/methods
	struct Move
	{
		public readonly byte x1 = 0, x2 = 0, x3 = 0, x4 = 0, x5 = 0;
		public readonly byte size = 2;
		// only possible values for size are be 1, 2 or 5 due to the permitted constructors
		// additionally, size 2 should be the most common, followed by 1 and then 5
		// so switches are written to reflect this fact
		public Move()
		{
			size = 0;
		}
		public Move(byte x1)
		{
			this.x1 = x1;
			size = 1;
		}
		public Move(byte x1, byte x2)
		{
			this.x1 = x1;
			this.x2 = x2;
			// size = 2; // already 2 by default
		}
		public Move(byte x1, byte x2, byte x3, byte x4, byte x5)
		{
			this.x1 = x1;
			this.x2 = x2;
			this.x3 = x3;
			this.x4 = x4;
			this.x5 = x5;
			size = 5;
		}
		public byte[] getBytes()
		{
			switch (size)
			{
				case 2: return new byte[2] { x1, x2 };
				case 1: return new byte[1] { x1 };
				default: return new byte[5] { x1, x2, x3, x4, x5 };
			}
		}
		public MainClass.SigmarHint GetHint() => new MainClass.SigmarHint(getBytes().Select(x => convertToHexIndex(x)).ToArray());

		public static bool MovesAreEqual(Move M, Move N)
		{
			// this equality function is fast, but it is "sloppy"
			// because it assumes that non-quintessence indices are sorted so indices[k] >= indices[k+1] for each k
			// this means that if an index is zero, then the remaining indices are also assumed to be zero
			if (M.x1 != N.x1 || M.size != N.size) return false;
			switch (M.size)
			{
				case 2: return M.x2 == N.x2;
				case 0:
				case 1: return true;
				default: return M.x2 == N.x2 && M.x3 == N.x3 && M.x4 == N.x4 && M.x5 == N.x5;
			}
		}
	}

	Stack<Move> MoveHistory = new();
	Stack<Move> MovesToCheck = new(); // we use Moves of size 0 to denote when to backtrack up the MoveHistory



	bool OutOfMovesAtThisStage() => MovesToCheck.Peek().size == 0;
	void MakeMove()
	{
		// assumes MovesToCheck is not empty
		var M = MovesToCheck.Pop();
		MoveHistory.Push(M);
		foreach (byte index in M.getBytes())
		{
			current_board[index] = new SigmarAtom(SigmarAtom.atom_null);
			remainingAtomsOfType[original_board[index].ID]--;
			atomsLeftOnBoard--;
		}
	}
	void UndoMove()
	{
		// assumes MoveHistory is not empty
		var M = MoveHistory.Pop();
		foreach (byte index in M.getBytes())
		{
			var atom = original_board[index];
			current_board[index] = atom;
			remainingAtomsOfType[atom.ID]++;
			atomsLeftOnBoard++;
		}
	}
	void AddNewMove(Move M, int solverType)
	{
		if (solverType == 2 && MovesToCheck.Any(x => Move.MovesAreEqual(x, M))) return;
		MovesToCheck.Push(M);
	}
	void generateMovesToCheck(int solverType, bool initialGeneration = false)
	{
		List<byte> freeSingletonMarbles = new();
		List<byte> freePairableMarbles = new();
		List<byte> freeQuintessenceMarbles = new();

		if (!initialGeneration) AddNewMove(new Move(), 1);

		// find marbles that are free
		// and add sington matches, too
		for (byte i = boardFirstSpace; i <= boardLastSpace; i++)
		{
			if (IsEmpty(i)) continue;
			if (marbleIsFree(i) && marbleIsNotBlocked(i))
			{
				switch (original_board[i].atomsNeededForMatch())
				{
					case 2: freePairableMarbles.Add(i); break;
					case 1: freeSingletonMarbles.Add(i); break;
					default: freeQuintessenceMarbles.Add(i); break;
				}
			}
		}

		// add quintessence-moves that are valid
		foreach (byte q in freeQuintessenceMarbles)
		{
			var atomq = original_board[q];
			for (int n4 = 0; n4 < freePairableMarbles.Count(); n4++)
			{
				byte i4 = freePairableMarbles[n4];
				var atom4 = original_board[i4];
				for (int n3 = n4 + 1; n3 < freePairableMarbles.Count(); n3++)
				{
					byte i3 = freePairableMarbles[n3];
					var atom3 = original_board[i3];
					for (int n2 = n3 + 1; n2 < freePairableMarbles.Count(); n2++)
					{
						byte i2 = freePairableMarbles[n2];
						var atom2 = original_board[i2];
						for (int n1 = n2 + 1; n1 < freePairableMarbles.Count(); n1++)
						{
							byte i1 = freePairableMarbles[n1];
							var atom1 = original_board[i1];
							if (atomq.matches(atom1, atom2, atom3, atom4))
							{
								AddNewMove(new Move(q, i1, i2, i3, i4), solverType);
								Log("Added move: " + atomq.ToString() + " " + atom1.ToString() + " " + atom2.ToString() + " " + atom3.ToString() + " " + atom4.ToString());
							}
						}
					}
				}
			}
		}

		// add pair-moves that are valid
		List<Move> movesToAdd = new();
		for (int n2 = 0; n2 < freePairableMarbles.Count(); n2++)
		{
			byte j = freePairableMarbles[n2];
			var atom_j = original_board[j];
			for (int n1 = n2 + 1; n1 < freePairableMarbles.Count(); n1++)
			{
				byte i = freePairableMarbles[n1];
				var atom_i = original_board[i];
				if (atom_i.matchesWith(atom_j))
				{
					movesToAdd.Add(new Move(i, j));
					Log("Added move: " + atom_i.ToString() + " " + atom_j.ToString());
				}
			}
		}

		if (solverType == 4)
		{
			for (int n = movesToAdd.Count - 1; n >= 0; n--)
			{
				AddNewMove(movesToAdd[n], solverType);
			}
		}
		else
		{
			for (int n = 0; n < movesToAdd.Count; n++)
			{
				AddNewMove(movesToAdd[n], solverType);
			}
		}

		


		// add singleton moves
		foreach (var s in freeSingletonMarbles)
		{
			if (original_board[s].matches()) AddNewMove(new Move(s), solverType);
		}
	}

	////////////////////////////////////////////////////////////////////////////////////////////////////
	// THE SOLVER
	void Log(string msg)
	{
		//Logger.Log(msg);
	}

	public MainClass.SigmarHint solveGame(int solverType = 1)
	{
		//Checkpoint("    Time spent waiting for solve signal");
		if (boardIsInvalid)
		{
			Checkpoint("The board was invalid. Time spent");
			return MainClass.SigmarHint.Exit;
		}
		if (boardWasSolved)
		{
			Checkpoint("The board was already solved. Time spent");
			return MainClass.SigmarHint.NewGame;
		}
		if (boardIsObviouslyUnsolvable(1))
		{
			Checkpoint("The board is obviously unsolvable. Time spent");
			return MainClass.SigmarHint.NewGame;
		}
		//find the initial opening moves
		generateMovesToCheck(1, true);
		if (MovesToCheck.Count == 0)
		{
			Checkpoint("The board is already unsolvable due to no opening moves. Time spent");
			return MainClass.SigmarHint.NewGame;
		}
		/*
		if (MovesToCheck.Count == 1)
		{
			Checkpoint("The board has only one valid move. Time spent");
			return MovesToCheck.Peek().GetHint();
		}
		*/


		//Checkpoint("    Ready for solving" + (alternateVersion ? " using alternate code" : ""));
		int loops = 0;
		while (MovesToCheck.Count > 0)
		{
			loops++;
			if (OutOfMovesAtThisStage())
			{
				if (solverType == 3) catalogBadBoard();
				UndoMove();
				Log("Out of moves here, moving back up a stage");
				MovesToCheck.Pop(); // go back up a stage
			}
			else
			{
				MakeMove();
				var M = MoveHistory.Peek();
				Log("    Let's try " + M.GetHint().ToString());
				if (boardWasSolved)
				{
					// reached the bottom stage!
					var firstMove = MoveHistory.Last();
					string msg = "";
					switch (solverType)
					{
						case 0: msg = "Solver1 (timeout disabled)"; break;
						default: msg = "Solver" + solverType; break;
					}
					Checkpoint("Done - the board is solvable in " + loops + " loops using " + msg + "! Time spent");
					return firstMove.GetHint();
				}
				if (boardIsObviouslyUnsolvable(solverType))
				{
					Log("    Whoops, that was an obviously bad move, backtracking... ");
					UndoMove();
				}
				else
				{
					generateMovesToCheck(solverType); // go down a stage
					Log("Moving down a stage...");
				}
			}
			if (loops >= new int[] { int.MaxValue, 20000, 20000, 200000, 20000 }[solverType])
			{
				Checkpoint("Giving up after " + loops + " loops. Time spent");
				return MainClass.SigmarHint.TimeOut;
			}
		}

		// reached all the way back up to the top stage - board cannot be solved
		Checkpoint("Done! Board is unsolvable, determined in " + loops + " loops. Time spent");
		return MainClass.SigmarHint.NewGame;
	}
}