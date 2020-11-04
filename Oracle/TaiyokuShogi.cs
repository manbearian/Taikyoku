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
        public (Player Player, PieceIdentity Id)? GetPiece((int X, int Y) loc) => _state[loc.X, loc.Y];

        // 24 	 	 	 	 	D	 	 	 	 	GB	 	 	 	D	 	 	 	 	 	 	D	 	 	 	GB	 	 	 	 	D	 	 	 	 	       11
        // 25  P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P	P  10
        // 26  LC	MK	VM	OX	LB	VP	VH	BN	DH	DK	SE	HF	EL	SP	VL	TG	SB	LD	DG	SB	TG	VL	SP	EL	HF	SE	DK	DH	BN	VH	VP	LB	OX	VM	MK	RC  9
        // 27  CH	SL	VR	WN	RE	M	SD	HS	GN	OS	EA	BS	SG	LP	T	BE	I	GM	GE	I	BE	T	LP	SG	BS	EA	OS	GN	HS	SD	M	RE	WN	VR	SL	CH  8
        // 28  EC	BL	EB	HO	OW	CM	CS	SW	BM	BT	OC	SF	BB	OR	SQ	SN	RD	LI	FE	RD	SN	SQ	OR	BB	SF	OC	BT	BM	SW	CS	CM	OW	HO	EB	VI	EC  7
        // 29  TC	VW	SX	DO	FH	VB	AB	EW	LH	CK	OM	CC	WS	ES	VS	NT	TF	RM	MT	TF	NT	VS	SU	NB	CC	OM	CK	LH	EW	AB	VB	FH	DO	SX	VW	TC  6
        // 30  WC	WH	HD	SM	PR	WB	FL	EG	FD	PS	FY	ST	BI	WG	F	KR	CA	GT	LL	HM	PH	F	WG	BI	ST	FY	PS	FD	EG	FL	WB	PR	SM	HD	WH	WC  5
        // 31  CI	CE	B	R	WF	FC	MF	VT	SO	LS	CL	CR	RH	HE	VO	GD	GO	DV	DS	GO	GD	VO	HE	RH	CR	CL	LS	SO	VT	MF	FC	WF	R	B	CE	CI  4
        // 32  SV	VE	N	PI	CG	PG	H	O	CN	SA	SR	GL	LN	CT	GS	VD	WL	GG	VG	WL	VD	GS	CT	LN	GL	SR	SA	CN	O	H	PG	CG	PI	N	VE	SV  3
        // 33  GC	SI	RN	RW	BG	RO	LT	LE	BO	WD	FP	RB	OK	PC	WA	FI	C	KM	PM	C	FI	WA	PC	OK	RB	FP	WD	BO	RI	TT	RO	BG	RW	RN	SI	GC  2
        // 34  RV	WE	TD	FS	CO	RA	FO	MS	RP	RU	SS	GR	RT	BA	BD	WR	S	NK	DE	S	GU	YA	BA	RT	GR	SS	RU	RP	MS	FO	RA	CO	FS	TD	FG	RV  1
        // 35  L	TS	RR	W	DM	ME	LO	BC	HR	FR	ED	CD	FT	Q	RS	LG	G	K	CP	G	RG	RS	Q	FT	WO	ED	FR	HR	BC	LO	ME	DM	W	RR	WT	L   0
        private void SetInitialBoard()
        {
            // todo: don't list pieces twices, find a way to do it once!

            _state[0, 0] = (Player.Black, PieceIdentity.Lance);
            _state[1, 0] = (Player.Black, PieceIdentity.WhiteTiger);
            _state[2, 0] = (Player.Black, PieceIdentity.RunningRabbit);
            _state[3, 0] = (Player.Black, PieceIdentity.Whale);
            _state[4, 0] = (Player.Black, PieceIdentity.FireDemon);
            _state[5, 0] = (Player.Black, PieceIdentity.RightMountainEagle);
            _state[6, 0] = (Player.Black, PieceIdentity.LongNosedGoblin);
            _state[0, 1] = (Player.Black, PieceIdentity.ReverseChariot);

            // place all the pawns
            for (int i = 0; i < BoardWidth; ++i)
            {
                _state[i, 10] = (Player.Black, PieceIdentity.Pawn);
                _state[i, 25] = (Player.White, PieceIdentity.Pawn);
            }

            _state[0, 34] = (Player.White, PieceIdentity.ReverseChariot);

            _state[0, 35] = (Player.White, PieceIdentity.Lance);
            _state[1, 35] = (Player.White, PieceIdentity.TurtleSnake);
            _state[2, 35] = (Player.White, PieceIdentity.RunningRabbit);
            _state[3, 35] = (Player.White, PieceIdentity.Whale);
            _state[4, 35] = (Player.White, PieceIdentity.FireDemon);
            _state[5, 35] = (Player.White, PieceIdentity.LeftMountainEagle);
            _state[6, 35] = (Player.White, PieceIdentity.LongNosedGoblin);
            // ..
            _state[29, 35] = (Player.White, PieceIdentity.LongNosedGoblin);
            _state[30, 35] = (Player.White, PieceIdentity.RightMountainEagle);
            _state[31, 35] = (Player.White, PieceIdentity.FireDemon);
            _state[32, 35] = (Player.White, PieceIdentity.Whale);
            _state[33, 35] = (Player.White, PieceIdentity.RunningRabbit);
            _state[34, 35] = (Player.White, PieceIdentity.WhiteTiger);
            _state[35, 35] = (Player.White, PieceIdentity.Lance);
        }

        public IEnumerable<(int X, int Y)> GetLegalMoves(Player player, PieceIdentity id, (int X, int Y) loc)
        {
            var legalMoves = new List<(int X, int Y)>();
            var movement = Movement.GetMovement(id);

            for (int i = 1; i <= movement.StepRange[Movement.Up]; ++i)
            {
                var (x, y) = (loc.X, player == Player.White ? loc.Y - i : loc.Y + i);

                if (y < 0 || y >= BoardHeight)
                    break;

                var existingPiece = _state[x, y];
                if (existingPiece.HasValue)
                {
                    if (existingPiece.Value.Item1 != player)
                        legalMoves.Add((x, y));
                    break;
                }

                legalMoves.Add((x, y));
            }

            for (int i = 1; i <= movement.StepRange[Movement.Down]; ++i)
            {
                var (x, y) = (loc.X, player == Player.White ? loc.Y + i : loc.Y - i);

                if (y < 0 || y >= BoardHeight)
                    break;

                var existingPiece = _state[x, y];
                if (existingPiece.HasValue)
                {
                    if (existingPiece.Value.Item1 != player)
                        legalMoves.Add((x, y));
                    break;
                }

                legalMoves.Add((x, y));
            }

            for (int i = 1; i <= movement.StepRange[Movement.Left]; ++i)
            {
                var (x, y) = (player == Player.White ? loc.X - i : loc.X + i, loc.Y);

                if (x < 0 || x >= BoardWidth)
                    break;

                var existingPiece = _state[x, y];
                if (existingPiece.HasValue)
                {
                    if (existingPiece.Value.Item1 != player)
                        legalMoves.Add((x, y));
                    break;
                }

                legalMoves.Add((x, y));
            }

            for (int i = 1; i <= movement.StepRange[Movement.Right]; ++i)
            {
                var (x, y) = (player == Player.White ? loc.X + i : loc.X - i, loc.Y);

                if (x < 0 || x >= BoardWidth)
                    break;

                var existingPiece = _state[x, y];
                if (existingPiece.HasValue)
                {
                    if (existingPiece.Value.Item1 != player)
                        legalMoves.Add((x, y));
                    break;
                }

                legalMoves.Add((x, y));
            }

            return legalMoves;
        }
    }
}