
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oracle
{
    public class Movement
    {
        // basic directions
        public const int UpLeft = 0;
        public const int Up = 1;
        public const int UpRight = 2;
        public const int Right = 3;
        public const int DownRight = 4;
        public const int Down = 5;
        public const int DownLeft = 6;
        public const int Left = 7;
        public const int DirectionCount = 8;

        // extra directions for "knight" jumps 
        public const int UpUpLeft = 8;
        public const int UpUpRight = 9;
        public const int UpLeftLeft = 10;
        public const int UpRightRight = 11;
        public const int DownRightRight = 12;
        public const int DownLeftLeft = 13;
        public const int DownDownLeft = 14;
        public const int DownDownRight = 15;
        public const int DirectionCountWithJumps = 16;

        public const int Unlimited = int.MaxValue;
        private static readonly int[] UnlimitedJump = new int[] { Unlimited };

        public static readonly int[] OrthoganalDirectrions = new int[] { Up, Right, Down, Left };
        public static readonly int[] DiagnalDirectrions = new int[] { UpLeft, UpRight, DownRight, DownLeft };
        public static readonly int[] JumpDirections = new int[] { UpUpLeft, UpUpRight, UpLeftLeft, UpRightRight, DownRightRight, DownLeftLeft, DownDownLeft, DownDownRight };

        // 8 states values 1-N
        //    0 means cannot move that direction (movement matrix)
        //    1 means single spaces in that direction
        //    N means move N squares in that direction
        //    N > 35 means move unlimited
        private int[] _stepRange = new int[8];
        public int[] StepRange { get => _stepRange; }

        // 16 states values of indicies (jumpable squares), int (range after jump)
        //    0 means cannot jump that direction (jump matrix)
        //    states 8-15 => 1 means can jump to this square (all other values invalid, range after jump must be 0)
        //    states 0-7 => N can over N squares and move M squares ater
        private (int[] JumpDistances, int RangeAfter)[] _jumpRange = new (int[] JumpDistances, int RangeAfter)[16];
        public (int[] JumpDistances, int RangeAfter)[] JumpRange { get => _jumpRange; }

        // Hook Move (90-degree turn)
        //    3 choices: orthog, diag, forward-diag
        private HookType? _hookMove = null;
        public HookType? HookMove { get => _hookMove; }

        // Area move (up to 2 single space moves, mult-capture)
        private bool _lionMove = false;
        public bool LionMove { get => _lionMove; }

        // Range capture (move over and capture number of pieces of lower rank)
        //   move matrix of Booleans for direction
        private bool[] _rangeCapture = new bool[8];
        public IReadOnlyList<bool> RangeCapture { get => _rangeCapture; }

        // Igui - capture without moving
        private bool _igui = false;
        public bool Igui { get => _igui; }

        public static Movement GetMovement(PieceIdentity id, TaiyokuShogiOptions options)
        {
            Movement m = new Movement();
            switch (id)
            {
                case PieceIdentity.Pawn:
                    m._stepRange[Up] = 1;
                    break;

                case PieceIdentity.EarthGeneral:
                case PieceIdentity.GoBetween:
                    m._stepRange[Up] = 1;
                    m._stepRange[Down] = 1;
                    break;

                case PieceIdentity.StoneGeneral:
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[UpRight] = 1;
                    break;

                case PieceIdentity.IronGeneral:
                case PieceIdentity.Dog:
                    m._stepRange[Up] = 1;
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[UpRight] = 1;
                    break;

                case PieceIdentity.SwoopingOwl:
                case PieceIdentity.OldRat:
                case PieceIdentity.StruttingCrow:
                    m._stepRange[Up] = 1;
                    m._stepRange[DownLeft] = 1;
                    m._stepRange[DownRight] = 1;
                    break;

                case PieceIdentity.TileGeneral:
                case PieceIdentity.SwordSoldier:
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[UpRight] = 1;
                    m._stepRange[Down] = 1;
                    break;

                case PieceIdentity.CopperGeneral:
                case PieceIdentity.FlyingGoose:
                case PieceIdentity.ClimbingMonkey:
                    m._stepRange[Up] = 1;
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[UpRight] = 1;
                    m._stepRange[Down] = 1;
                    break;

                case PieceIdentity.RecliningDragon:
                    m._stepRange[Up] = 1;
                    m._stepRange[Left] = 1;
                    m._stepRange[Right] = 1;
                    m._stepRange[Down] = 1;
                    break;

                case PieceIdentity.CoiledSerpent:
                    m._stepRange[Up] = 1;
                    m._stepRange[Down] = 1;
                    m._stepRange[DownLeft] = 1;
                    m._stepRange[DownRight] = 1;
                    break;

                case PieceIdentity.FlyingCock:
                    m._stepRange[Left] = 1;
                    m._stepRange[Right] = 1;
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[UpRight] = 1;
                    break;

                case PieceIdentity.CatSword:
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[UpRight] = 1;
                    m._stepRange[DownLeft] = 1;
                    m._stepRange[DownRight] = 1;
                    break;

                case PieceIdentity.EvilWolf:
                    m._stepRange[Left] = 1;
                    m._stepRange[Right] = 1;
                    m._stepRange[Up] = 1;
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[UpRight] = 1;
                    break;

                case PieceIdentity.SilverGeneral:
                case PieceIdentity.ViolentStag:
                    m._stepRange[Up] = 1;
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[UpRight] = 1;
                    m._stepRange[DownLeft] = 1;
                    m._stepRange[DownRight] = 1;
                    break;

                case PieceIdentity.BlindDog:
                case PieceIdentity.ChineseCock:
                    m._stepRange[Left] = 1;
                    m._stepRange[Right] = 1;
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[UpRight] = 1;
                    m._stepRange[Down] = 1;
                    break;

                case PieceIdentity.OldMonkey:
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[UpRight] = 1;
                    m._stepRange[Down] = 1;
                    m._stepRange[DownLeft] = 1;
                    m._stepRange[DownRight] = 1;
                    break;

                case PieceIdentity.GoldGeneral:
                case PieceIdentity.ViolentWolf:
                    m._stepRange[Up] = 1;
                    m._stepRange[Down] = 1;
                    m._stepRange[Left] = 1;
                    m._stepRange[Right] = 1;
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[UpRight] = 1;
                    break;

                case PieceIdentity.FerociousLeopard:
                    m._stepRange[Up] = 1;
                    m._stepRange[Down] = 1;
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[UpRight] = 1;
                    m._stepRange[DownLeft] = 1;
                    m._stepRange[DownRight] = 1;
                    break;

                case PieceIdentity.BlindMonkey:
                case PieceIdentity.BlindBear:
                    m._stepRange[Left] = 1;
                    m._stepRange[Right] = 1;
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[UpRight] = 1;
                    m._stepRange[DownLeft] = 1;
                    m._stepRange[DownRight] = 1;
                    break;

                case PieceIdentity.DrunkenElephant:
                case PieceIdentity.NeighboringKing:
                case PieceIdentity.RushingBoar:
                    m._stepRange[Up] = 1;
                    m._stepRange[Left] = 1;
                    m._stepRange[Right] = 1;
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[UpRight] = 1;
                    m._stepRange[DownLeft] = 1;
                    m._stepRange[DownRight] = 1;
                    break;

                case PieceIdentity.Deva:
                    m._stepRange[Up] = 1;
                    m._stepRange[Down] = 1;
                    m._stepRange[Left] = 1;
                    m._stepRange[Right] = 1;
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[DownLeft] = 1;
                    m._stepRange[DownRight] = 1;
                    break;

                case PieceIdentity.DarkSpirit:
                    m._stepRange[Up] = 1;
                    m._stepRange[Down] = 1;
                    m._stepRange[Left] = 1;
                    m._stepRange[Right] = 1;
                    m._stepRange[UpRight] = 1;
                    m._stepRange[DownLeft] = 1;
                    m._stepRange[DownRight] = 1;
                    break;

                case PieceIdentity.BlindTiger:
                    m._stepRange[Down] = 1;
                    m._stepRange[Left] = 1;
                    m._stepRange[Right] = 1;
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[UpRight] = 1;
                    m._stepRange[DownLeft] = 1;
                    m._stepRange[DownRight] = 1;
                    break;

                case PieceIdentity.Prince:
                case PieceIdentity.LeftGeneral:
                case PieceIdentity.RightGeneral:
                case PieceIdentity.BearsEyes:
                case PieceIdentity.VenomousWolf:
                    m._stepRange[Up] = 1;
                    m._stepRange[Down] = 1;
                    m._stepRange[Left] = 1;
                    m._stepRange[Right] = 1;
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[UpRight] = 1;
                    m._stepRange[DownLeft] = 1;
                    m._stepRange[DownRight] = 1;
                    break;

                case PieceIdentity.WoodGeneral:
                    m._stepRange[UpLeft] = 2;
                    m._stepRange[UpRight] = 2;
                    break;

                case PieceIdentity.Donkey:
                case PieceIdentity.EnchantedBadger:
                    m._stepRange[Up] = 2;
                    m._stepRange[Down] = 2;
                    m._stepRange[Left] = 2;
                    m._stepRange[Right] = 2;
                    break;

                case PieceIdentity.FlyingHorse:
                    m._stepRange[UpLeft] = 2;
                    m._stepRange[UpRight] = 2;
                    m._stepRange[DownLeft] = 2;
                    m._stepRange[DownRight] = 2;
                    break;

                case PieceIdentity.BeastCadet:
                    m._stepRange[Up] = 2;
                    m._stepRange[Left] = 2;
                    m._stepRange[Right] = 2;
                    m._stepRange[UpLeft] = 2;
                    m._stepRange[UpRight] = 2;
                    m._stepRange[DownLeft] = 2;
                    m._stepRange[DownRight] = 2;
                    break;

                case PieceIdentity.King:
                    m._stepRange[Up] = 2;
                    m._stepRange[Down] = 2;
                    m._stepRange[Left] = 2;
                    m._stepRange[Right] = 2;
                    m._stepRange[UpLeft] = 2;
                    m._stepRange[UpRight] = 2;
                    m._stepRange[DownLeft] = 2;
                    m._stepRange[DownRight] = 2;
                    break;

                case PieceIdentity.FragrantElephant:
                case PieceIdentity.WhiteElephant:
                    m._stepRange[Up] = 2;
                    m._stepRange[Down] = 2;
                    m._stepRange[Left] = 2;
                    m._stepRange[Right] = 2;
                    m._stepRange[UpLeft] = 2;
                    m._stepRange[UpRight] = 2;
                    m._stepRange[DownLeft] = 2;
                    m._stepRange[DownRight] = 2;
                    break;

                case PieceIdentity.RushingBird:
                    m._stepRange[Up] = 2;
                    m._stepRange[Left] = 1;
                    m._stepRange[Right] = 1;
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[UpRight] = 1;
                    m._stepRange[DownLeft] = 1;
                    m._stepRange[DownRight] = 1;
                    break;

                case PieceIdentity.AngryBoar:
                    m._stepRange[Up] = 1;
                    m._stepRange[Left] = 1;
                    m._stepRange[Right] = 1;
                    m._stepRange[UpLeft] = 2;
                    m._stepRange[UpRight] = 2;
                    break;

                case PieceIdentity.ViolentBear:
                    if ((options & TaiyokuShogiOptions.ViolentBearAlternative) == 0)
                    {
                        m._stepRange[Up] = 1;
                        m._stepRange[Left] = 1;
                        m._stepRange[Right] = 1;
                        m._stepRange[UpLeft] = 2;
                        m._stepRange[UpRight] = 2;
                    }
                    else
                    {
                        m._stepRange[Left] = 1;
                        m._stepRange[Right] = 1;
                        m._stepRange[UpLeft] = 2;
                        m._stepRange[UpRight] = 2;
                        m._stepRange[DownLeft] = 1;
                        m._stepRange[DownRight] = 1;
                    }
                    break;

                case PieceIdentity.EasternBarbarian:
                case PieceIdentity.WesternBarbarian:
                    m._stepRange[Up] = 2;
                    m._stepRange[Down] = 2;
                    m._stepRange[Left] = 1;
                    m._stepRange[Right] = 1;
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[UpRight] = 1;
                    break;

                case PieceIdentity.NorthernBarbarian:
                case PieceIdentity.SouthernBarbarian:
                case PieceIdentity.PrancingStag:
                    m._stepRange[Up] = 1;
                    m._stepRange[Down] = 1;
                    m._stepRange[Left] = 2;
                    m._stepRange[Right] = 2;
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[UpRight] = 1;
                    break;

                case PieceIdentity.PoisonousSnake:
                    m._stepRange[Up] = 2;
                    m._stepRange[Down] = 1;
                    m._stepRange[Left] = 2;
                    m._stepRange[Right] = 2;
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[UpRight] = 1;
                    break;

                case PieceIdentity.OldKite:
                    m._stepRange[Left] = 1;
                    m._stepRange[Right] = 1;
                    m._stepRange[DownLeft] = 2;
                    m._stepRange[DownRight] = 2;
                    m._stepRange[UpLeft] = 2;
                    m._stepRange[UpRight] = 2;
                    break;

                case PieceIdentity.FierceEagle:
                    m._stepRange[Up] = 1;
                    m._stepRange[Left] = 1;
                    m._stepRange[Right] = 1;
                    m._stepRange[DownLeft] = 2;
                    m._stepRange[DownRight] = 2;
                    m._stepRange[UpLeft] = 2;
                    m._stepRange[UpRight] = 2;
                    break;

                case PieceIdentity.GuardianOfTheGods:
                    m._stepRange[Up] = 3;
                    m._stepRange[Down] = 3;
                    m._stepRange[Left] = 3;
                    m._stepRange[Right] = 3;
                    break;

                case PieceIdentity.Wrestler:
                    m._stepRange[UpLeft] = 3;
                    m._stepRange[UpRight] = 3;
                    m._stepRange[DownLeft] = 3;
                    m._stepRange[DownRight] = 3;
                    break;

                case PieceIdentity.CaptiveCadet:
                    m._stepRange[Up] = 3;
                    m._stepRange[Left] = 3;
                    m._stepRange[Right] = 3;
                    m._stepRange[UpLeft] = 3;
                    m._stepRange[UpRight] = 3;
                    m._stepRange[DownLeft] = 3;
                    m._stepRange[DownRight] = 3;
                    break;

                case PieceIdentity.HorseGeneral:
                case PieceIdentity.OxGeneral:
                    m._stepRange[Up] = 3;
                    m._stepRange[Down] = 1;
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[UpRight] = 1;
                    break;

                case PieceIdentity.FireGeneral:
                    m._stepRange[Up] = 3;
                    m._stepRange[Down] = 3;
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[UpRight] = 1;
                    break;

                case PieceIdentity.WaterGeneral:
                    m._stepRange[Up] = 1;
                    m._stepRange[Down] = 1;
                    m._stepRange[UpLeft] = 3;
                    m._stepRange[UpRight] = 3;
                    break;

                case PieceIdentity.BuddhistDevil:
                    m._stepRange[Down] = 1;
                    m._stepRange[Left] = 1;
                    m._stepRange[Right] = 1;
                    m._stepRange[UpLeft] = 3;
                    m._stepRange[UpRight] = 3;
                    break;

                case PieceIdentity.WindGeneral:
                case PieceIdentity.RiverGeneral:
                    m._stepRange[Up] = 3;
                    m._stepRange[Down] = 1;
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[UpRight] = 1;
                    break;

                case PieceIdentity.Yaksha:
                    m._stepRange[Left] = 3;
                    m._stepRange[Right] = 3;
                    m._stepRange[Down] = 1;
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[UpRight] = 1;
                    break;

                case PieceIdentity.SwordGeneral:
                    m._stepRange[Up] = 3;
                    m._stepRange[Down] = 1;
                    m._stepRange[UpLeft] = 3;
                    m._stepRange[UpRight] = 3;
                    break;

                case PieceIdentity.CaptiveOfficer:
                    m._stepRange[Up] = 2;
                    m._stepRange[Left] = 2;
                    m._stepRange[Right] = 2;
                    m._stepRange[UpLeft] = 3;
                    m._stepRange[UpRight] = 3;
                    m._stepRange[DownLeft] = 3;
                    m._stepRange[DownRight] = 3;
                    break;

                case PieceIdentity.BeastOfficer:
                    m._stepRange[Up] = 3;
                    m._stepRange[Left] = 2;
                    m._stepRange[Right] = 2;
                    m._stepRange[UpLeft] = 3;
                    m._stepRange[UpRight] = 3;
                    m._stepRange[DownLeft] = 3;
                    m._stepRange[DownRight] = 3;
                    break;

                case PieceIdentity.HeavenlyTetrarch:
                    m._stepRange[Up] = 4;
                    m._stepRange[Down] = 4;
                    m._stepRange[Left] = 4;
                    m._stepRange[Right] = 4;
                    m._stepRange[UpLeft] = 4;
                    m._stepRange[UpRight] = 4;
                    m._stepRange[DownLeft] = 4;
                    m._stepRange[DownRight] = 4;
                    break;

                case PieceIdentity.ChickenGeneral:
                case PieceIdentity.PupGeneral:
                    m._stepRange[Up] = 4;
                    m._stepRange[DownLeft] = 1;
                    m._stepRange[DownRight] = 1;
                    break;

                case PieceIdentity.PigGeneral:
                    m._stepRange[Down] = 2;
                    m._stepRange[UpLeft] = 4;
                    m._stepRange[UpRight] = 4;
                    break;

                case PieceIdentity.MountainStag:
                    m._stepRange[Up] = 1;
                    m._stepRange[Left] = 2;
                    m._stepRange[Right] = 2;
                    m._stepRange[Down] = 4;
                    m._stepRange[UpLeft] = 3;
                    m._stepRange[UpRight] = 3;
                    break;

                case PieceIdentity.LeopardKing:
                    m._stepRange[Up] = 5;
                    m._stepRange[Down] = 5;
                    m._stepRange[Left] = 5;
                    m._stepRange[Right] = 5;
                    m._stepRange[UpLeft] = 5;
                    m._stepRange[UpRight] = 5;
                    m._stepRange[DownLeft] = 5;
                    m._stepRange[DownRight] = 5;
                    break;

                case PieceIdentity.TurtleDove:
                    m._stepRange[Down] = 1;
                    m._stepRange[Left] = 1;
                    m._stepRange[Right] = 1;
                    m._stepRange[UpLeft] = 5;
                    m._stepRange[UpRight] = 5;
                    break;

                case PieceIdentity.CrossbowSoldier:
                    m._stepRange[Up] = 5;
                    m._stepRange[Down] = 1;
                    m._stepRange[Left] = 3;
                    m._stepRange[Right] = 3;
                    m._stepRange[UpLeft] = 3;
                    m._stepRange[UpRight] = 3;
                    break;

                case PieceIdentity.BurningSoldier:
                    m._stepRange[Up] = 7;
                    m._stepRange[Down] = 1;
                    m._stepRange[Left] = 3;
                    m._stepRange[Right] = 3;
                    m._stepRange[UpLeft] = 5;
                    m._stepRange[UpRight] = 5;
                    break;

                case PieceIdentity.Lance:
                case PieceIdentity.OxCart:
                case PieceIdentity.SavageTiger:
                    m._stepRange[Up] = Unlimited;
                    break;

                case PieceIdentity.ReverseChariot:
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[Down] = Unlimited;
                    break;

                case PieceIdentity.SideDragon:
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[Left] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    break;

                case PieceIdentity.MountainWitch:
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[DownRight] = Unlimited;
                    break;

                case PieceIdentity.WhiteHorse:
                case PieceIdentity.BirdOfParadise:
                case PieceIdentity.MultiGeneral:
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    break;

                case PieceIdentity.Rook:
                case PieceIdentity.Soldier:
                case PieceIdentity.RunningChariot:
                case PieceIdentity.SquareMover:
                case PieceIdentity.GlidingSwallow:
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[Left] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    break;

                case PieceIdentity.FreeSerpent:
                case PieceIdentity.CoiledDragon:
                case PieceIdentity.Whale:
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[DownRight] = Unlimited;
                    break;

                case PieceIdentity.Bishop:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[DownRight] = Unlimited;
                    break;

                case PieceIdentity.FreeWolf:
                case PieceIdentity.RunningLeopard:
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[Left] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    break;

                case PieceIdentity.WizardStork:
                    m._stepRange[Left] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    break;

                case PieceIdentity.FlyingOx:
                case PieceIdentity.FreeBear:
                case PieceIdentity.FreeLeopard:
                case PieceIdentity.GreatWhale:
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[DownRight] = Unlimited;
                    break;

                case PieceIdentity.TreacherousFox:
                    if ((options & TaiyokuShogiOptions.TreacherousFoxAlternative) == 0)
                    {
                        m._stepRange[Up] = Unlimited;
                        m._stepRange[Down] = Unlimited;
                        m._stepRange[UpLeft] = Unlimited;
                        m._stepRange[UpRight] = Unlimited;
                        m._stepRange[DownLeft] = Unlimited;
                        m._stepRange[DownRight] = Unlimited;
                    }
                    else
                    {
                        m._stepRange[Up] = Unlimited;
                        m._stepRange[Down] = Unlimited;
                        m._stepRange[UpLeft] = Unlimited;
                        m._stepRange[UpRight] = Unlimited;
                        m._stepRange[DownLeft] = Unlimited;
                        m._stepRange[DownRight] = Unlimited;

                        m._jumpRange[Up] = (new int[] { 1, 2 }, Unlimited);
                        m._jumpRange[Down] = (new int[] { 1, 2 }, Unlimited);
                        m._jumpRange[UpLeft] = (new int[] { 1, 2 }, Unlimited);
                        m._jumpRange[UpRight] = (new int[] { 1, 2 }, Unlimited);
                        m._jumpRange[DownLeft] = (new int[] { 1, 2 }, Unlimited);
                        m._jumpRange[DownRight] = (new int[] { 1, 2 }, Unlimited);
                    }
                    break;

                case PieceIdentity.Cavalier:
                case PieceIdentity.StrongChariot:
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[Left] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    break;

                case PieceIdentity.FreeDragon:
                case PieceIdentity.FreeTiger:
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[Left] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[DownRight] = Unlimited;
                    break;

                case PieceIdentity.Queen:
                case PieceIdentity.FreeStag:
                case PieceIdentity.StrongEagle:
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[Left] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[DownRight] = Unlimited;
                    break;

                case PieceIdentity.HowlingDog_Left:
                case PieceIdentity.HowlingDog_Right:
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[Down] = 1;
                    break;

                case PieceIdentity.VerticalHorse:
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[UpRight] = 1;
                    m._stepRange[Down] = 1;
                    break;

                case PieceIdentity.SpearSoldier:
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[Left] = 1;
                    m._stepRange[Right] = 1;
                    m._stepRange[Down] = 1;
                    break;

                case PieceIdentity.VerticalPup:
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[DownLeft] = 1;
                    m._stepRange[DownRight] = 1;
                    m._stepRange[Down] = 1;
                    break;

                case PieceIdentity.RaidingFalcon:
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[UpRight] = 1;
                    m._stepRange[Left] = 1;
                    m._stepRange[Right] = 1;
                    break;

                case PieceIdentity.RightIronChariot:
                    m._stepRange[Up] = 1;
                    m._stepRange[Left] = 1;
                    m._stepRange[Right] = 1;
                    m._stepRange[Down] = 1;
                    m._stepRange[DownLeft] = Unlimited;
                    break;

                case PieceIdentity.LeftIronChariot:
                    m._stepRange[Up] = 1;
                    m._stepRange[Left] = 1;
                    m._stepRange[Right] = 1;
                    m._stepRange[Down] = 1;
                    m._stepRange[DownRight] = Unlimited;
                    break;

                case PieceIdentity.VerticalLeopard:
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[Left] = 1;
                    m._stepRange[Right] = 1;
                    m._stepRange[Down] = 1;
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[UpRight] = 1;
                    break;

                case PieceIdentity.RightDog:
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[Down] = 1;
                    m._stepRange[DownLeft] = Unlimited;
                    break;

                case PieceIdentity.LeftDog:
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[Down] = 1;
                    m._stepRange[DownRight] = Unlimited;
                    break;

                case PieceIdentity.RamsHeadSoldier:
                case PieceIdentity.FlyingSwallow:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Down] = 1;
                    break;

                case PieceIdentity.WoodChariot:
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[DownRight] = 1;
                    break;

                case PieceIdentity.TileChariot:
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[UpRight] = 1;
                    m._stepRange[DownLeft] = 1;
                    break;

                case PieceIdentity.RunningBoar:
                case PieceIdentity.RunningPup:
                case PieceIdentity.RunningSerpent:
                case PieceIdentity.EarthChariot:
                case PieceIdentity.VerticalMover:
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[Left] = 1;
                    m._stepRange[Right] = 1;
                    break;

                case PieceIdentity.ViolentOx:
                    m._stepRange[Up] = 1;
                    m._stepRange[Down] = 1;
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    break;

                case PieceIdentity.SideWolf:
                    m._stepRange[Left] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[DownRight] = 1;
                    break;

                case PieceIdentity.SideOx:
                    m._stepRange[Left] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[UpRight] = 1;
                    m._stepRange[DownLeft] = 1;
                    break;

                case PieceIdentity.SideMover:
                    m._stepRange[Up] = 1;
                    m._stepRange[Down] = 1;
                    m._stepRange[Left] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    break;

                case PieceIdentity.SideMonkey:
                    m._stepRange[Down] = 1;
                    m._stepRange[Left] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[UpRight] = 1;
                    break;

                case PieceIdentity.DivineSparrow:
                    if ((options & TaiyokuShogiOptions.DivineSparrowAlternative) == 0)
                    {
                        m._stepRange[Up] = 1;
                        m._stepRange[Down] = 1;
                        m._stepRange[Left] = 1;
                        m._stepRange[Right] = 1;
                        m._stepRange[UpLeft] = Unlimited;
                        m._stepRange[DownLeft] = Unlimited;
                    }
                    else
                    {
                        m._stepRange[Up] = 1;
                        m._stepRange[Down] = 1;
                        m._stepRange[Left] = 1;
                        m._stepRange[Right] = 1;
                        m._stepRange[UpLeft] = Unlimited;
                        m._stepRange[DownLeft] = Unlimited;
                        m._stepRange[DownRight] = Unlimited;
                    }
                    break;


                case PieceIdentity.PloddingOx:
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[UpRight] = 1;
                    m._stepRange[DownLeft] = 1;
                    m._stepRange[DownRight] = 1;
                    break;

                case PieceIdentity.SwallowsWings:
                    m._stepRange[Up] = 1;
                    m._stepRange[Down] = 1;
                    m._stepRange[Left] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[UpRight] = 1;
                    break;

                case PieceIdentity.SideFlier:
                    m._stepRange[Left] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[UpRight] = 1;
                    m._stepRange[DownLeft] = 1;
                    m._stepRange[DownRight] = 1;
                    break;

                case PieceIdentity.FlyingStag:
                case PieceIdentity.CopperElephant:
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[Left] = 1;
                    m._stepRange[Right] = 1;
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[UpRight] = 1;
                    m._stepRange[DownLeft] = 1;
                    m._stepRange[DownRight] = 1;
                    break;

                case PieceIdentity.VermillionSparrow:
                    m._stepRange[Up] = 1;
                    m._stepRange[Down] = 1;
                    m._stepRange[Left] = 1;
                    m._stepRange[Right] = 1;
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[UpRight] = 1;
                    m._stepRange[DownLeft] = 1;
                    m._stepRange[DownRight] = Unlimited;
                    break;

                case PieceIdentity.TurtleSnake:
                    m._stepRange[Up] = 1;
                    m._stepRange[Down] = 1;
                    m._stepRange[Left] = 1;
                    m._stepRange[Right] = 1;
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[DownRight] = 1;
                    break;

                case PieceIdentity.SideBoar:
                    m._stepRange[Up] = 1;
                    m._stepRange[Down] = 1;
                    m._stepRange[Left] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[UpRight] = 1;
                    m._stepRange[DownLeft] = 1;
                    m._stepRange[DownRight] = 1;
                    break;

                case PieceIdentity.LeftChariot:
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[Left] = 1;
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[DownRight] = Unlimited;
                    break;

                case PieceIdentity.RightChariot:
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[Right] = 1;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[DownLeft] = Unlimited;
                    break;

                case PieceIdentity.GreatTiger:
                    m._stepRange[Up] = 1;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[Left] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    break;

                case PieceIdentity.RightTiger:
                    m._stepRange[Left] = Unlimited;
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[UpRight] = 1;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[DownRight] = 1;
                    break;

                case PieceIdentity.LeftTiger:
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[DownLeft] = 1;
                    m._stepRange[DownRight] = Unlimited;
                    break;

                case PieceIdentity.GreatBear:
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[Down] = 1;
                    m._stepRange[Left] = 1;
                    m._stepRange[Right] = 1;
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    break;

                case PieceIdentity.RunningRabbit:
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[Down] = 1;
                    m._stepRange[DownLeft] = 1;
                    m._stepRange[DownRight] = 1;
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    break;

                case PieceIdentity.LeftArmy:
                    m._stepRange[Up] = 1;
                    m._stepRange[Down] = 1;
                    m._stepRange[Left] = Unlimited;
                    m._stepRange[Right] = 1;
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[UpRight] = 1;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[DownRight] = 1;
                    break;

                case PieceIdentity.RightArmy:
                    m._stepRange[Up] = 1;
                    m._stepRange[Down] = 1;
                    m._stepRange[Left] = 1;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[DownLeft] = 1;
                    m._stepRange[DownRight] = Unlimited;
                    break;

                case PieceIdentity.DivineTurtle:
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[Up] = 1;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = 1;
                    m._stepRange[DownRight] = Unlimited;
                    m._stepRange[Down] = 1;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[Left] = 1;
                    break;

                case PieceIdentity.RunningWolf:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = 1;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[Left] = Unlimited;
                    break;

                case PieceIdentity.FlyingFalcon:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = 1;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[DownRight] = Unlimited;
                    m._stepRange[DownLeft] = Unlimited;
                    break;

                case PieceIdentity.BurningChariot:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = 1;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[Left] = 1;
                    break;

                case PieceIdentity.DragonKing:
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = 1;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[DownRight] = 1;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = 1;
                    m._stepRange[Left] = Unlimited;
                    break;

                case PieceIdentity.DragonHorse:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = 1;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = 1;
                    m._stepRange[DownRight] = Unlimited;
                    m._stepRange[Down] = 1;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[Left] = 1;
                    break;

                case PieceIdentity.FreeBoar:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[Down] = 1;
                    m._stepRange[Left] = Unlimited;
                    break;

                case PieceIdentity.WindDragon:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = 1;
                    m._stepRange[DownRight] = Unlimited;
                    m._stepRange[DownLeft] = 1;
                    m._stepRange[Left] = Unlimited;
                    break;

                case PieceIdentity.CloudDragon:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = 1;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = 1;
                    m._stepRange[DownRight] = Unlimited;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[Left] = 1;
                    break;

                case PieceIdentity.RainDragon:
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[Up] = 1;
                    m._stepRange[UpRight] = 1;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[DownRight] = Unlimited;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[Left] = Unlimited;
                    break;

                case PieceIdentity.FireOx:
                case PieceIdentity.ViolentWind:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = 1;
                    m._stepRange[DownRight] = Unlimited;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[Left] = 1;
                    break;

                case PieceIdentity.ChineseRiver:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = 1;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[DownRight] = Unlimited;
                    m._stepRange[Down] = 1;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[Left] = Unlimited;
                    break;

                case PieceIdentity.VerticalTiger:
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[Down] = 2;
                    break;

                case PieceIdentity.WindSnappingTurtle:
                    m._stepRange[UpLeft] = 2;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = 2;
                    m._stepRange[Down] = Unlimited;
                    break;

                case PieceIdentity.RunningTile:
                case PieceIdentity.RunningTiger:
                case PieceIdentity.RunningBear:
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[Right] = 2;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[Left] = 2;
                    break;

                case PieceIdentity.GoldenDeer:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[DownRight] = 2;
                    m._stepRange[DownLeft] = 2;
                    break;

                case PieceIdentity.SilverRabbit:
                    m._stepRange[UpLeft] = 2;
                    m._stepRange[UpRight] = 2;
                    m._stepRange[DownRight] = Unlimited;
                    m._stepRange[DownLeft] = Unlimited;
                    break;

                case PieceIdentity.WalkingHeron:
                    m._stepRange[UpLeft] = 2;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = 2;
                    m._stepRange[Right] = 2;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[Left] = 2;
                    break;

                case PieceIdentity.YoungBird:
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[Right] = 2;
                    m._stepRange[DownRight] = 2;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = 2;
                    m._stepRange[Left] = 2;
                    break;

                case PieceIdentity.RightDragon:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Right] = 2;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[Left] = Unlimited;
                    break;

                case PieceIdentity.LeftDragon:
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[DownRight] = Unlimited;
                    m._stepRange[Left] = 2;
                    break;

                case PieceIdentity.BlueDragon:
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = 2;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[Left] = 2;
                    break;

                case PieceIdentity.WhiteTiger:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = 2;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[Down] = 2;
                    m._stepRange[Left] = Unlimited;
                    break;

                case PieceIdentity.DivineTiger:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[Down] = 2;
                    m._stepRange[Left] = Unlimited;
                    break;

                case PieceIdentity.DivineDragon:
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[Left] = 2;
                    break;

                case PieceIdentity.RunningStag:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[Down] = 2;
                    m._stepRange[Left] = Unlimited;
                    break;

                case PieceIdentity.RearStandard:
                    m._stepRange[UpLeft] = 2;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = 2;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[DownRight] = 2;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = 2;
                    m._stepRange[Left] = Unlimited;
                    break;

                case PieceIdentity.CeramicDove:
                case PieceIdentity.ElephantKing:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = 2;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = 2;
                    m._stepRange[DownRight] = Unlimited;
                    m._stepRange[Down] = 2;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[Left] = 2;
                    break;

                case PieceIdentity.WoodlandDemon:
                case PieceIdentity.FreeChicken:
                case PieceIdentity.Horseman:
                case PieceIdentity.GreatHorse:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = 2;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[Left] = 2;
                    break;

                case PieceIdentity.FreeDog:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = 2;
                    m._stepRange[DownRight] = 2;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = 2;
                    m._stepRange[Left] = 2;
                    break;

                case PieceIdentity.RunningOx:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[DownRight] = 2;
                    m._stepRange[DownLeft] = 2;
                    m._stepRange[Left] = Unlimited;
                    break;

                case PieceIdentity.ChariotSoldier:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = 2;
                    m._stepRange[DownRight] = Unlimited;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[Left] = 2;
                    break;

                case PieceIdentity.FireDemon:
                case PieceIdentity.WaterBuffalo:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = 2;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[DownRight] = Unlimited;
                    m._stepRange[Down] = 2;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[Left] = Unlimited;
                    break;

                case PieceIdentity.StrongBear:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[DownRight] = Unlimited;
                    m._stepRange[Down] = 2;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[Left] = Unlimited;
                    break;

                case PieceIdentity.LiberatedHorse:
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = 1;
                    m._stepRange[Down] = 2;
                    break;

                case PieceIdentity.VerticalBear:
                case PieceIdentity.VerticalSoldier:
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[Right] = 2;
                    m._stepRange[Down] = 1;
                    m._stepRange[Left] = 2;
                    break;

                case PieceIdentity.TigerSoldier:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = 2;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Down] = 1;
                    break;

                case PieceIdentity.EarthDragon:
                    if ((options & TaiyokuShogiOptions.EarthDragonAlternative) == 0)
                    {
                        m._stepRange[UpLeft] = 2;
                        m._stepRange[Up] = 1;
                        m._stepRange[UpRight] = 2;
                        m._stepRange[DownRight] = Unlimited;
                        m._stepRange[DownLeft] = Unlimited;
                    }
                    else
                    {
                        m._stepRange[UpLeft] = 1;
                        m._stepRange[Up] = 2;
                        m._stepRange[UpRight] = 1;
                        m._stepRange[Down] = 1;
                        m._stepRange[DownRight] = Unlimited;
                        m._stepRange[DownLeft] = Unlimited;
                    }
                    break;

                case PieceIdentity.SilverChariot:
                    m._stepRange[UpLeft] = 2;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = 2;
                    m._stepRange[DownRight] = 1;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = 1;
                    break;

                case PieceIdentity.StoneChariot:
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = 1;
                    m._stepRange[Right] = 2;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[Left] = 2;
                    break;

                case PieceIdentity.SideSoldier:
                    m._stepRange[Up] = 2;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[Down] = 1;
                    m._stepRange[Left] = Unlimited;
                    break;

                case PieceIdentity.GoldChariot:
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = 1;
                    m._stepRange[Right] = 2;
                    m._stepRange[DownRight] = 1;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = 1;
                    m._stepRange[Left] = 2;
                    break;

                case PieceIdentity.BoarSoldier:
                case PieceIdentity.LeopardSoldier:
                case PieceIdentity.BearSoldier:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = 2;
                    m._stepRange[Down] = 1;
                    m._stepRange[Left] = 2;
                    break;

                case PieceIdentity.FreePup:
                case PieceIdentity.FreeOx:
                case PieceIdentity.FreeHorse:
                case PieceIdentity.FreePig:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = 2;
                    m._stepRange[DownRight] = 1;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = 1;
                    m._stepRange[Left] = 2;
                    break;

                case PieceIdentity.LittleStandard:
                    m._stepRange[UpLeft] = 2;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = 2;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[DownRight] = 1;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = 1;
                    m._stepRange[Left] = Unlimited;
                    break;

                case PieceIdentity.CopperChariot:
                    m._stepRange[UpLeft] = 3;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = 3;
                    m._stepRange[Down] = Unlimited;
                    break;

                case PieceIdentity.ForestDemon:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = 3;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = 3;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[Left] = 3;
                    break;

                case PieceIdentity.GreatDragon:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = 3;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[DownRight] = Unlimited;
                    m._stepRange[Down] = 3;
                    m._stepRange[DownLeft] = Unlimited;
                    break;

                case PieceIdentity.CenterStandard:
                case PieceIdentity.FrontStandard:
                    m._stepRange[UpLeft] = 3;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = 3;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[DownRight] = 3;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = 3;
                    m._stepRange[Left] = Unlimited;
                    break;

                case PieceIdentity.GreatDove:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = 3;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = 3;
                    m._stepRange[DownRight] = Unlimited;
                    m._stepRange[Down] = 3;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[Left] = 3;
                    break;

                case PieceIdentity.GreatStandard:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[DownRight] = 3;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = 3;
                    m._stepRange[Left] = Unlimited;
                    break;

                case PieceIdentity.VerticalWolf:
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[Right] = 1;
                    m._stepRange[Down] = 3;
                    m._stepRange[Left] = 1;
                    break;

                case PieceIdentity.SideSerpent:
                    m._stepRange[Up] = 3;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[Down] = 1;
                    m._stepRange[Left] = Unlimited;
                    break;

                case PieceIdentity.CloudEagle:
                    m._stepRange[UpLeft] = 3;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = 3;
                    m._stepRange[Right] = 1;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[Left] = 1;
                    break;

                case PieceIdentity.GooseWing:
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = 1;
                    m._stepRange[Right] = 3;
                    m._stepRange[DownRight] = 1;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = 1;
                    m._stepRange[Left] = 3;
                    break;

                case PieceIdentity.HorseSoldier:
                case PieceIdentity.OxSoldier:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = 3;
                    m._stepRange[Down] = 1;
                    m._stepRange[Left] = 3;
                    break;

                case PieceIdentity.SpearGeneral:
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[Right] = 3;
                    m._stepRange[Down] = 2;
                    m._stepRange[Left] = 3;
                    break;

                case PieceIdentity.BurningGeneral:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = 3;
                    m._stepRange[Down] = 2;
                    m._stepRange[Left] = 3;
                    break;

                case PieceIdentity.BeastBird:
                case PieceIdentity.CaptiveBird:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = 3;
                    m._stepRange[DownRight] = Unlimited;
                    m._stepRange[Down] = 2;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[Left] = 3;
                    break;

                case PieceIdentity.LongbowSoldier:
                case PieceIdentity.GreatLeopard:
                    m._stepRange[UpLeft] = 3;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = 3;
                    m._stepRange[Right] = 2;
                    m._stepRange[Down] = 1;
                    m._stepRange[Left] = 2;
                    break;

                case PieceIdentity.ThunderRunner:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = 4;
                    m._stepRange[Down] = 4;
                    m._stepRange[Left] = 4;
                    break;

                case PieceIdentity.FireDragon:
                    m._stepRange[UpLeft] = 4;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = 4;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[DownRight] = 2;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = 2;
                    m._stepRange[Left] = Unlimited;
                    break;

                case PieceIdentity.WaterDragon:
                    m._stepRange[UpLeft] = 2;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = 2;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[DownRight] = 4;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = 4;
                    m._stepRange[Left] = Unlimited;
                    break;

                case PieceIdentity.LongbowGeneral:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = 5;
                    m._stepRange[DownRight] = Unlimited;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[Left] = 5;
                    break;

                case PieceIdentity.RightPhoenix:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = 5;
                    m._stepRange[DownRight] = Unlimited;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[Left] = 5;
                    break;

                case PieceIdentity.PeacefulMountain:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = 5;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = 5;
                    m._stepRange[DownRight] = Unlimited;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[Left] = 5;
                    break;

                case PieceIdentity.FreeDemon:
                    if ((options & TaiyokuShogiOptions.FreeDemonAlternative) == 0)
                    {
                        m._stepRange[UpLeft] = Unlimited;
                        m._stepRange[UpRight] = Unlimited;
                        m._stepRange[Right] = Unlimited;
                        m._stepRange[DownRight] = Unlimited;
                        m._stepRange[Down] = 5;
                        m._stepRange[DownLeft] = Unlimited;
                    }
                    else
                    {
                        m._stepRange[UpLeft] = Unlimited;
                        m._stepRange[Up] = 5;
                        m._stepRange[UpRight] = Unlimited;
                        m._stepRange[Right] = Unlimited;
                        m._stepRange[DownRight] = Unlimited;
                        m._stepRange[Down] = 5;
                        m._stepRange[DownLeft] = Unlimited;
                        m._stepRange[Left] = Unlimited;
                    }
                    break;

                case PieceIdentity.FreeDreamEater:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = 5;
                    m._stepRange[DownRight] = Unlimited;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[Left] = 5;
                    break;

                case PieceIdentity.FreeFire:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = 5;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[DownRight] = Unlimited;
                    m._stepRange[Down] = 5;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[Left] = Unlimited;
                    break;

                case PieceIdentity.RunningDragon:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[DownRight] = Unlimited;
                    m._stepRange[Down] = 5;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[Left] = Unlimited;
                    break;

                case PieceIdentity.GreatShark:
                    m._stepRange[UpLeft] = 5;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = 5;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[DownRight] = 2;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = 2;
                    m._stepRange[Left] = Unlimited;
                    break;

                case PieceIdentity.CrossbowGeneral:
                    m._stepRange[UpLeft] = 5;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = 5;
                    m._stepRange[Right] = 3;
                    m._stepRange[Down] = 2;
                    m._stepRange[Left] = 3;
                    break;

                case PieceIdentity.PlayfulCockatoo:
                    m._stepRange[UpLeft] = 3;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = 3;
                    m._stepRange[Right] = 5;
                    m._stepRange[DownRight] = 2;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = 2;
                    m._stepRange[Left] = 5;
                    break;

                case PieceIdentity.Knight:
                    m._jumpRange[UpUpLeft] = (new int[] { 1 }, 0);
                    m._jumpRange[UpUpRight] = (new int[] { 1 }, 0);
                    break;

                case PieceIdentity.FlyingDragon:
                    m._jumpRange[UpLeft] = (new int[] { 1 }, 0);
                    m._jumpRange[UpRight] = (new int[] { 1 }, 0);
                    m._jumpRange[DownLeft] = (new int[] { 1 }, 0);
                    m._jumpRange[DownRight] = (new int[] { 1 }, 0);
                    break;

                case PieceIdentity.Kirin:
                    m._stepRange[UpLeft] = 1;
                    m._stepRange[UpRight] = 1;
                    m._stepRange[DownRight] = 1;
                    m._stepRange[DownLeft] = 1;

                    m._jumpRange[Left] = (new int[] { 1 }, 0);
                    m._jumpRange[Right] = (new int[] { 1 }, 0);
                    break;

                case PieceIdentity.Phoenix:
                    m._stepRange[Up] = 1;
                    m._stepRange[Right] = 1;
                    m._stepRange[Down] = 1;
                    m._stepRange[Left] = 1;

                    m._jumpRange[UpLeft] = (new int[] { 1 }, 0);
                    m._jumpRange[UpRight] = (new int[] { 1 }, 0);
                    m._jumpRange[DownRight] = (new int[] { 1 }, 0);
                    m._jumpRange[DownLeft] = (new int[] { 1 }, 0);
                    break;

                case PieceIdentity.FlyingCat:
                    m._stepRange[DownRight] = 1;
                    m._stepRange[Down] = 1;
                    m._stepRange[DownLeft] = 1;

                    m._jumpRange[UpLeft] = (new int[] { 2 }, 0);
                    m._jumpRange[Up] = (new int[] { 2 }, 0);
                    m._jumpRange[UpRight] = (new int[] { 2 }, 0);
                    m._jumpRange[Right] = (new int[] { 2 }, 0);
                    m._jumpRange[Left] = (new int[] { 2 }, 0);
                    break;

                case PieceIdentity.RunningHorse:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;

                    m._jumpRange[DownRightRight] = (new int[] { 1 }, 0);
                    m._jumpRange[DownLeftLeft] = (new int[] { 1 }, 0);
                    break;

                case PieceIdentity.MountainFalcon:
                    // from https://www.chessvariants.com/shogivariants.dir/taikyoku_english.html
                    // the English language Wikipedia page differs a bit and presents two alternatives
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = 2;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[DownRight] = 2;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = 2;
                    m._stepRange[Left] = Unlimited;

                    m._jumpRange[Up] = (new int[] { 1 }, Unlimited);
                    break;

                case PieceIdentity.LittleTurtle:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = 2;
                    m._stepRange[DownRight] = Unlimited;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[Left] = 2;

                    m._jumpRange[Up] = (new int[] { 1 }, 0);
                    m._jumpRange[Down] = (new int[] { 1 }, 0);
                    break;

                case PieceIdentity.GreatStag:
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[DownRight] = 2;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = 2;
                    m._stepRange[Left] = Unlimited;

                    m._jumpRange[UpLeft] = (new int[] { 1 }, 0);
                    m._jumpRange[UpRight] = (new int[] { 1 }, 0);
                    break;

                case PieceIdentity.MountainEagle_Left:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[DownRight] = 2;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[Left] = Unlimited;

                    m._jumpRange[UpLeft] = (new int[] { 1 }, 0);
                    m._jumpRange[DownLeft] = (new int[] { 1 }, 0);
                    break;

                case PieceIdentity.MountainEagle_Right:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[DownRight] = Unlimited;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = 2;
                    m._stepRange[Left] = Unlimited;

                    m._jumpRange[UpRight] = (new int[] { 1 }, 0);
                    m._jumpRange[DownRight] = (new int[] { 1 }, 0);
                    break;

                case PieceIdentity.KirinMaster:
                case PieceIdentity.GreatTurtle:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = 3;
                    m._stepRange[DownRight] = Unlimited;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[Left] = 3;

                    m._jumpRange[Up] = (new int[] { 2 }, 0);
                    m._jumpRange[Down] = (new int[] { 2 }, 0);
                    break;

                case PieceIdentity.PhoenixMaster:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = 3;
                    m._stepRange[DownRight] = Unlimited;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[Left] = 3;

                    m._jumpRange[UpLeft] = (new int[] { 2 }, 0);
                    m._jumpRange[UpRight] = (new int[] { 2 }, 0);
                    break;

                case PieceIdentity.GreatMaster:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = 5;
                    m._stepRange[DownRight] = 5;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = 5;
                    m._stepRange[Left] = 5;

                    m._jumpRange[UpLeft] = (new int[] { 2 }, 0);
                    m._jumpRange[Up] = (new int[] { 2 }, 0);
                    m._jumpRange[UpRight] = (new int[] { 2 }, 0);
                    break;

                case PieceIdentity.HornedFalcon:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[DownRight] = Unlimited;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[Left] = Unlimited;

                    m._jumpRange[Up] = (new int[] { 1 }, 0);
                    break;

                case PieceIdentity.SoaringEagle:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[DownRight] = Unlimited;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[Left] = Unlimited;

                    m._jumpRange[UpLeft] = (new int[] { 1 }, 0);
                    m._jumpRange[UpRight] = (new int[] { 1 }, 0);
                    break;

                case PieceIdentity.RoaringDog:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[DownRight] = 3;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = 3;
                    m._stepRange[Left] = Unlimited;

                    m._jumpRange[Up] = (new int[] { 2 }, 0);
                    m._jumpRange[Right] = (new int[] { 2 }, 0);
                    m._jumpRange[Down] = (new int[] { 2 }, 0);
                    m._jumpRange[Left] = (new int[] { 2 }, 0);
                    break;

                case PieceIdentity.LionDog:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[DownRight] = Unlimited;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[Left] = Unlimited;

                    m._jumpRange[UpLeft] = (new int[] { 2 }, 0);
                    m._jumpRange[Up] = (new int[] { 2 }, 0);
                    m._jumpRange[UpRight] = (new int[] { 2 }, 0);
                    m._jumpRange[Right] = (new int[] { 2 }, 0);
                    m._jumpRange[DownRight] = (new int[] { 2 }, 0);
                    m._jumpRange[Down] = (new int[] { 2 }, 0);
                    m._jumpRange[DownLeft] = (new int[] { 2 }, 0);
                    m._jumpRange[Left] = (new int[] { 2 }, 0);
                    break;

                case PieceIdentity.GreatDreamEater:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[DownRight] = Unlimited;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[Left] = Unlimited;

                    m._jumpRange[Right] = (new int[] { 2 }, 0);
                    m._jumpRange[Left] = (new int[] { 2 }, 0);
                    break;

                case PieceIdentity.HeavenlyHorse:
                    m._stepRange[UpLeft] = Unlimited;

                    m._jumpRange[UpUpLeft] = (new int[] { 1 }, 0);
                    m._jumpRange[UpUpRight] = (new int[] { 1 }, 0);
                    m._jumpRange[DownDownLeft] = (new int[] { 1 }, 0);
                    m._jumpRange[DownDownRight] = (new int[] { 1 }, 0);
                    break;

                case PieceIdentity.SpiritTurtle:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[DownRight] = Unlimited;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[Left] = Unlimited;

                    m._jumpRange[Up] = (new int[] { 2 }, 0);
                    m._jumpRange[Right] = (new int[] { 2 }, 0);
                    m._jumpRange[Down] = (new int[] { 2 }, 0);
                    m._jumpRange[Left] = (new int[] { 2 }, 0);
                    break;

                case PieceIdentity.TreasureTurtle:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[DownRight] = Unlimited;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[Left] = Unlimited;

                    m._jumpRange[Up] = (new int[] { 1 }, 0);
                    m._jumpRange[Right] = (new int[] { 1 }, 0);
                    m._jumpRange[Down] = (new int[] { 1 }, 0);
                    m._jumpRange[Left] = (new int[] { 1 }, 0);
                    break;

                case PieceIdentity.WoodenDove:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = 2;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = 2;
                    m._stepRange[DownRight] = Unlimited;
                    m._stepRange[Down] = 2;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[Left] = 2;

                    m._jumpRange[UpLeft] = (new int[] { 2 }, 2);
                    m._jumpRange[UpRight] = (new int[] { 2 }, 2);
                    m._jumpRange[DownRight] = (new int[] { 2 }, 2);
                    m._jumpRange[DownLeft] = (new int[] { 2 }, 2);
                    break;

                case PieceIdentity.CenterMaster:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = 3;
                    m._stepRange[DownRight] = 3;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = 3;
                    m._stepRange[Left] = 3;

                    m._jumpRange[UpLeft] = (new int[] { 1 }, Unlimited);
                    m._jumpRange[Up] = (new int[] { 1 }, Unlimited);
                    m._jumpRange[UpRight] = (new int[] { 1 }, Unlimited);
                    m._jumpRange[Down] = (new int[] { 1 }, Unlimited);
                    break;

                case PieceIdentity.RocMaster:
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[Right] = 5;
                    m._stepRange[DownRight] = 5;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = 5;
                    m._stepRange[Left] = 5;

                    m._jumpRange[UpLeft] = (new int[] { 2 }, Unlimited);
                    m._jumpRange[UpRight] = (new int[] { 2 }, Unlimited);
                    break;

                case PieceIdentity.FreeEagle:
                    // Additional FreeEagle moves are listed @ https://www.chessvariants.com/shogivariants.dir/taikyoku_english.html
                    // These are not implemented

                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[DownRight] = Unlimited;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[Left] = Unlimited;

                    m._jumpRange[UpLeft] = (new int[] { 1, 2, 3 }, Unlimited);
                    m._jumpRange[Up] = (new int[] { 1, 2 }, Unlimited);
                    m._jumpRange[UpRight] = (new int[] { 1, 2, 3 }, Unlimited);
                    m._jumpRange[Right] = (new int[] { 1, 2 }, Unlimited);
                    m._jumpRange[DownRight] = (new int[] { 1, 2 }, Unlimited);
                    m._jumpRange[Down] = (new int[] { 1, 2 }, Unlimited);
                    m._jumpRange[DownLeft] = (new int[] { 1, 2 }, Unlimited);
                    m._jumpRange[Left] = (new int[] { 1, 2 }, Unlimited);
                    break;

                case PieceIdentity.FreeBird:
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[DownRight] = 3;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = 3;
                    m._stepRange[Left] = Unlimited;

                    m._jumpRange[UpLeft] = (new int[] { 1, 2, }, Unlimited);
                    m._jumpRange[UpRight] = (new int[] { 1, 2 }, Unlimited);
                    break;

                case PieceIdentity.GreatFalcon:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[DownRight] = Unlimited;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[Left] = Unlimited;

                    m._jumpRange[Up] = (new int[] { 1 }, Unlimited);
                    m._jumpRange[Down] = (new int[] { 1 }, Unlimited);
                    break;

                case PieceIdentity.TeachingKing:
                    m._jumpRange[UpLeft] = (new int[] { 1, 2, }, Unlimited);
                    m._jumpRange[Up] = (new int[] { 1, 2 }, Unlimited);
                    m._jumpRange[UpRight] = (new int[] { 1, 2 }, Unlimited);
                    m._jumpRange[Right] = (new int[] { 1, 2 }, Unlimited);
                    m._jumpRange[DownRight] = (new int[] { 1, 2 }, Unlimited);
                    m._jumpRange[Down] = (new int[] { 1, 2 }, Unlimited);
                    m._jumpRange[DownLeft] = (new int[] { 1, 2 }, Unlimited);
                    m._jumpRange[Left] = (new int[] { 1, 2 }, Unlimited);
                    break;

                case PieceIdentity.MountainCrane:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[DownRight] = Unlimited;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[Left] = Unlimited;

                    m._jumpRange[UpLeft] = (new int[] { 1, 2 }, Unlimited);
                    m._jumpRange[Up] = (new int[] { 1, 2 }, Unlimited);
                    m._jumpRange[UpRight] = (new int[] { 1, 2 }, Unlimited);
                    m._jumpRange[Right] = (new int[] { 1, 2 }, Unlimited);
                    m._jumpRange[DownRight] = (new int[] { 1, 2 }, Unlimited);
                    m._jumpRange[Down] = (new int[] { 1, 2 }, Unlimited);
                    m._jumpRange[DownLeft] = (new int[] { 1, 2 }, Unlimited);
                    m._jumpRange[Left] = (new int[] { 1, 2 }, Unlimited);
                    break;

                case PieceIdentity.GreatEagle:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[DownRight] = Unlimited;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[Left] = Unlimited;

                    m._jumpRange[UpLeft] = (new int[] { 1 }, Unlimited);
                    m._jumpRange[Up] = (new int[] { 1 }, Unlimited);
                    m._jumpRange[UpRight] = (new int[] { 1 }, Unlimited);
                    m._jumpRange[Right] = (new int[] { 1 }, Unlimited);
                    m._jumpRange[DownRight] = (new int[] { 1 }, Unlimited);
                    m._jumpRange[Down] = (new int[] { 1 }, Unlimited);
                    m._jumpRange[DownLeft] = (new int[] { 1 }, Unlimited);
                    m._jumpRange[Left] = (new int[] { 1 }, Unlimited);
                    break;

                case PieceIdentity.GreatElephant:
                    m._stepRange[UpLeft] = 3;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = 3;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[DownRight] = Unlimited;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[Left] = Unlimited;

                    m._jumpRange[Up] = (new int[] { 1, 2 }, Unlimited);
                    m._jumpRange[Right] = (new int[] { 1, 2 }, Unlimited);
                    m._jumpRange[DownRight] = (new int[] { 1, 2 }, Unlimited);
                    m._jumpRange[Down] = (new int[] { 1, 2 }, Unlimited);
                    m._jumpRange[DownLeft] = (new int[] { 1, 2 }, Unlimited);
                    m._jumpRange[Left] = (new int[] { 1, 2 }, Unlimited);
                    break;

                case PieceIdentity.GoldenBird:
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[Right] = 3;
                    m._stepRange[DownRight] = 3;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = 3;
                    m._stepRange[Left] = 3;

                    m._jumpRange[UpLeft] = (new int[] { 1, 2 }, Unlimited);
                    m._jumpRange[UpRight] = (new int[] { 1, 2 }, Unlimited);
                    break;

                case PieceIdentity.AncientDragon:
                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[DownRight] = Unlimited;
                    m._stepRange[DownLeft] = Unlimited;

                    m._jumpRange[Up] = (UnlimitedJump, 0);
                    m._jumpRange[Down] = (UnlimitedJump, 0);
                    break;

                case PieceIdentity.RainDemon:
                    m._stepRange[Up] = 3;
                    m._stepRange[Right] = 2;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[Left] = 2;

                    m._jumpRange[UpLeft] = (UnlimitedJump, 0);
                    m._jumpRange[UpRight] = (UnlimitedJump, 0);
                    break;

                case PieceIdentity.RookGeneral:
                    m._rangeCapture[Up] = true;
                    m._rangeCapture[Right] = true;
                    m._rangeCapture[Down] = true;
                    m._rangeCapture[Left] = true;
                    break;

                case PieceIdentity.BishopGeneral:
                    m._rangeCapture[UpLeft] = true;
                    m._rangeCapture[UpRight] = true;
                    m._rangeCapture[DownRight] = true;
                    m._rangeCapture[DownLeft] = true;
                    break;

                case PieceIdentity.GreatGeneral:
                    m._rangeCapture[UpLeft] = true;
                    m._rangeCapture[Up] = true;
                    m._rangeCapture[UpRight] = true;
                    m._rangeCapture[Right] = true;
                    m._rangeCapture[DownRight] = true;
                    m._rangeCapture[Down] = true;
                    m._rangeCapture[DownLeft] = true;
                    m._rangeCapture[Left] = true;
                    break;

                case PieceIdentity.ViolentDragon:
                    m._stepRange[Up] = 2;
                    m._stepRange[Right] = 2;
                    m._stepRange[Down] = 2;
                    m._stepRange[Left] = 2;
                    m._rangeCapture[UpLeft] = true;
                    m._rangeCapture[UpRight] = true;
                    m._rangeCapture[DownRight] = true;
                    m._rangeCapture[DownLeft] = true;
                    break;

                case PieceIdentity.FlyingCrocodile:
                    m._stepRange[UpLeft] = 3;
                    m._stepRange[UpRight] = 3;
                    m._stepRange[DownRight] = 2;
                    m._stepRange[DownLeft] = 2;
                    m._rangeCapture[Up] = true;
                    m._rangeCapture[Right] = true;
                    m._rangeCapture[Down] = true;
                    m._rangeCapture[Left] = true;
                    break;

                case PieceIdentity.ViceGeneral:
                    m._jumpRange[Up] = (new int[] { 1 }, 0);
                    m._jumpRange[Right] = (new int[] { 1 }, 0);
                    m._jumpRange[Down] = (new int[] { 1 }, 0);
                    m._jumpRange[Left] = (new int[] { 1 }, 0);
                    m._rangeCapture[UpLeft] = true;
                    m._rangeCapture[UpRight] = true;
                    m._rangeCapture[DownRight] = true;
                    m._rangeCapture[DownLeft] = true;
                    break;

                case PieceIdentity.HookMover:
                    m._hookMove = HookType.Orthogonal;
                    break;

                case PieceIdentity.LongNosedGoblin:
                    m._hookMove = HookType.Diagonal;
                    if ((options & TaiyokuShogiOptions.LongNosedGoblinAlternative) != 0)
                    {
                        m._stepRange[Up] = 1;
                        m._stepRange[Right] = 1;
                        m._stepRange[Down] = 1;
                        m._stepRange[Left] = 1;
                    }
                    break;

                case PieceIdentity.Capricorn:
                    m._hookMove = HookType.Diagonal;
                    if ((options & TaiyokuShogiOptions.CapricornAlternative) != 0)
                    {
                        m._stepRange[Up] = 1;
                        m._stepRange[Right] = 1;
                        m._stepRange[Down] = 1;
                        m._stepRange[Left] = 1;
                    }
                    break;

                case PieceIdentity.Peacock:
                    m._hookMove = HookType.ForwardDiagnal;
                    m._stepRange[DownRight] = 2;
                    m._stepRange[DownLeft] = 2;
                    break;

                case PieceIdentity.HeavenlyTetrarchKing:
                    m._igui = true;

                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[DownRight] = Unlimited;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[Left] = Unlimited;

                    m._jumpRange[UpLeft] = (new int[] { 1 }, Unlimited);
                    m._jumpRange[Up] = (new int[] { 1 }, Unlimited);
                    m._jumpRange[UpRight] = (new int[] { 1 }, Unlimited);
                    m._jumpRange[Right] = (new int[] { 1 }, Unlimited);
                    m._jumpRange[DownRight] = (new int[] { 1 }, Unlimited);
                    m._jumpRange[Down] = (new int[] { 1 }, Unlimited);
                    m._jumpRange[DownLeft] = (new int[] { 1 }, Unlimited);
                    m._jumpRange[Left] = (new int[] { 1 }, Unlimited);

                    break;

                case PieceIdentity.Lion:
                    m._lionMove = true;
                    m._igui = true;
                    break;

                case PieceIdentity.FuriousFiend:
                    m._lionMove = true;
                    m._igui = true;

                    m._stepRange[UpLeft] = 3;
                    m._stepRange[Up] = 3;
                    m._stepRange[UpRight] = 3;
                    m._stepRange[Right] = 3;
                    m._stepRange[DownRight] = 3;
                    m._stepRange[Down] = 3;
                    m._stepRange[DownLeft] = 3;
                    m._stepRange[Left] = 3;
                    break;

                case PieceIdentity.BuddhistSpirit:
                    m._lionMove = true;
                    m._igui = true;

                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[Up] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[Right] = Unlimited;
                    m._stepRange[DownRight] = Unlimited;
                    m._stepRange[Down] = Unlimited;
                    m._stepRange[DownLeft] = Unlimited;
                    m._stepRange[Left] = Unlimited;
                    break;

                case PieceIdentity.LionHawk:
                    m._lionMove = true;
                    m._igui = true;

                    m._stepRange[UpLeft] = Unlimited;
                    m._stepRange[UpRight] = Unlimited;
                    m._stepRange[DownRight] = Unlimited;
                    m._stepRange[DownLeft] = Unlimited;

                    m._jumpRange[UpLeft] = (new int[] { 1 }, Unlimited);
                    m._jumpRange[UpRight] = (new int[] { 1 }, Unlimited);
                    m._jumpRange[DownRight] = (new int[] { 1 }, Unlimited);
                    m._jumpRange[DownLeft] = (new int[] { 1 }, Unlimited);
                    break;
            }

            return m;
        }

        public static (int X, int Y)? ComputeMove((int X, int Y) loc, int direction, int distance)
        {
            var (x, y) = direction switch
            {
                Up => (loc.X, loc.Y - distance),
                Down => (loc.X, loc.Y + distance),
                Left => (loc.X - distance, loc.Y),
                Right => (loc.X + distance, loc.Y),
                UpLeft => (loc.X - distance, loc.Y - distance),
                UpRight => (loc.X + distance, loc.Y - distance),
                DownLeft => (loc.X - distance, loc.Y + distance),
                DownRight => (loc.X + distance, loc.Y + distance),
                UpUpLeft => (loc.X - (distance / 2), loc.Y - distance),
                UpUpRight => (loc.X + (distance / 2), loc.Y - distance),
                UpLeftLeft => (loc.X - distance, loc.Y - (distance / 2)),
                UpRightRight => (loc.X + distance, loc.Y - (distance / 2)),
                DownDownLeft => (loc.X - (distance / 2), loc.Y + distance),
                DownDownRight => (loc.X + (distance / 2), loc.Y + distance),
                DownLeftLeft => (loc.X - distance, loc.Y + (distance / 2)),
                DownRightRight => (loc.X + distance, loc.Y + (distance / 2)),
                _ => throw new NotSupportedException()
            };

            if (y < 0 || y >= TaiyokuShogi.BoardHeight)
                return null;
            if (x < 0 || x >= TaiyokuShogi.BoardWidth)
                return null;

            return (x, y);
        }

        public static PromotionType CheckPromotion(Piece piece, (int X, int Y) startLoc, (int X, int Y) endLoc, bool _capture /* ignored in Taikyoku Shogi */)
        {
            if (!piece.CanPromote())
                return PromotionType.None;

            return piece.Owner switch
            {
                Player.Black => endLoc.Y < 11 ? PromotionType.May : PromotionType.None,
                Player.White => endLoc.Y > TaiyokuShogi.BoardHeight - 11 ? PromotionType.May : PromotionType.None,
                _ => throw new NotSupportedException()
            };
        }
    }

    public enum HookType
    {
        Orthogonal,
        Diagonal,
        ForwardDiagnal
    };

    public enum MoveType
    {
        Standard, Jump, RangedCapture, Igui, Hook, Area
    }

    public enum PromotionType
    {
        None, May, Must
    }


    internal class MoveGenerator
    {
        private TaiyokuShogi Game { get; }

        private Piece Piece { get; }

        private (int X, int Y) StartLoc { get; }

        private List<((int X, int Y) Loc, MoveType Type, PromotionType Promotion)> _moves = new List<((int X, int Y) Loc, MoveType Type, PromotionType Promotion)>();
        public IEnumerable<((int X, int Y) Loc, MoveType Type, PromotionType Promotion)> Moves { get => _moves; }

        public MoveGenerator(TaiyokuShogi game, Piece piece, (int X, int Y) startLoc)
            => (Game, Piece, StartLoc) = (game, piece, startLoc);

        // Compute a target move and add it to the list of moves if it is legal. Returns the computed location regardless
        public (int X, int Y)? Add((int X, int Y) startLoc, int direction, int distance, MoveType type, bool captureOnly = false)
        {
            var targetLoc = Movement.ComputeMove(startLoc, direction, Piece.Owner == Player.Black ? distance : -distance);

            if (!targetLoc.HasValue)
                return null;

            var existingPiece = Game.GetPiece(targetLoc.Value);
            if (existingPiece != null)
            {
                if (existingPiece?.Owner != Piece.Owner || (targetLoc == StartLoc && (type == MoveType.Area || type == MoveType.Igui)))
                {
                    _moves.Add((targetLoc.Value, type, Movement.CheckPromotion(Piece, startLoc, targetLoc.Value, true)));
                }
            }
            else if (!captureOnly)
            {
                _moves.Add((targetLoc.Value, type, Movement.CheckPromotion(Piece, startLoc, targetLoc.Value, false)));
            }

            return targetLoc;
        }

        // add all legal moves in the given range
        public void AddRange((int X, int Y) loc, int direction, int range, MoveType type)
        {
            bool captureOnly = false;

            for (int i = 1; i <= range; ++i)
            {
                var move = Add(loc, direction, i, type, captureOnly);
                if (move == null)
                    break;

                var existingPiece = Game.GetPiece(move.Value);

                if (type == MoveType.Hook && existingPiece == null)
                {
                    var (left, right) = Rotate90(direction);
                    AddRange(move.Value, left, Movement.Unlimited, MoveType.Standard);
                    AddRange(move.Value, right, Movement.Unlimited, MoveType.Standard);
                }
                else if (type == MoveType.RangedCapture)
                {
                    if (existingPiece != null)
                    {
                        captureOnly = true;
                        if (Piece.Rank > existingPiece.Rank)
                            break;
                    }
                }
                else if (type != MoveType.Jump && existingPiece != null)
                {
                    break;
                }
            }
        }

        private static (int Left, int Right) Rotate90(int direction) =>
            direction switch
            {
                Movement.Up => (Movement.Left, Movement.Right),
                Movement.Down => (Movement.Right, Movement.Left),
                Movement.Left => (Movement.Down, Movement.Up),
                Movement.Right => (Movement.Up, Movement.Down),
                Movement.UpLeft => (Movement.DownLeft, Movement.UpRight),
                Movement.UpRight => (Movement.UpLeft, Movement.DownRight),
                Movement.DownLeft => (Movement.DownRight, Movement.UpLeft),
                Movement.DownRight => (Movement.UpRight, Movement.DownLeft),
                _ => throw new NotSupportedException()
            };
    }


    public static class TaiyokuShogiMoves
    {
        public static Movement GetMovement(this TaiyokuShogi game, PieceIdentity id) => Movement.GetMovement(id, game.Options);

        public static IEnumerable<((int X, int Y) Loc, MoveType Type, PromotionType Promotion)> GetLegalMoves(this TaiyokuShogi game, Piece piece, (int X, int Y) loc, (int X, int Y)? midLoc = null)
        {
            var legalMoves = new MoveGenerator(game, piece, loc);
            var movement = game.GetMovement(piece.Id);

            if (midLoc != null)
            {
                // An area-move (double-capture) or Igui (capture-in-place)

                if (movement.LionMove)
                {
                    for (int i = 0; i < Movement.DirectionCount; ++i)
                    {
                        legalMoves.Add(midLoc.Value, i, 1, MoveType.Area);
                    }
                    legalMoves.Add(midLoc.Value, Movement.Up, 0, MoveType.Area);
                }

                if (movement.Igui)
                {
                    // back to initial is the only legal move
                    legalMoves.Add(loc, Movement.Up, 0, MoveType.Igui);
                }

                return legalMoves.Moves;
            }

            for (int direction = 0; direction < movement.StepRange.Length; ++direction)
            {
                legalMoves.AddRange(loc, direction, movement.StepRange[direction], MoveType.Standard);
            }

            for (int direction = 0; direction < movement.JumpRange.Length; ++direction)
            {
                    if (movement.JumpRange[direction].JumpDistances?.FirstOrDefault() == Movement.Unlimited)
                    {
                        legalMoves.AddRange(loc, direction, Movement.Unlimited, MoveType.Jump);
                        break;
                    }

                for (int i = 0; i < movement.JumpRange[direction].JumpDistances?.Length; ++i)
                {
                    var jumpLoc = legalMoves.Add(loc, direction, movement.JumpRange[direction].JumpDistances[i] + 1, MoveType.Jump);
                    if (jumpLoc != null && game.GetPiece(jumpLoc.Value) == null)
                    {
                        legalMoves.AddRange(jumpLoc.Value, direction, movement.JumpRange[direction].RangeAfter, MoveType.Standard);
                    }
                }
            }

            for (int direction = 0; direction < movement.StepRange.Length; ++direction)
            {
                if (movement.RangeCapture[direction])
                {
                    legalMoves.AddRange(loc, direction, Movement.Unlimited, MoveType.RangedCapture);
                }
            }

            if (movement.HookMove.HasValue)
            {
                switch (movement.HookMove.Value)
                {
                    case HookType.Orthogonal:
                        legalMoves.AddRange(loc, Movement.Up, Movement.Unlimited, MoveType.Hook);
                        legalMoves.AddRange(loc, Movement.Down, Movement.Unlimited, MoveType.Hook);
                        legalMoves.AddRange(loc, Movement.Left, Movement.Unlimited, MoveType.Hook);
                        legalMoves.AddRange(loc, Movement.Right, Movement.Unlimited, MoveType.Hook);
                        break;
                    case HookType.Diagonal:
                        legalMoves.AddRange(loc, Movement.UpLeft, Movement.Unlimited, MoveType.Hook);
                        legalMoves.AddRange(loc, Movement.UpRight, Movement.Unlimited, MoveType.Hook);
                        legalMoves.AddRange(loc, Movement.DownLeft, Movement.Unlimited, MoveType.Hook);
                        legalMoves.AddRange(loc, Movement.DownRight, Movement.Unlimited, MoveType.Hook);
                        break;
                    case HookType.ForwardDiagnal:
                        legalMoves.AddRange(loc, Movement.UpLeft, Movement.Unlimited, MoveType.Hook);
                        legalMoves.AddRange(loc, Movement.UpRight, Movement.Unlimited, MoveType.Hook);
                        break;
                    default:
                        throw new NotSupportedException();
                }
            }

            if (movement.LionMove)
            {
                for (int direction = 0; direction < Movement.DirectionCount; ++direction)
                {
                    legalMoves.Add(loc, direction, 1, MoveType.Area);
                }

                for (int direction = 0; direction < Movement.DirectionCountWithJumps; ++direction)
                {
                    legalMoves.Add(loc, direction, 2, MoveType.Jump);
                }
            }

            if (movement.Igui)
            {
                for (int i = 0; i < Movement.DirectionCount; ++i)
                {
                    legalMoves.Add(loc, i, 1, MoveType.Igui);
                }
            }

            return legalMoves.Moves;
        }
    }
}
