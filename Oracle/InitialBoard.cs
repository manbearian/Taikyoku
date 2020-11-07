using System;
using System.Collections.Generic;
using System.Linq;


namespace Oracle
{

    internal static class InititialBoard
    {
        private static readonly string[] InitialBoardData =
         {
           " 	 	 	 	 	D	 	 	 	 	GB	 	 	 	D	 	 	 	 	 	 	D	 	 	 	GB	 	 	 	 	D	 	 	 	 	 	 ",
           "P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P",
           "LC	MK	VM	OX	LB	VP	VH	BN	DH	DK	SE	HF	EL	SP	VL	TG	SB	LD	DG	SB	TG	VL	SP	EL	HF	SE	DK	DH	BN	VH	VP	LB	OX	VM	MK	RC",
           "CH	SL	VR	WN	RE	M	SD	HS	GN	OS	EA	BS	SG	LP	T	BE	I	GM	GE	I	BE	T	LP	SG	BS	EA	OS	GN	HS	SD	M	RE	WN	VR	SL	CH",
           "EC	BL	EB	HO	OW	CM	CS	SW	BM	BT	OC	SF	BB	OR	SQ	SN	RD	LI	FE	RD	SN	SQ	OR	BB	SF	OC	BT	BM	SW	CS	CM	OW	HO	EB	VI	EC",
           "TC	VW	SX	DO	FH	VB	AB	EW	LH	CK	OM	CC	WS	ES	VS	NT	TF	RM	MT	TF	NT	VS	SU	NB	CC	OM	CK	LH	EW	AB	VB	FH	DO	SX	VW	TC",
           "WC	WH	HDL	SM	PR	WB	FL	EG	FD	PS	FY	ST	BI	WG	F	KR	CA	GT	LL	HM	PH	F	WG	BI	ST	FY	PS	FD	EG	FL	WB	PR	SM	HDR	WH	WC",
           "CI	CE	B	R	WF	FC	MF	VT	SO	LS	CL	CR	RH	HE	VO	GD	GO	DV	DS	GO	GD	VO	HE	RH	CR	CL	LS	SO	VT	MF	FC	WF	R	B	CE	CI",
           "SV	VE	N	PI	CG	PG	H	O	CN	SA	SR	GL	LN	CT	GS	VD	WL	GG	VG	WL	VD	GS	CT	LN	GL	SR	SA	CN	O	H	PG	CG	PI	N	VE	SV",
           "GC	SI	RN	RW	BG	RO	LT	LE	BO	WD	FP	RB	OK	PC	WA	FI	C	KM	PM	C	FI	WA	PC	OK	RB	FP	WD	BO	RI	TT	RO	BG	RW	RN	SI	GC",
           "RV	WE	TD	FS	CO	RA	FO	MS	RP	RU	SS	GR	RT	BA	BD	WR	S	NK	DE	S	GU	YA	BA	RT	GR	SS	RU	RP	MS	FO	RA	CO	FS	TD	FG	RV",
           "L	TS	RR	W	DM	MEL	LO	BC	HR	FR	ED	CD	FT	Q	RS	LG	G	K	CP	G	RG	RS	Q	FT	WO	ED	FR	HR	BC	LO	MER	DM	W	RR	WT	L"
        };

        private static readonly Dictionary<string, PieceIdentity?> PieceMap = new Dictionary<string, PieceIdentity?> {
            { "AB", PieceIdentity.AngryBoar },
            { "B", PieceIdentity.Bishop },
            { "BA", PieceIdentity.RunningBear },
            { "BB", PieceIdentity.BlindBear },
            { "BC", PieceIdentity.BeastCadet },
            { "BD", PieceIdentity.BuddhistDevil },
            { "BE", PieceIdentity.BearSoldier },
            { "BG", PieceIdentity.BishopGeneral },
            { "BI", PieceIdentity.BlindDog },
            { "BL", PieceIdentity.BlueDragon },
            { "BM", PieceIdentity.BlindMonkey },
            { "BN", PieceIdentity.BurningSoldier },
            { "BO", PieceIdentity.BeastOfficer },
            { "BS", PieceIdentity.BoarSoldier },
            { "BT", PieceIdentity.BlindTiger },
            { "C", PieceIdentity.CopperGeneral },
            { "CA", PieceIdentity.Capricorn },
            { "CC", PieceIdentity.ChineseCock },
            { "CD", PieceIdentity.CeramicDove },
            { "CE", PieceIdentity.CloudEagle },
            { "CG", PieceIdentity.ChickenGeneral },
            { "CH", PieceIdentity.ChariotSoldier },
            { "CI", PieceIdentity.StoneChariot },
            { "CK", PieceIdentity.FlyingCock },
            { "CL", PieceIdentity.CloudDragon },
            { "CM", PieceIdentity.ClimbingMonkey },
            { "CN", PieceIdentity.CenterStandard },
            { "CO", PieceIdentity.CaptiveOfficer },
            { "CP", PieceIdentity.Prince },
            { "CR", PieceIdentity.CopperChariot },
            { "CS", PieceIdentity.CatSword },
            { "CT", PieceIdentity.CaptiveCadet },
            { "D", PieceIdentity.Dog },
            { "DE", PieceIdentity.DrunkenElephant },
            { "DG", PieceIdentity.RoaringDog },
            { "DH", PieceIdentity.DragonHorse },
            { "DK", PieceIdentity.DragonKing },
            { "DO", PieceIdentity.Donkey },
            { "DM", PieceIdentity.FireDemon },
            { "DS", PieceIdentity.DarkSpirit },
            { "DV", PieceIdentity.Deva },
            { "EA", PieceIdentity.EarthGeneral },
            { "EB", PieceIdentity.EnchantedBadger },
            { "EC", PieceIdentity.EarthChariot },
            { "ED", PieceIdentity.EarthDragon },
            { "EG", PieceIdentity.FierceEagle },
            { "EL", PieceIdentity.SoaringEagle },
            { "ES", PieceIdentity.EasternBarbarian },
            { "EW", PieceIdentity.EvilWolf },
            { "F", PieceIdentity.FireGeneral },
            { "FC", PieceIdentity.FlyingCat },
            { "FD", PieceIdentity.FlyingDragon },
            { "FE", PieceIdentity.FreeEagle },
            { "FG", PieceIdentity.FragrantElephant },
            { "FH", PieceIdentity.FlyingHorse },
            { "FI", PieceIdentity.FireDragon },
            { "FL", PieceIdentity.FerociousLeopard },
            { "FO", PieceIdentity.ForestDemon },
            { "FP", PieceIdentity.FreePup },
            { "FR", PieceIdentity.FreeDemon },
            { "FS", PieceIdentity.FlyingSwallow },
            { "FT", PieceIdentity.FreeDreamEater },
            { "FY", PieceIdentity.FlyingGoose },
            { "G", PieceIdentity.GoldGeneral },
            { "GB", PieceIdentity.GoBetween },
            { "GC", PieceIdentity.GoldChariot },
            { "GD", PieceIdentity.GreatDragon },
            { "GE", PieceIdentity.GreatStandard },
            { "GG", PieceIdentity.GreatGeneral },
            { "GL", PieceIdentity.GoldenDeer },
            { "GM", PieceIdentity.GreatMaster },
            { "GN", PieceIdentity.WoodGeneral },
            { "GO", PieceIdentity.GoldenBird },
            { "GR", PieceIdentity.GreatDove },
            { "GS", PieceIdentity.GreatStag },
            { "GT", PieceIdentity.GreatTurtle },
            { "GU", PieceIdentity.GuardianOfTheGods },
            { "H", PieceIdentity.HorseGeneral },
            { "HDL", PieceIdentity.LeftHowlingDog },
            { "HDR", PieceIdentity.LeftHowlingDog },
            { "HE", PieceIdentity.RamsHeadSoldier },
            { "HF", PieceIdentity.HornedFalcon },
            { "HM", PieceIdentity.HookMover },
            { "HO", PieceIdentity.Horseman },
            { "HR", PieceIdentity.RunningHorse },
            { "HS", PieceIdentity.HorseSoldier },
            { "I", PieceIdentity.IronGeneral },
            { "K", PieceIdentity.King },
            { "KM", PieceIdentity.KirinMaster },
            { "KR", PieceIdentity.Kirin },
            { "L", PieceIdentity.Lance },
            { "LB", PieceIdentity.LongbowSoldier },
            { "LC", PieceIdentity.LeftChariot },
            { "LD", PieceIdentity.LionDog },
            { "LE", PieceIdentity.LeftDragon },
            { "LG", PieceIdentity.LeftGeneral },
            { "LH", PieceIdentity.LiberatedHorse },
            { "LI", PieceIdentity.LionHawk },
            { "LL", PieceIdentity.LittleTurtle },
            { "LN", PieceIdentity.Lion },
            { "LO", PieceIdentity.LongNosedGoblin },
            { "LP", PieceIdentity.LeopardSoldier },
            { "LS", PieceIdentity.LittleStandard },
            { "LT", PieceIdentity.LeftTiger },
            { "M", PieceIdentity.MountainGeneral },
            { "MEL", PieceIdentity.MountainEagle_Left },
            { "MER", PieceIdentity.MountainEagle_Right },
            { "MF", PieceIdentity.MountainFalcon },
            { "MK", PieceIdentity.SideMonkey },
            { "MS", PieceIdentity.MountainStag },
            { "MT", PieceIdentity.CenterMaster },
            { "N", PieceIdentity.Knight },
            { "NB", PieceIdentity.NorthernBarbarian },
            { "NK", PieceIdentity.NeighboringKing },
            { "NT", PieceIdentity.ViolentWolf },
            { "O", PieceIdentity.OxGeneral },
            { "OC", PieceIdentity.OxCart },
            { "OK", PieceIdentity.OldKite },
            { "OM", PieceIdentity.OldMonkey },
            { "OR", PieceIdentity.OldRat },
            { "OS", PieceIdentity.OxSoldier },
            { "OW", PieceIdentity.SwoopingOwl },
            { "OX", PieceIdentity.FlyingOx },
            { "P", PieceIdentity.Pawn },
            { "PC", PieceIdentity.Peacock },
            { "PG", PieceIdentity.PupGeneral },
            { "PH", PieceIdentity.Phoenix },
            { "PI", PieceIdentity.PigGeneral },
            { "PM", PieceIdentity.PhoenixMaster },
            { "PR", PieceIdentity.PrancingStag },
            { "PS", PieceIdentity.PoisonousSnake },
            { "Q", PieceIdentity.Queen },
            { "R", PieceIdentity.Rook },
            { "RA", PieceIdentity.RainDragon },
            { "RB", PieceIdentity.RushingBird },
            { "RC", PieceIdentity.RightChariot },
            { "RD", PieceIdentity.RecliningDragon },
            { "RE", PieceIdentity.RiverGeneral },
            { "RG", PieceIdentity.RightGeneral },
            { "RH", PieceIdentity.RunningChariot },
            { "RI", PieceIdentity.RightDragon },
            { "RM", PieceIdentity.RocMaster },
            { "RN", PieceIdentity.RunningStag },
            { "RO", PieceIdentity.RookGeneral },
            { "RP", PieceIdentity.RunningPup },
            { "RR", PieceIdentity.RunningRabbit },
            { "RS", PieceIdentity.RearStandard },
            { "RT", PieceIdentity.RunningTiger },
            { "RU", PieceIdentity.RunningSerpent },
            { "RV", PieceIdentity.ReverseChariot },
            { "RW", PieceIdentity.RunningWolf },
            { "S", PieceIdentity.SilverGeneral },
            { "SA", PieceIdentity.SideBoar },
            { "SB", PieceIdentity.CrossbowSoldier },
            { "SD", PieceIdentity.FrontStandard },
            { "SE", PieceIdentity.SwordSoldier },
            { "SF", PieceIdentity.SideFlier },
            { "SG", PieceIdentity.StoneGeneral },
            { "SI", PieceIdentity.SideDragon },
            { "SL", PieceIdentity.SideSoldier },
            { "SM", PieceIdentity.SideMover },
            { "SN", PieceIdentity.CoiledSerpent },
            { "SO", PieceIdentity.Soldier },
            { "SP", PieceIdentity.SpearSoldier },
            { "SQ", PieceIdentity.SquareMover },
            { "SR", PieceIdentity.SilverRabbit },
            { "SS", PieceIdentity.SideSerpent },
            { "ST", PieceIdentity.StruttingCrow },
            { "SU", PieceIdentity.SouthernBarbarian },
            { "SV", PieceIdentity.SilverChariot },
            { "SW", PieceIdentity.SwallowsWings },
            { "SX", PieceIdentity.SideOx },
            { "T", PieceIdentity.TileGeneral },
            { "TC", PieceIdentity.TileChariot },
            { "TD", PieceIdentity.TurtleDove },
            { "TF", PieceIdentity.TreacherousFox },
            { "TG", PieceIdentity.SavageTiger },
            { "TS", PieceIdentity.TurtleSnake },
            { "TT", PieceIdentity.RightTiger },
            { "VB", PieceIdentity.ViolentBear },
            { "VD", PieceIdentity.ViolentDragon },
            { "VE", PieceIdentity.VerticalBear },
            { "VG", PieceIdentity.ViceGeneral },
            { "VH", PieceIdentity.VerticalHorse },
            { "VI", PieceIdentity.VermillionSparrow },
            { "VL", PieceIdentity.VerticalLeopard },
            { "VM", PieceIdentity.VerticalMover },
            { "VO", PieceIdentity.ViolentOx },
            { "VP", PieceIdentity.VerticalPup },
            { "VR", PieceIdentity.VerticalSoldier },
            { "VS", PieceIdentity.ViolentStag },
            { "VT", PieceIdentity.VerticalTiger },
            { "VW", PieceIdentity.VerticalWolf },
            { "W", PieceIdentity.Whale },
            { "WA", PieceIdentity.WaterDragon },
            { "WB", PieceIdentity.WaterBuffalo },
            { "WC", PieceIdentity.WoodChariot },
            { "WD", PieceIdentity.WindDragon },
            { "WE", PieceIdentity.WhiteElephant },
            { "WF", PieceIdentity.SideWolf },
            { "WG", PieceIdentity.WaterGeneral },
            { "WH", PieceIdentity.WhiteHorse },
            { "WL", PieceIdentity.WoodlandDemon },
            { "WN", PieceIdentity.WindGeneral },
            { "WO", PieceIdentity.WoodenDove },
            { "WR", PieceIdentity.Wrestler },
            { "WS", PieceIdentity.WesternBarbarian },
            { "WT", PieceIdentity.WhiteTiger },
            { "YA", PieceIdentity.Yaksha },
            { " ", null }
        };

        public static void SetInitialState(this (Player, PieceIdentity)?[,] board)
        {
            // remove any existing pieces
            for (int i = 0; i < board.GetLength(0); ++i)
                for (int j = 0; j < board.GetLength(1); ++j)
                    board[i, j] = null;

            SetupPlayer(Player.White);
            SetupPlayer(Player.Black);

            void SetupPlayer(Player player)
            {
                int row(int i) => player == Player.White ? TaiyokuShogi.BoardWidth - i - 1 : i;
                int col(int i) => player == Player.White ? i : TaiyokuShogi.BoardHeight - i - 1;

                for (int i = 0; i < InitialBoardData.Length; ++i)
                {
                    var pieces = InitialBoardData[InitialBoardData.Length - i - 1].Split("\t");

                    for (int j = 0; j < pieces.Length; ++j)
                    {
                        var piece = PieceMap[pieces[j]];
                        if (piece != null)
                            board[col(j), row(i)] = (player, piece.Value);
                    }
                }

                // test pieces
                board[17, 17] = (Player.White, PieceIdentity.WoodenDove);
                board[18, 18] = (Player.White, PieceIdentity.Pawn);
                board[19, 19] = (Player.Black, PieceIdentity.Pawn);
                board[21, 21] = (Player.Black, PieceIdentity.Pawn);
            }
        }
    }
}