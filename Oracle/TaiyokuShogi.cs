using System;
using System.Collections.Generic;
using System.Linq;


namespace Oracle
{
    public enum Player
    {
        White,
        Black
    }

    public class IllegalMoveException : Exception { }

    public class TaiyokuShogi
    {
        public const int BoardHeight = 36;
        public const int BoardWidth = 36;

        private readonly (Player, PieceIdentity)?[,] _boardState = new (Player, PieceIdentity)?[BoardHeight, BoardHeight];

        public Player CurrentTurn { get; private set; } = Player.White;

        public TaiyokuShogi()
        {
            SetInitialBoard();
        }

        public (Player Player, PieceIdentity Id)? GetPiece(int x, int y) => _boardState[x, y];

        public (Player Player, PieceIdentity Id)? GetPiece((int X, int Y) loc) => _boardState[loc.X, loc.Y];

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

            _boardState[0, 0] = (Player.Black, PieceIdentity.Lance);
            _boardState[1, 0] = (Player.Black, PieceIdentity.WhiteTiger);
            _boardState[2, 0] = (Player.Black, PieceIdentity.RunningRabbit);
            _boardState[3, 0] = (Player.Black, PieceIdentity.Whale);
            _boardState[4, 0] = (Player.Black, PieceIdentity.FireDemon);
            _boardState[5, 0] = (Player.Black, PieceIdentity.RightMountainEagle);
            _boardState[6, 0] = (Player.Black, PieceIdentity.LongNosedGoblin);
            _boardState[0, 1] = (Player.Black, PieceIdentity.ReverseChariot);

            // place all the pawns
            for (int i = 0; i < BoardWidth; ++i)
            {
                _boardState[i, 10] = (Player.Black, PieceIdentity.Pawn);
                _boardState[i, 25] = (Player.White, PieceIdentity.Pawn);
            }

            // test pieces
            // _state[17, 16] = (Player.White, PieceIdentity.FreeBear);
            _boardState[17, 17] = (Player.Black, PieceIdentity.MountainStag);

            _boardState[0, 34] = (Player.White, PieceIdentity.ReverseChariot);

            _boardState[0, 35] = (Player.White, PieceIdentity.Lance);
            _boardState[1, 35] = (Player.White, PieceIdentity.TurtleSnake);
            _boardState[2, 35] = (Player.White, PieceIdentity.RunningRabbit);
            _boardState[3, 35] = (Player.White, PieceIdentity.Whale);
            _boardState[4, 35] = (Player.White, PieceIdentity.FireDemon);
            _boardState[5, 35] = (Player.White, PieceIdentity.LeftMountainEagle);
            _boardState[6, 35] = (Player.White, PieceIdentity.LongNosedGoblin);
            // ..
            _boardState[29, 35] = (Player.White, PieceIdentity.LongNosedGoblin);
            _boardState[30, 35] = (Player.White, PieceIdentity.RightMountainEagle);
            _boardState[31, 35] = (Player.White, PieceIdentity.FireDemon);
            _boardState[32, 35] = (Player.White, PieceIdentity.Whale);
            _boardState[33, 35] = (Player.White, PieceIdentity.RunningRabbit);
            _boardState[34, 35] = (Player.White, PieceIdentity.WhiteTiger);
            _boardState[35, 35] = (Player.White, PieceIdentity.Lance);
        }

        public IEnumerable<(int X, int Y)> GetLegalMoves(Player player, PieceIdentity id, (int X, int Y) loc)
        {
            var legalMoves = new List<(int X, int Y)>();
            var movement = Movement.GetMovement(id);

            for (int direction = 0; direction < movement.StepRange.Length; ++direction)
            {
                for (int i = 1; i <= movement.StepRange[direction]; ++i)
                {
                    var moveAmount = player == Player.White ? i : -i;
                    var (x, y) = direction switch
                    {
                        Movement.Up => (loc.X, loc.Y - moveAmount),
                        Movement.Down => (loc.X, loc.Y + moveAmount),
                        Movement.Left => (loc.X - moveAmount, loc.Y),
                        Movement.Right => (loc.X + moveAmount, loc.Y),
                        Movement.UpLeft => (loc.X - moveAmount, loc.Y - moveAmount),
                        Movement.UpRight => (loc.X + moveAmount, loc.Y - moveAmount),
                        Movement.DownLeft => (loc.X - moveAmount, loc.Y + moveAmount),
                        Movement.DownRight => (loc.X + moveAmount, loc.Y + moveAmount),
                        _ => throw new NotSupportedException()
                    };

                    if (y < 0 || y >= BoardHeight)
                        break;
                    if (x < 0 || x >= BoardWidth)
                        break;

                    var existingPiece = _boardState[x, y];
                    if (existingPiece.HasValue)
                    {
                        if (existingPiece.Value.Item1 != player)
                            legalMoves.Add((x, y));
                        break;
                    }

                    legalMoves.Add((x, y));
                }
            }

            return legalMoves;
        }

        // move the piece at startLoc to endLoc
        //   midLoc is for area-moves
        public void MakeMove(Player player, (int X, int Y) startLoc, (int X, int Y) endLoc, (int X, int Y)? midLoc = null)
        {
            if (CurrentTurn != player)
                throw new IllegalMoveException();

            var piece = GetPiece(startLoc);

            if (piece == null || piece.Value.Player != player)
                throw new IllegalMoveException();

            if (!GetLegalMoves(player, piece.Value.Id, startLoc).Contains(endLoc))
                throw new IllegalMoveException();

            if (midLoc != null)
            {
                // capture any piece that got run over by the area-move
                // todo: test if this is an area mover
                _boardState[midLoc.Value.X, midLoc.Value.Y] = null;
            }

            // todo: multi-capture for ranged-capture pieces

            // set new location, this has the effect of removing any piece that was there from the board
            _boardState[endLoc.X, endLoc.Y] = piece;

            UpdateTurn();
        }

        public void UpdateTurn() => CurrentTurn = CurrentTurn == Player.White ? Player.Black : Player.White;
    }
}