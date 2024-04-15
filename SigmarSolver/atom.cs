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
	public const byte atom_null = 0;
	public const byte atom_salt = 1;
	public const byte atom_air = 2;
	public const byte atom_earth = 3;
	public const byte atom_fire = 4;
	public const byte atom_water = 5;
	public const byte atom_quicksilver = 6;
	public const byte atom_gold = 7;
	public const byte atom_silver = 8;
	public const byte atom_copper = 9;
	public const byte atom_iron = 10;
	public const byte atom_tin = 11;
	public const byte atom_lead = 12;
	public const byte atom_vitae = 13;
	public const byte atom_mors = 14;
	public const byte atom_repeat = 15; // reserved but not used
	public const byte atom_quintessence = 16;
	public const byte minUnusedID = 17;

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
		{        atom_lead, new SigmarAtom(        atom_lead, match_________self | match___projection, matchID___rightRight)},
		{         atom_tin, new SigmarAtom(         atom_tin, match_________self | match___projection, matchID___rightRight)},
		{        atom_iron, new SigmarAtom(        atom_iron, match_________self | match___projection, matchID___rightRight)},
		{      atom_copper, new SigmarAtom(      atom_copper, match_________self | match___projection, matchID___rightRight)},
		{      atom_silver, new SigmarAtom(      atom_silver, match_________self | match___projection, matchID___rightRight)},
		{        atom_gold, new SigmarAtom(        atom_gold, match__cannot_pair | match____singleton, matchID_________none)},
		{        atom_salt, new SigmarAtom(        atom_salt, match________berlo | match_____________, matchID_________salt)},
		{         atom_air, new SigmarAtom(         atom_air, match________berlo | match__unification, matchID__________air)},
		{       atom_water, new SigmarAtom(       atom_water, match________berlo | match__unification, matchID________water)},
		{        atom_fire, new SigmarAtom(        atom_fire, match________berlo | match__unification, matchID_________fire)},
		{       atom_earth, new SigmarAtom(       atom_earth, match________berlo | match__unification, matchID________earth)},
		{atom_quintessence, new SigmarAtom(atom_quintessence, match__cannot_pair | match__unification, matchID_quintessence)},
		{       atom_vitae, new SigmarAtom(       atom_vitae, match____animismus, matchID_____leftHalf)},
		{        atom_mors, new SigmarAtom(        atom_mors, match____animismus, matchID___rightRight)},
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
