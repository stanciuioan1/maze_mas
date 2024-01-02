using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Reactive
{
    class Point
    {
        public int r;
        public int c;
        public Point parent;

        public Point(int x, int y, Point p)
        {
            r = x;
            c = y;
            parent = p;
        }

        // compute the opposite node given that it is in the other direction from the parent
        public Point opposite()
        {
            if (r.CompareTo(parent.r) != 0)
                return new Point(r + r.CompareTo(parent.r), c, this);
            if (c.CompareTo(parent.c) != 0)
                return new Point(r, c + c.CompareTo(parent.c), this);
            return null;
        }
    }





    public class Utils
    {
        private static bool isTesting = false;
        public static int Size = 20;
        public static int NoExplorers = 5;
        public static int[,] Maze = selectMatrix();
        public static int Delay = 400;
        public static int SpawnDelay = 2 * Delay;
        public static Random RandNoGen = new Random();
        public static int[] dWidth = { 1, 0, -1, 0 };
        public static int[] dHeight = { 0, 1, 0, -1 };
        private static int[,] selectMatrix()
        {
            int[,] localMaze =  {
                { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
                { 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1},
                { 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0, 1, 0, 0},
                { 0, 1, 1, 1, 0, 1, 1, 1, 0, 1, 0, 1, 1, 1, 0, 1, 0, 0, 1, 1},
                { 0, 0, 2, 0, 0, 0, 0, 1, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0},
                { 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1},
                { 0, 0, 0, 1, 0, 1, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0},
                { 0, 1, 1, 1, 0, 1, 0, 1, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 1},
                { 0, 1, 0, 0, 0, 0, 0, 1, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 1},
                { 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 1, 1, 1, 1, 0, 1, 0, 1},
                { 0, 0, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1},
                { 0, 1, 1, 1, 1, 1, 1, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 1, 1},
                { 0, 0, 0, 1, 0, 1, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0, 0},
                { 0, 1, 1, 1, 0, 1, 0, 1, 1, 1, 0, 1, 1, 1, 1, 1, 0, 1, 1, 1},
                { 0, 1, 0, 1, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0, 1, 0, 0},
                { 0, 1, 0, 1, 0, 1, 1, 1, 0, 1, 1, 1, 1, 1, 0, 1, 0, 1, 1, 1},
                { 0, 0, 0, 1, 0, 0, 0, 1, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0},
                { 0, 1, 1, 1, 0, 1, 1, 1, 0, 1, 0, 1, 1, 1, 0, 1, 1, 1, 1, 1},
                { 0, 0, 0, 1, 0, 0, 0, 1, 0, 1, 0, 0, 0, 1, 0, 1, 0, 1, 0, 0},
                { 0, 3, 1, 1, 0, 1, 1, 1, 0, 1, 0, 1, 1, 1, 0, 1, 0, 1, 1, 1},
                };

            if (isTesting == true)
                return localMaze;
            else
                return GetMatrix(Size, Size);
        }
        private static int[,] GetMatrix(int rows, int columns)
        {
            int[,] maze = new int[rows, columns];
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < columns; j++)
                    maze[i, j] = 1;

            Point start = new Point((int)(new Random().NextDouble() * rows), (int)(new Random().NextDouble() * columns), null);
            maze[start.r, start.c] = 2;
            List<Point> frontier = new List<Point>();
            for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                {
                    if ((x == 0 && y == 0) || (x != 0 && y != 0))
                        continue;
                    try
                    {
                        if (maze[start.r + x, start.c + y] == 0) continue;
                    }
                    catch (IndexOutOfRangeException)
                    {
                        continue;
                    }
                    frontier.Add(new Point(start.r + x, start.c + y, start));
                }

            Point last = null;
            Random random = new Random();

            while (frontier.Count > 0)
            {
                // pick the current node at random
                int value = random.Next(frontier.Count);
                Point current = frontier[value];
                frontier.RemoveAt(value);
                Point opposite = current.opposite();
                try
                {
                    // if both the node and its opposite are walls
                    if (maze[current.r, current.c] == 1)
                    {
                        if (maze[opposite.r, opposite.c] == 1)
                        {
                            // open a path between the nodes
                            maze[current.r, current.c] = 0;
                            maze[opposite.r, opposite.c] = 0;
                            // store the last node to mark it later
                            last = opposite;

                            // iterate through direct neighbors of the node, same as earlier
                            for (int x = -1; x <= 1; x++)
                                for (int y = -1; y <= 1; y++)
                                {
                                    if ((x == 0 && y == 0) || (x != 0 && y != 0))
                                        continue;
                                    if (maze[opposite.r + x, opposite.c + y] == 0)
                                        continue;
                                    frontier.Add(new Point(opposite.r + x, opposite.c + y, opposite));
                                }
                        }
                    }
                }
                catch (Exception)
                {
                    // ignore NullPointer and ArrayIndexOutOfBounds
                }

                // if the algorithm has resolved, mark the end node
                if (frontier.Count == 0)
                    maze[last.r, last.c] = 3;

            }

            return maze;

        }

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