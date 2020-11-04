using System;
using System.Collections.Generic;
using System.Collections.Immutable;


namespace Oracle
{
    public enum Player
    {
        White,
        Black
    }

    public class TaiyokuShogi
    {
        public const int BoardHeight = 36;
        public const int BoardWidth = 36;

        private readonly (Player, PieceIdentity)?[,] _state = new (Player, PieceIdentity)?[BoardHeight, BoardHeight];

        public TaiyokuShogi()
        {
            SetInitialBoard();
        }

        public (Player Player, PieceIdentity Id)? GetPiece(int x, int y) => _state[x, y];

        //   	 	 	 	 	D	 	 	 	 	GB	 	 	 	D	 	 	 	 	 	 	D	 	 	 	GB	 	 	 	 	D	 	 	 	 	 
        //  P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P
        //  LC	MK	VM	OX	LB	VP	VH	BN	DH	DK	SE	HF	EL	SP	VL	TG	SB	LD	DG	SB	TG	VL	SP	EL	HF	SE	DK	DH	BN	VH	VP	LB	OX	VM	MK	RC
        //  CH	SL	VR	WN	RE	M	SD	HS	GN	OS	EA	BS	SG	LP	T	BE	I	GM	GE	I	BE	T	LP	SG	BS	EA	OS	GN	HS	SD	M	RE	WN	VR	SL	CH
        //  EC	BL	EB	HO	OW	CM	CS	SW	BM	BT	OC	SF	BB	OR	SQ	SN	RD	LI	FE	RD	SN	SQ	OR	BB	SF	OC	BT	BM	SW	CS	CM	OW	HO	EB	VI	EC
        //  TC	VW	SX	DO	FH	VB	AB	EW	LH	CK	OM	CC	WS	ES	VS	NT	TF	RM	MT	TF	NT	VS	SU	NB	CC	OM	CK	LH	EW	AB	VB	FH	DO	SX	VW	TC
        //  WC	WH	HD	SM	PR	WB	FL	EG	FD	PS	FY	ST	BI	WG	F	KR	CA	GT	LL	HM	PH	F	WG	BI	ST	FY	PS	FD	EG	FL	WB	PR	SM	HD	WH	WC
        //  CI	CE	B	R	WF	FC	MF	VT	SO	LS	CL	CR	RH	HE	VO	GD	GO	DV	DS	GO	GD	VO	HE	RH	CR	CL	LS	SO	VT	MF	FC	WF	R	B	CE	CI
        //  SV	VE	N	PI	CG	PG	H	O	CN	SA	SR	GL	LN	CT	GS	VD	WL	GG	VG	WL	VD	GS	CT	LN	GL	SR	SA	CN	O	H	PG	CG	PI	N	VE	SV
        //  GC	SI	RN	RW	BG	RO	LT	LE	BO	WD	FP	RB	OK	PC	WA	FI	C	KM	PM	C	FI	WA	PC	OK	RB	FP	WD	BO	RI	TT	RO	BG	RW	RN	SI	GC
        //  RV	WE	TD	FS	CO	RA	FO	MS	RP	RU	SS	GR	RT	BA	BD	WR	S	NK	DE	S	GU	YA	BA	RT	GR	SS	RU	RP	MS	FO	RA	CO	FS	TD	FG	RV
        //  L	TS	RR	W	DM	ME	LO	BC	HR	FR	ED	CD	FT	Q	RS	LG	G	K	CP	G	RG	RS	Q	FT	WO	ED	FR	HR	BC	LO	ME	DM	W	RR	WT	L
        private void SetInitialBoard()
        {
            _state[0, 35] = (Player.White, PieceIdentity.Lance);
            _state[1, 35] = (Player.White, PieceIdentity.TurtleSnake);
            _state[2, 35] = (Player.White, PieceIdentity.RunningRabbit);
            _state[3, 35] = (Player.White, PieceIdentity.Whale);
            _state[4, 35] = (Player.White, PieceIdentity.FireDemon);
            _state[5, 35] = (Player.White, PieceIdentity.LeftMountainEagle);
            _state[6, 35] = (Player.White, PieceIdentity.LongNosedGoblin);
            _state[0, 34] = (Player.White, PieceIdentity.ReverseChariot);
        }
    }
}