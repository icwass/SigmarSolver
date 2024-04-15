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
			|| (solverType == 4 && currentPositionIsBad())
		;
	}
	void catalogBadBoard()
	{
		var badBoard = new SigmarAtom[145];
		for (int i = 12; i <= 132; i++)
		{
			badBoard[i] = new SigmarAtom(current_board[i].ID);
			//badBoard[i] = current_board[i];
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
	/*
	void printState()
	{
		// print atom counts
		string str = "";
		for (int i = 1; i < remainingAtomsOfType.Length; i++)
		{
			str += "    " + new SigmarAtom((byte)i).ToString() + " : " + remainingAtomsOfType[i];
		}
		Logger.Log(str);
		// print current_board
		str = "";
		for (int i = 1; i <= 133; i++)
		{
			var hex = convertToHexIndex(i);
			if (hex.Q == 0)
			{
				Logger.Log(str);
				Logger.Log("");
				str = "";
				for (int k = -5; k < hex.R; k++)
				{
					str += "\t";
				}
			}
			str += current_board[i].ToString() + "\t\t";
		}
	}
	*/

	float checkpointTime;
	void Checkpoint(string msg = "")
	{
		if (msg != "" && MainClass.enableDataLogging) Logger.Log("[SigmarSolver]" + msg + ": " + (Time.NowInSeconds() - checkpointTime) + " s");
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
	}

	////////////////////////////////////////////////////////////////////////////////////////////////////
	// Move struct and related data/methods
	struct Move
	{
		public readonly byte x1 = 0, x2 = 0, x3 = 0, x4 = 0, x5 = 0;
		public readonly byte size = 2;
		// for ACTUAL moves, only possible values size are be 1, 2 or 5 due to the permitted constructors
		// additionally, size 2 should be the most common, followed by 1 and then 5
		// so switches are written to reflect this fact
		// (moves with size 0 are used to denote markers in stacks/queues/whatever
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
	void GenerateMovesToCheck(int solverType, bool initialGeneration = false)
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
				}
			}
		}

		if (solverType == 3)
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
		GenerateMovesToCheck(1, true);
		if (MovesToCheck.Count == 0)
		{
			Checkpoint("The board is already unsolvable due to no opening moves. Time spent");
			return MainClass.SigmarHint.NewGame;
		}

		int loops = 0;
		while (MovesToCheck.Count > 0)
		{
			loops++;
			if (OutOfMovesAtThisStage())
			{
				if (solverType == 4) catalogBadBoard();
				UndoMove();
				// out of moves, going back up a stage
				MovesToCheck.Pop(); // remove stage marker
			}
			else
			{
				MakeMove();
				if (boardWasSolved)
				{
					// reached the bottom stage!
					var firstMove = MoveHistory.Last();
					string msg = "";
					switch (solverType)
					{
						case 0: msg = "SolverSimple (timeout disabled)"; break;
						case 1: msg = "SolverSimple"; break;
						case 2: msg = "SolverNoDoublecover"; break;
						case 3: msg = "SolverMirror"; break;
						case 4: msg = "SolverMemoize"; break;
						default: msg = "Solver" + solverType; break;
					}
					Checkpoint("Done - the board is solvable in " + loops + " loops using " + msg + ". Time spent");
					return firstMove.GetHint();
				}
				if (boardIsObviouslyUnsolvable(solverType))
				{
					// whoops, time to backtrack
					UndoMove();
				}
				else
				{
					// time to go down a stage - look for more moves
					GenerateMovesToCheck(solverType);
				}
			}
			if (loops >= new int[] { int.MaxValue, 4000, 4000, 4000, 150000 }[solverType])
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