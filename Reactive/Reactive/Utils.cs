using System;
using System.Collections.Generic;
using System.Linq;

namespace Reactive
{
    public class Utils
    {
        public static int Size = 11;
        public static int NoExplorers = 100;
        public static int[,] Maze = {
            {2,1,1,1,1,1,1,1,1,1,3},
            {0,1,0,0,0,0,1,0,0,0,0},
            {0,1,0,1,1,0,1,0,1,1,0},
            {0,1,0,1,1,0,1,1,1,1,0},
            {0,0,0,0,1,0,1,0,0,0,0},
            {0,1,1,1,1,0,1,0,1,1,0},
            {0,1,0,0,0,0,1,0,1,0,0},
            {0,1,1,1,1,1,1,0,1,1,1},
            {0,0,0,1,0,0,0,0,1,0,0},
            {1,1,0,0,0,1,1,1,1,0,1},
            {1,1,0,0,0,0,0,0,0,0,0},
        };

        public static int Delay = 400;
        public static int SpawnDelay = 2 * Delay;
        public static Random RandNoGen = new Random();
        public static int[] dWidth = { 1, 0, -1, 0 };
        public static int[] dHeight = { 0, 1, 0, -1 };

        public static WeightedMaze CreateWeightedMaze(int[,] maze)
        {
            int width = maze.GetLength(0);
            int height = maze.GetLength(1);
            WeightedMaze weightedMaze = new WeightedMaze(width, height);

            // Initialize weights based on the maze.
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    for (int direction = 0; direction < 4; direction++)
                    {
                        int x = i + dWidth[direction];
                        int y = j + dHeight[direction];

                        // By default, all spaces are "floors".
                        double weight = 1;
                        if (x < 0 || y < 0 || x >= width || y >= height)
                        {
                            // It is margin, mark it as a wall.
                            weight = 0;
                        }
                        else if (maze[x, y] == 1)
                        {
                            // Direction pointing to a wall.
                            weight = 0;
                        }

                        weightedMaze.Values[i, j, direction] = weight;
                    }
                }
            }

            return weightedMaze;
        }

        public static int GetDirrerentialDirection(int startX, int startY, int endX, int endY)
        {
            int dX = endX - startX;
            int dY = endY - startY;

            for(int direction = 0; direction < 4; direction ++)
            {
                if (dX == dWidth[direction] && dY == dHeight[direction])
                {
                    return direction;
                }
            }

            return -1;
        }

        public static void ParseMessage(string content, out string action, out List<string> parameters)
        {
            string[] t = content.Split();

            action = t[0];

            parameters = new List<string>();
            for (int i = 1; i < t.Length; i++)
                parameters.Add(t[i]);
        }

        public static void ParseMessage(string content, out string action, out string parameters)
        {
            string[] t = content.Split();

            action = t[0];

            parameters = "";

            if (t.Length > 1)
            {
                for (int i = 1; i < t.Length - 1; i++)
                    parameters += t[i] + " ";
                parameters += t[t.Length - 1];
            }
        }

        public static void ParseParameters(string content, out List<string> parameters)
        { 
            parameters = content.Split().ToList<string>();
        }

        public static void ParseIntParameters(string content, out List<int> parameters)
        {
            List<string> splited;
            ParseParameters(content, out splited);
            parameters = splited.Select(str => int.Parse(str)).ToList();
        }

        public static string Str(object p1, object p2)
        {
            return string.Format("{0} {1}", p1, p2);
        }

        public static string Str(object p1, object p2, object p3)
        {
            return string.Format("{0} {1} {2}", p1, p2, p3);
        }
        public static string Str(object p1, object p2, object p3, object p4)
        {
            return string.Format("{0} {1} {2} {3}", p1, p2, p3, p4);
        }
    }
}