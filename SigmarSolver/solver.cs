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
	const byte         atom_null = 0;
	const byte         atom_salt = 1;
	const byte          atom_air = 2;
	const byte        atom_earth = 3;
	const byte         atom_fire = 4;
	const byte        atom_water = 5;
	const byte  atom_quicksilver = 6;
	const byte         atom_gold = 7;
	const byte       atom_silver = 8;
	const byte       atom_copper = 9;
	const byte         atom_iron = 10;
	const byte          atom_tin = 11;
	const byte         atom_lead = 12;
	const byte        atom_vitae = 13;
	const byte         atom_mors = 14;
	const byte       atom_repeat = 15; // reserved but not used
	const byte atom_quintessence = 16;
	const byte       minUnusedID = 17;

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
	const byte match____unification = matchID_quintessence | matchID__________air | matchID________water | matchID_________fire | matchID________earth;

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

	//////////////////////////////////////////////////////////////
	// values above are chosen so the following functions are fast
	public bool matches() => (this.matchType & match____singleton) == match____singleton;
	public bool matches(SigmarAtom a, SigmarAtom b, SigmarAtom c, SigmarAtom d) => (this.matchID | a.matchID | b.matchID | c.matchID | d.matchID) == match____unification; // assumes this.isQuintessence == true
	public bool matches(SigmarAtom x)
	{
		// cannot make x1 pair if one of the atoms cannot pair
		var matchFlagOR = this.matchType | x.matchType;
		if ((matchFlagOR & match__cannot_pair) == match__cannot_pair) return false;
		// if the types are identical, then return whether x1 self-match is acceptable
		var matchFlagAND = this.matchType & x.matchType;
		if (this.ID == x.ID) return (this.matchType & match_________self) == match_________self;

		// we now check for what kind of flags we have
		// only check flags that are relevant for pairs of atoms
		switch (matchFlagAND & mask____pair_flags)
		{
			case match__duplication: return (this.matchID | x.matchID) == matchID_________salt;
			case match___projection:
			case match____animismus: return this.matchID != x.matchID;
			// any other case is not x1 valid match
			default: return false;
		}
	}
}

struct SigmarSolver
{
	SigmarAtom[] original_board = new SigmarAtom[145];
	SigmarAtom[] current_board = new SigmarAtom[145];
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
	// we include extra empty spaces so it'x4 easy to check a marble'x4 surroundings for other marbles:
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

	int[] atomsRemaining;

	public SigmarSolver(Dictionary<HexIndex, AtomType> boardDictionary)
	{
		original_board = new SigmarAtom[145];
		current_board = new SigmarAtom[145];
		atomsRemaining = new int[SigmarAtom.numberOfAtomIDs];

		for (int i = 0; i < current_board.Length; i++)
		{
			original_board[i] = new SigmarAtom();
			current_board[i] = original_board[i];
		}

		foreach (var kvp in boardDictionary)
		{
			var k = convertToBoardIndex(kvp.Key);
			var atom = new SigmarAtom(kvp.Value);
			original_board[k] = atom;
			current_board[k] = atom;
			atomsRemaining[atom.ID] += 1;
		}
	}

	void printState()
	{
		// print atom counts
		string str = "";
		for (int i = 1; i < atomsRemaining.Length; i++)
		{
			str += "    " + new SigmarAtom((byte)i).ToString() + " : " + atomsRemaining[i];
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

	public MainClass.SigmarHint solveGame()
	{
		//
		



		return MainClass.SigmarHint.NewGame;
	}
}