
using System;
using System.Collections.Generic;

namespace Oracle
{
    public class Movement
    {
        public enum Hooks
        {
            Orthogonal,
            Diagonal,
            ForwardDiagnal
        };

        // basic directions
        public const int UpLeft = 0;
        public const int Up = 1;
        public const int UpRight = 2;
        public const int Right = 3;
        public const int DownRight = 4;
        public const int Down = 5;
        public const int DownLeft = 6;
        public const int Left = 7;

        // extra directions for "knight" jumps 
        public const int HorseUpLeft = 8;
        public const int HorseUpRight = 9;
        public const int HorseLeftUp = 10;
        public const int HorseRightUp = 11;
        public const int HorseRightDown = 12;
        public const int HorseLeftDown = 13;
        public const int HorseDownLeft = 14;
        public const int HorseDownRight = 15;

        public const int FullRange = int.MaxValue;

        // 8 states values 1-N
        //    0 means cannot move that direction (movement matrix)
        //    1 means single spaces in that direction
        //    N means move N squares in that direction
        //    N > 35 means move unlimited
        public int[] StepRange { get; } = new int[8];

        // 16 states values of 1-N, bool (true: exact, false: up to), bool (slide after)
        //    0 means cannot jump that direction (jump matrix)
        //    states 8-15 => 1 can jump there
        //    states 0-7 => 1->N can jump N squares and stop
        public (int Range, bool Exact, bool SlideAfter)[] JumpRange { get; } = new (int Direction, bool Exact, bool SlideAfter)[15];

        // Hook Move (90-degree turn)
        //    3 choices: orthog, diag, forward-diag
        public Hooks? HookMove { get; } = null;

        // Area move ( (two single space moves, double capture
        public bool AreaMove { get; } = false;

        // Can jump any number of spaces to capture
        public bool FlyMove { get; } = false;

        // Range capture
        //   pieces need a rank to see what it cannot jump
        //   move matrix of Booleans for direction
        public bool[] RangeCapture { get; } = new bool[8];

        public int Rank { get; } = 5;
    }

    public static class TaiyokuShogiMoves
    {
        public static Movement GetMovement(this TaiyokuShogi game, PieceIdentity id)
        {
            Movement m = new Movement();
            switch (id)
            {
                case PieceIdentity.Pawn:
                    m.StepRange[Movement.Up] = 1;
                    break;

                case PieceIdentity.EarthGeneral:
                case PieceIdentity.GoBetween:
                    m.StepRange[Movement.Up] = 1;
                    m.StepRange[Movement.Down] = 1;
                    break;

                case PieceIdentity.StoneGeneral:
                    m.StepRange[Movement.UpLeft] = 1;
                    m.StepRange[Movement.UpRight] = 1;
                    break;

                case PieceIdentity.IronGeneral:
                case PieceIdentity.Dog:
                    m.StepRange[Movement.Up] = 1;
                    m.StepRange[Movement.UpLeft] = 1;
                    m.StepRange[Movement.UpRight] = 1;
                    break;

                case PieceIdentity.SwoopingOwl:
                case PieceIdentity.OldRat:
                case PieceIdentity.StruttingCrow:
                    m.StepRange[Movement.Up] = 1;
                    m.StepRange[Movement.DownLeft] = 1;
                    m.StepRange[Movement.DownRight] = 1;
                    break;

                case PieceIdentity.TileGeneral:
                case PieceIdentity.SwordSoldier:
                    m.StepRange[Movement.UpLeft] = 1;
                    m.StepRange[Movement.UpRight] = 1;
                    m.StepRange[Movement.Down] = 1;
                    break;

                case PieceIdentity.CopperGeneral:
                case PieceIdentity.FlyingGoose:
                case PieceIdentity.ClimbingMonkey:
                    m.StepRange[Movement.Up] = 1;
                    m.StepRange[Movement.UpLeft] = 1;
                    m.StepRange[Movement.UpRight] = 1;
                    m.StepRange[Movement.Down] = 1;
                    break;

                case PieceIdentity.RecliningDragon:
                    m.StepRange[Movement.Up] = 1;
                    m.StepRange[Movement.Left] = 1;
                    m.StepRange[Movement.Right] = 1;
                    m.StepRange[Movement.Down] = 1;
                    break;

                case PieceIdentity.CoiledSerpent:
                    m.StepRange[Movement.Up] = 1;
                    m.StepRange[Movement.Down] = 1;
                    m.StepRange[Movement.DownLeft] = 1;
                    m.StepRange[Movement.DownRight] = 1;
                    break;

                case PieceIdentity.FlyingCock:
                    m.StepRange[Movement.Left] = 1;
                    m.StepRange[Movement.Right] = 1;
                    m.StepRange[Movement.UpLeft] = 1;
                    m.StepRange[Movement.UpRight] = 1;
                    break;

                case PieceIdentity.CatSword:
                    m.StepRange[Movement.UpLeft] = 1;
                    m.StepRange[Movement.UpRight] = 1;
                    m.StepRange[Movement.DownLeft] = 1;
                    m.StepRange[Movement.DownRight] = 1;
                    break;

                case PieceIdentity.EvilWolf:
                    m.StepRange[Movement.Left] = 1;
                    m.StepRange[Movement.Right] = 1;
                    m.StepRange[Movement.Up] = 1;
                    m.StepRange[Movement.UpLeft] = 1;
                    m.StepRange[Movement.UpRight] = 1;
                    break;

                case PieceIdentity.SilverGeneral:
                case PieceIdentity.ViolentStag:
                    m.StepRange[Movement.Up] = 1;
                    m.StepRange[Movement.UpLeft] = 1;
                    m.StepRange[Movement.UpRight] = 1;
                    m.StepRange[Movement.DownLeft] = 1;
                    m.StepRange[Movement.DownRight] = 1;
                    break;

                case PieceIdentity.BlindDog:
                case PieceIdentity.ChineseCock:
                    m.StepRange[Movement.Left] = 1;
                    m.StepRange[Movement.Right] = 1;
                    m.StepRange[Movement.UpLeft] = 1;
                    m.StepRange[Movement.UpRight] = 1;
                    m.StepRange[Movement.Down] = 1;
                    break;

                case PieceIdentity.OldMonkey:
                    m.StepRange[Movement.UpLeft] = 1;
                    m.StepRange[Movement.UpRight] = 1;
                    m.StepRange[Movement.Down] = 1;
                    m.StepRange[Movement.DownLeft] = 1;
                    m.StepRange[Movement.DownRight] = 1;
                    break;

                case PieceIdentity.GoldGeneral:
                case PieceIdentity.ViolentWolf:
                    m.StepRange[Movement.Up] = 1;
                    m.StepRange[Movement.Down] = 1;
                    m.StepRange[Movement.Left] = 1;
                    m.StepRange[Movement.Right] = 1;
                    m.StepRange[Movement.UpLeft] = 1;
                    m.StepRange[Movement.UpRight] = 1;
                    break;

                case PieceIdentity.FerociousLeopard:
                    m.StepRange[Movement.Up] = 1;
                    m.StepRange[Movement.Down] = 1;
                    m.StepRange[Movement.UpLeft] = 1;
                    m.StepRange[Movement.UpRight] = 1;
                    m.StepRange[Movement.DownLeft] = 1;
                    m.StepRange[Movement.DownRight] = 1;
                    break;

                case PieceIdentity.BlindMonkey:
                case PieceIdentity.BlindBear:
                    m.StepRange[Movement.Left] = 1;
                    m.StepRange[Movement.Right] = 1;
                    m.StepRange[Movement.UpLeft] = 1;
                    m.StepRange[Movement.UpRight] = 1;
                    m.StepRange[Movement.DownLeft] = 1;
                    m.StepRange[Movement.DownRight] = 1;
                    break;

                case PieceIdentity.DrunkenElephant:
                case PieceIdentity.NeighboringKing:
                case PieceIdentity.RushingBoar:
                    m.StepRange[Movement.Up] = 1;
                    m.StepRange[Movement.Left] = 1;
                    m.StepRange[Movement.Right] = 1;
                    m.StepRange[Movement.UpLeft] = 1;
                    m.StepRange[Movement.UpRight] = 1;
                    m.StepRange[Movement.DownLeft] = 1;
                    m.StepRange[Movement.DownRight] = 1;
                    break;

                case PieceIdentity.Deva:
                    m.StepRange[Movement.Up] = 1;
                    m.StepRange[Movement.Down] = 1;
                    m.StepRange[Movement.Left] = 1;
                    m.StepRange[Movement.Right] = 1;
                    m.StepRange[Movement.UpLeft] = 1;
                    m.StepRange[Movement.DownLeft] = 1;
                    m.StepRange[Movement.DownRight] = 1;
                    break;

                case PieceIdentity.DarkSpirit:
                    m.StepRange[Movement.Up] = 1;
                    m.StepRange[Movement.Down] = 1;
                    m.StepRange[Movement.Left] = 1;
                    m.StepRange[Movement.Right] = 1;
                    m.StepRange[Movement.UpRight] = 1;
                    m.StepRange[Movement.DownLeft] = 1;
                    m.StepRange[Movement.DownRight] = 1;
                    break;

                case PieceIdentity.BlindTiger:
                    m.StepRange[Movement.Down] = 1;
                    m.StepRange[Movement.Left] = 1;
                    m.StepRange[Movement.Right] = 1;
                    m.StepRange[Movement.UpLeft] = 1;
                    m.StepRange[Movement.UpRight] = 1;
                    m.StepRange[Movement.DownLeft] = 1;
                    m.StepRange[Movement.DownRight] = 1;
                    break;

                case PieceIdentity.Prince:
                case PieceIdentity.LeftGeneral:
                case PieceIdentity.RightGeneral:
                case PieceIdentity.BearsEyes:
                case PieceIdentity.VenomousWolf:
                    m.StepRange[Movement.Up] = 1;
                    m.StepRange[Movement.Down] = 1;
                    m.StepRange[Movement.Left] = 1;
                    m.StepRange[Movement.Right] = 1;
                    m.StepRange[Movement.UpLeft] = 1;
                    m.StepRange[Movement.UpRight] = 1;
                    m.StepRange[Movement.DownLeft] = 1;
                    m.StepRange[Movement.DownRight] = 1;
                    break;

                case PieceIdentity.WoodGeneral:
                    m.StepRange[Movement.UpLeft] = 2;
                    m.StepRange[Movement.UpRight] = 2;
                    break;

                case PieceIdentity.Donkey:
                case PieceIdentity.EnchantedBadger:
                    m.StepRange[Movement.Up] = 2;
                    m.StepRange[Movement.Down] = 2;
                    m.StepRange[Movement.Left] = 2;
                    m.StepRange[Movement.Right] = 2;
                    break;

                case PieceIdentity.FlyingHorse:
                    m.StepRange[Movement.UpLeft] = 2;
                    m.StepRange[Movement.UpRight] = 2;
                    m.StepRange[Movement.DownLeft] = 2;
                    m.StepRange[Movement.DownRight] = 2;
                    break;

                case PieceIdentity.BeastCadet:
                    m.StepRange[Movement.Up] = 2;
                    m.StepRange[Movement.Left] = 2;
                    m.StepRange[Movement.Right] = 2;
                    m.StepRange[Movement.UpLeft] = 2;
                    m.StepRange[Movement.UpRight] = 2;
                    m.StepRange[Movement.DownLeft] = 2;
                    m.StepRange[Movement.DownRight] = 2;
                    break;

                case PieceIdentity.King:
                case PieceIdentity.FragrantElephant:
                case PieceIdentity.WhiteElephant:
                    m.StepRange[Movement.Up] = 2;
                    m.StepRange[Movement.Down] = 2;
                    m.StepRange[Movement.Left] = 2;
                    m.StepRange[Movement.Right] = 2;
                    m.StepRange[Movement.UpLeft] = 2;
                    m.StepRange[Movement.UpRight] = 2;
                    m.StepRange[Movement.DownLeft] = 2;
                    m.StepRange[Movement.DownRight] = 2;
                    break;

                case PieceIdentity.RushingBird:
                    m.StepRange[Movement.Up] = 2;
                    m.StepRange[Movement.Left] = 1;
                    m.StepRange[Movement.Right] = 1;
                    m.StepRange[Movement.UpLeft] = 1;
                    m.StepRange[Movement.UpRight] = 1;
                    m.StepRange[Movement.DownLeft] = 1;
                    m.StepRange[Movement.DownRight] = 1;
                    break;

                case PieceIdentity.AngryBoar:
                    m.StepRange[Movement.Up] = 1;
                    m.StepRange[Movement.Left] = 1;
                    m.StepRange[Movement.Right] = 1;
                    m.StepRange[Movement.UpLeft] = 2;
                    m.StepRange[Movement.UpRight] = 2;
                    break;

                case PieceIdentity.ViolentBear:
                    if ((game.Options & TaiyokuShogiOptions.ViolentBearAlternative) == 0)
                    {
                        m.StepRange[Movement.Up] = 1;
                        m.StepRange[Movement.Left] = 1;
                        m.StepRange[Movement.Right] = 1;
                        m.StepRange[Movement.UpLeft] = 2;
                        m.StepRange[Movement.UpRight] = 2;
                    }
                    else
                    {
                        m.StepRange[Movement.Left] = 1;
                        m.StepRange[Movement.Right] = 1;
                        m.StepRange[Movement.UpLeft] = 2;
                        m.StepRange[Movement.UpRight] = 2;
                        m.StepRange[Movement.DownLeft] = 1;
                        m.StepRange[Movement.DownRight] = 1;
                    }
                    break;

                case PieceIdentity.EasternBarbarian:
                case PieceIdentity.WesternBarbarian:
                    m.StepRange[Movement.Up] = 2;
                    m.StepRange[Movement.Down] = 2;
                    m.StepRange[Movement.Left] = 1;
                    m.StepRange[Movement.Right] = 1;
                    m.StepRange[Movement.UpLeft] = 1;
                    m.StepRange[Movement.UpRight] = 1;
                    break;

                case PieceIdentity.NorthernBarbarian:
                case PieceIdentity.SouthernBarbarian:
                case PieceIdentity.PrancingStag:
                    m.StepRange[Movement.Up] = 1;
                    m.StepRange[Movement.Down] = 1;
                    m.StepRange[Movement.Left] = 2;
                    m.StepRange[Movement.Right] = 2;
                    m.StepRange[Movement.UpLeft] = 1;
                    m.StepRange[Movement.UpRight] = 1;
                    break;

                case PieceIdentity.PoisonousSnake:
                    m.StepRange[Movement.Up] = 2;
                    m.StepRange[Movement.Down] = 1;
                    m.StepRange[Movement.Left] = 2;
                    m.StepRange[Movement.Right] = 2;
                    m.StepRange[Movement.UpLeft] = 1;
                    m.StepRange[Movement.UpRight] = 1;
                    break;

                case PieceIdentity.OldKite:
                    m.StepRange[Movement.Left] = 1;
                    m.StepRange[Movement.Right] = 1;
                    m.StepRange[Movement.DownLeft] = 2;
                    m.StepRange[Movement.DownRight] = 2;
                    m.StepRange[Movement.UpLeft] = 2;
                    m.StepRange[Movement.UpRight] = 2;
                    break;

                case PieceIdentity.FierceEagle:
                    m.StepRange[Movement.Up] = 1;
                    m.StepRange[Movement.Left] = 1;
                    m.StepRange[Movement.Right] = 1;
                    m.StepRange[Movement.DownLeft] = 2;
                    m.StepRange[Movement.DownRight] = 2;
                    m.StepRange[Movement.UpLeft] = 2;
                    m.StepRange[Movement.UpRight] = 2;
                    break;

                case PieceIdentity.GuardianOfTheGods:
                    m.StepRange[Movement.Up] = 3;
                    m.StepRange[Movement.Down] = 3;
                    m.StepRange[Movement.Left] = 3;
                    m.StepRange[Movement.Right] = 3;
                    break;

                case PieceIdentity.Wrestler:
                    m.StepRange[Movement.UpLeft] = 3;
                    m.StepRange[Movement.UpRight] = 3;
                    m.StepRange[Movement.DownLeft] = 3;
                    m.StepRange[Movement.DownRight] = 3;
                    break;

                case PieceIdentity.CaptiveCadet:
                    m.StepRange[Movement.Up] = 3;
                    m.StepRange[Movement.Left] = 3;
                    m.StepRange[Movement.Right] = 3;
                    m.StepRange[Movement.UpLeft] = 3;
                    m.StepRange[Movement.UpRight] = 3;
                    m.StepRange[Movement.DownLeft] = 3;
                    m.StepRange[Movement.DownRight] = 3;
                    break;

                case PieceIdentity.HorseGeneral:
                case PieceIdentity.OxGeneral:
                    m.StepRange[Movement.Up] = 3;
                    m.StepRange[Movement.Down] = 1;
                    m.StepRange[Movement.UpLeft] = 1;
                    m.StepRange[Movement.UpRight] = 1;
                    break;

                case PieceIdentity.FireGeneral:
                    m.StepRange[Movement.Up] = 3;
                    m.StepRange[Movement.Down] = 3;
                    m.StepRange[Movement.UpLeft] = 1;
                    m.StepRange[Movement.UpRight] = 1;
                    break;

                case PieceIdentity.WaterGeneral:
                    m.StepRange[Movement.Up] = 1;
                    m.StepRange[Movement.Down] = 1;
                    m.StepRange[Movement.UpLeft] = 3;
                    m.StepRange[Movement.UpRight] = 3;
                    break;

                case PieceIdentity.BuddhistDevil:
                    m.StepRange[Movement.Down] = 1;
                    m.StepRange[Movement.Left] = 1;
                    m.StepRange[Movement.Right] = 1;
                    m.StepRange[Movement.UpLeft] = 3;
                    m.StepRange[Movement.UpRight] = 3;
                    break;

                case PieceIdentity.WindGeneral:
                case PieceIdentity.RiverGeneral:
                    m.StepRange[Movement.Up] = 3;
                    m.StepRange[Movement.Down] = 1;
                    m.StepRange[Movement.UpLeft] = 1;
                    m.StepRange[Movement.UpRight] = 1;
                    break;

                case PieceIdentity.Yaksha:
                    m.StepRange[Movement.Left] = 3;
                    m.StepRange[Movement.Right] = 3;
                    m.StepRange[Movement.Down] = 1;
                    m.StepRange[Movement.UpLeft] = 1;
                    m.StepRange[Movement.UpRight] = 1;
                    break;

                case PieceIdentity.SwordGeneral:
                    m.StepRange[Movement.Up] = 3;
                    m.StepRange[Movement.Down] = 1;
                    m.StepRange[Movement.UpLeft] = 3;
                    m.StepRange[Movement.UpRight] = 3;
                    break;

                case PieceIdentity.CaptiveOfficer:
                    m.StepRange[Movement.Up] = 2;
                    m.StepRange[Movement.Left] = 2;
                    m.StepRange[Movement.Right] = 2;
                    m.StepRange[Movement.UpLeft] = 3;
                    m.StepRange[Movement.UpRight] = 3;
                    m.StepRange[Movement.DownLeft] = 3;
                    m.StepRange[Movement.DownRight] = 3;
                    break;

                case PieceIdentity.BeastOfficer:
                    m.StepRange[Movement.Up] = 3;
                    m.StepRange[Movement.Left] = 2;
                    m.StepRange[Movement.Right] = 2;
                    m.StepRange[Movement.UpLeft] = 3;
                    m.StepRange[Movement.UpRight] = 3;
                    m.StepRange[Movement.DownLeft] = 3;
                    m.StepRange[Movement.DownRight] = 3;
                    break;

                case PieceIdentity.HeavenlyTetrarch:
                    m.StepRange[Movement.Up] = 4;
                    m.StepRange[Movement.Down] = 4;
                    m.StepRange[Movement.Left] = 4;
                    m.StepRange[Movement.Right] = 4;
                    m.StepRange[Movement.UpLeft] = 4;
                    m.StepRange[Movement.UpRight] = 4;
                    m.StepRange[Movement.DownLeft] = 4;
                    m.StepRange[Movement.DownRight] = 4;
                    break;

                case PieceIdentity.ChickenGeneral:
                case PieceIdentity.PupGeneral:
                    m.StepRange[Movement.Up] = 4;
                    m.StepRange[Movement.DownLeft] = 1;
                    m.StepRange[Movement.DownRight] = 1;
                    break;

                case PieceIdentity.PigGeneral:
                    m.StepRange[Movement.Down] = 2;
                    m.StepRange[Movement.UpLeft] = 4;
                    m.StepRange[Movement.UpRight] = 4;
                    break;

                case PieceIdentity.MountainStag:
                    m.StepRange[Movement.Up] = 1;
                    m.StepRange[Movement.Left] = 2;
                    m.StepRange[Movement.Right] = 2;
                    m.StepRange[Movement.Down] = 4;
                    m.StepRange[Movement.UpLeft] = 3;
                    m.StepRange[Movement.UpRight] = 3;
                    break;

                case PieceIdentity.LeopardKing:
                    m.StepRange[Movement.Up] = 5;
                    m.StepRange[Movement.Down] = 5;
                    m.StepRange[Movement.Left] = 5;
                    m.StepRange[Movement.Right] = 5;
                    m.StepRange[Movement.UpLeft] = 5;
                    m.StepRange[Movement.UpRight] = 5;
                    m.StepRange[Movement.DownLeft] = 5;
                    m.StepRange[Movement.DownRight] = 5;
                    break;

                case PieceIdentity.TurtleDove:
                    m.StepRange[Movement.Down] = 1;
                    m.StepRange[Movement.Left] = 1;
                    m.StepRange[Movement.Right] = 1;
                    m.StepRange[Movement.UpLeft] = 5;
                    m.StepRange[Movement.UpRight] = 5;
                    break;

                case PieceIdentity.CrossbowSoldier:
                    m.StepRange[Movement.Up] = 5;
                    m.StepRange[Movement.Down] = 1;
                    m.StepRange[Movement.Left] = 3;
                    m.StepRange[Movement.Right] = 3;
                    m.StepRange[Movement.UpLeft] = 3;
                    m.StepRange[Movement.UpRight] = 3;
                    break;

                case PieceIdentity.BurningSoldier:
                    m.StepRange[Movement.Up] = 7;
                    m.StepRange[Movement.Down] = 1;
                    m.StepRange[Movement.Left] = 3;
                    m.StepRange[Movement.Right] = 3;
                    m.StepRange[Movement.UpLeft] = 5;
                    m.StepRange[Movement.UpRight] = 5;
                    break;

                case PieceIdentity.Lance:
                case PieceIdentity.OxCart:
                case PieceIdentity.SavageTiger:
                    m.StepRange[Movement.Up] = Movement.FullRange;
                    break;

                case PieceIdentity.ReverseChariot:
                    m.StepRange[Movement.Up] = Movement.FullRange;
                    m.StepRange[Movement.Down] = Movement.FullRange;
                    break;

                case PieceIdentity.SideDragon:
                    m.StepRange[Movement.Up] = Movement.FullRange;
                    m.StepRange[Movement.Left] = Movement.FullRange;
                    m.StepRange[Movement.Right] = Movement.FullRange;
                    break;

                case PieceIdentity.MountainWitch:
                    m.StepRange[Movement.Down] = Movement.FullRange;
                    m.StepRange[Movement.DownLeft] = Movement.FullRange;
                    m.StepRange[Movement.DownRight] = Movement.FullRange;
                    break;

                case PieceIdentity.WhiteHorse:
                case PieceIdentity.BirdOfParadise:
                case PieceIdentity.MultiGeneral:
                    m.StepRange[Movement.Up] = Movement.FullRange;
                    m.StepRange[Movement.Down] = Movement.FullRange;
                    m.StepRange[Movement.UpLeft] = Movement.FullRange;
                    m.StepRange[Movement.UpRight] = Movement.FullRange;
                    break;

                case PieceIdentity.Rook:
                case PieceIdentity.Soldier:
                case PieceIdentity.RunningChariot:
                case PieceIdentity.SquareMover:
                case PieceIdentity.GlidingSwallow:
                    m.StepRange[Movement.Up] = Movement.FullRange;
                    m.StepRange[Movement.Down] = Movement.FullRange;
                    m.StepRange[Movement.Left] = Movement.FullRange;
                    m.StepRange[Movement.Right] = Movement.FullRange;
                    break;

                case PieceIdentity.FreeSerpent:
                case PieceIdentity.CoiledDragon:
                case PieceIdentity.Whale:
                    m.StepRange[Movement.Up] = Movement.FullRange;
                    m.StepRange[Movement.Down] = Movement.FullRange;
                    m.StepRange[Movement.DownLeft] = Movement.FullRange;
                    m.StepRange[Movement.DownRight] = Movement.FullRange;
                    break;

                case PieceIdentity.Bishop:
                    m.StepRange[Movement.UpLeft] = Movement.FullRange;
                    m.StepRange[Movement.UpRight] = Movement.FullRange;
                    m.StepRange[Movement.DownLeft] = Movement.FullRange;
                    m.StepRange[Movement.DownRight] = Movement.FullRange;
                    break;

                case PieceIdentity.FreeWolf:
                case PieceIdentity.RunningLeopard:
                    m.StepRange[Movement.Up] = Movement.FullRange;
                    m.StepRange[Movement.Left] = Movement.FullRange;
                    m.StepRange[Movement.Right] = Movement.FullRange;
                    m.StepRange[Movement.UpLeft] = Movement.FullRange;
                    m.StepRange[Movement.UpRight] = Movement.FullRange;
                    break;

                case PieceIdentity.WizardStork:
                    m.StepRange[Movement.Left] = Movement.FullRange;
                    m.StepRange[Movement.Right] = Movement.FullRange;
                    m.StepRange[Movement.Down] = Movement.FullRange;
                    m.StepRange[Movement.UpLeft] = Movement.FullRange;
                    m.StepRange[Movement.UpRight] = Movement.FullRange;
                    break;

                case PieceIdentity.FlyingOx:
                case PieceIdentity.FreeBear:
                case PieceIdentity.FreeLeopard:
                case PieceIdentity.GreatWhale:
                    m.StepRange[Movement.Up] = Movement.FullRange;
                    m.StepRange[Movement.Down] = Movement.FullRange;
                    m.StepRange[Movement.UpLeft] = Movement.FullRange;
                    m.StepRange[Movement.UpRight] = Movement.FullRange;
                    m.StepRange[Movement.DownLeft] = Movement.FullRange;
                    m.StepRange[Movement.DownRight] = Movement.FullRange;
                    break;
            }

            return m;
        }
    }
}