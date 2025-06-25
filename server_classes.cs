using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Classes
{
    public enum team
    {
        white,
        black
    }
    public abstract class Chessman
    {
        protected int x, y;
        protected team team;

        team Team { get { return team; } }

        public int[][] whereCanMove()
        {
            int[][] a = new int[64][];
            int b = 0;
            for (int i = 0; i < 64; ++i)
                a[i] = new int[2];

            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    if (this.isCanMove(i, j))
                    {
                        a[b][0] = i;
                        a[b][1] = j;
                        b++;
                        Console.WriteLine($"{b}, {i}, {j}");
                    }
                }
            }
            int[][] c = new int[b][];
            for (int i = 0; i < b; ++i)
                c[i] = new int[2];
            for (int i = 0; i < b; i++)
            {
                c[i][0] = a[i][0];
                c[i][1] = a[i][1];
            }
            return c;
        }

        public abstract bool isCanMove(int y, int x);


    }
    class Pawn : Chessman
    {

        public override bool isCanMove(int y, int x)
        {
            if (this.x == x)
                if (this.y == y - 1 || this.y == y - 2) return true;

            return false;
        }
        public Pawn(int x, int y, team team)
        {
            this.x = x;
            this.y = y;
            this.team = team;
        }
    }

    class Turris : Chessman
    {

        public override bool isCanMove(int y, int x)
        {
            if (y == this.y || x == this.x) return true;
            else
                return false;
        }
        public Turris(int x, int y, team team)
        {
            this.x = x;
            this.y = y;
            this.team = team;
        }
    }

    class Horse : Chessman
    {
        public override bool isCanMove(int y, int x)
        {
            if (this.y + 2 == y && this.x + 1 == x) return true;
            else if (this.y + 2 == y && this.x - 1 == x) return true;
            else if (this.y - 2 == y && this.x + 1 == x) return true;
            else if (this.y - 2 == y && this.x - 1 == x) return true;
            else if (this.y + 1 == y && this.x - 2 == x) return true;
            else if (this.y + 1 == y && this.x + 2 == x) return true;
            else if (this.y - 1 == y && this.x - 2 == x) return true;
            else if (this.y - 1 == y && this.x + 2 == x) return true;
            else
                return false;
        }
        public Horse(int x, int y, team team)
        {
            this.x = x;
            this.y = y;
            this.team = team;
        }
    }

    class Elephant : Chessman
    {
        public override bool isCanMove(int y, int x)
        {
            if (x - this.x == y - this.y)
                return true;


            return false;
        }
        public Elephant(int x, int y, team team)
        {
            this.x = x;
            this.y = y;
            this.team = team;
        }
    }

    class Ferzin : Chessman
    {
        public override bool isCanMove(int y, int x)
        {

            if (this.x == x + 1 || this.x == x - 1 || this.x == x)
                if (this.y == y + 1 || this.y == y - 1 || this.y == y)
                    return true;
            if (y == this.y || x == this.x) return true;

            if (x - this.x == y - this.y)
                return true;


            return false;
        }
        public Ferzin(int x, int y, team team)
        {
            this.x = x;
            this.y = y;
            this.team = team;
        }
    }

    class King : Chessman
    {
        public override bool isCanMove(int y, int x)
        {

            if (this.x == x + 1 || this.x == x - 1 || this.x == x)
                if (this.y == y + 1 || this.y == y - 1 || this.y == y)
                    return true;

            return false;
        }
        public King(int x, int y, team team)
        {
            this.x = x;
            this.y = y;
            this.team = team;
        }
    }

    public class ChessField
    {
        public Chessman[] Chessmans { get; set; }
        public team Move;
        public ChessField()
        {
            Move = team.white;
            Chessmans = new Chessman[] {
                new Turris(0, 0, team.black),
                new Turris(7, 0, team.black),
                new Turris(0, 7, team.white),
                new Turris(7, 7, team.black),
                new Pawn(1, 0, team.black),
                new Pawn(1, 1, team.black),
                new Pawn(1, 2, team.black),
                new Pawn(1, 3, team.black),
                new Pawn(1, 4, team.black),
                new Pawn(1, 5, team.black),
                new Pawn(1, 6, team.black),
                new Pawn(1, 7, team.black),
                new Pawn(6, 0, team.white),
                new Pawn(6, 1, team.white),
                new Pawn(6, 2, team.white),
                new Pawn(6, 3, team.white),
                new Pawn(6, 4, team.white),
                new Pawn(6, 5, team.white),
                new Pawn(6, 6, team.white),
                new Pawn(6, 7, team.white),
                new Horse(0, 1, team.black),
                new Horse(0, 6, team.black),
                new Horse(7, 1, team.black),
                new Horse(7, 6, team.black),
                new Elephant(0, 2, team.black),
                new Elephant(0, 5, team.black),
                new Elephant(7, 2, team.white),
                new Elephant(7, 5, team.white),
                new Ferzin(0, 3, team.black),
                new Ferzin(7, 3, team.white),
                new King(0, 4, team.black),
                new King(7, 4, team.white),
            };
        }
    }


}
