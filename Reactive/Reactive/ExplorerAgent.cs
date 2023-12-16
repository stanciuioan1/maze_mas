using ActressMas;
using System;
using System.Collections.Generic;

namespace Reactive
{
    public class ExplorerAgent : Agent
    {
        private int _x, _y;
        private Stack<string> _lastPositions;
        private Stack<string> _pathToExit;
        private State _state;
        public WeightedMaze Weights { get; set; }

        private enum State { NotStarted, Exploring, DeadEnd, Exit };

        public override void Setup()
        {
            _x = -1;
            _y = -1;
            _lastPositions = new Stack<string>();
            _pathToExit = new Stack<string>();
            _state = State.NotStarted;
            Weights = Utils.CreateWeightedMaze(Utils.Maze);
            Console.WriteLine("Waiting for the start command " + Name);
        }

        public override void Act(Message message)
        {
            Console.WriteLine("\t[{1} -> {0}]: {2}", this.Name, message.Sender, message.Content);

            string action;
            List<string> parameters;
            Utils.ParseMessage(message.Content, out action, out parameters);

            switch(action)
            {
                case "spawn":
                    HandleSpawn(parameters);
                    break;

                case "avoid":
                    HandleAvoid(parameters);
                    break;

                case "exploring":
                    HandleExplored(parameters);
                    break;

                case "found":
                    HandleExit(parameters, true);
                    break;

                case "exit":
                    HandleExit(parameters, false);
                    break;

                case "come":
                    HandleCome(parameters);
                    break;

                case "move":
                    HandleMove(parameters);
                    break;

                case "block":
                    HandleBlock(parameters);
                    break;
            }
        }

        private void HandleSpawn(List<string> parameters)
        {
            if (_state != State.NotStarted) return;

            // Wake up the agent at the starting position.
            _state = State.Exploring;
            _x = int.Parse(parameters[0]);
            _y = int.Parse(parameters[1]);
            Send("maze", Utils.Str("position", _x, _y));
        }

        private void HandleExit(List<string> parameters, bool isFirst)
        {
            // Send to all others the position if it was first time discovered.
            if(isFirst)
            {
                Broadcast(Utils.Str("come", _x, _y), false, "explorers_channel");
            }

            // Stop the agent.
            Console.WriteLine("{0}: Stopped", Name);
            this.Stop();
        }

        private void HandleCome(List<string> parameters)
        {
            // Exit has been found.
            _state = State.Exit;

            // Create path to the exit using the weights and the provided location.
            int exitX = int.Parse(parameters[0]);
            int exitY = int.Parse(parameters[1]);
            CreatePathToExit(exitX, exitY);
        }

        private void HandleAvoid(List<string> parameters)
        {
            // When going to exit, there is a predefined path.
            if (_state == State.Exit) return;

            int avoidX = int.Parse(parameters[0]);
            int avoidY = int.Parse(parameters[1]);
            int avoidDir = int.Parse(parameters[2]);
           
            Weights.Values[avoidX, avoidY, avoidDir] = 0;
        }

        private void HandleExplored(List<string> parameters)
        {
            // When going to exit, there is a predefined path.
            if (_state == State.Exit) return;

            int avoidX = int.Parse(parameters[0]);
            int avoidY = int.Parse(parameters[1]);
            int avoidDir = int.Parse(parameters[2]);
            double oldValue = Weights.Values[avoidX, avoidY, avoidDir]; 

            // Value must be higher than 0.
            Weights.Values[avoidX, avoidY, avoidDir] = Math.Max(0.01, oldValue - 0.1);
        }

        private void HandleMove(List<string> parameters)
        {
            if (_state == State.Exploring)
            {
                MoveBestDirection();
                Send("maze", Utils.Str("change", _x, _y));
            }
            else if (_state == State.DeadEnd)
            {
                MoveBackwords();
                Send("maze", Utils.Str("change", _x, _y));

                // Check available locations.
                int locations = 0;
                for (int d = 0; d < 4; d++)
                {
                    if (Weights.Values[_x, _y, d] > 0)
                    {
                        locations++;
                    }
                }

                if (locations > 1)
                {
                    _state = State.Exploring;
                }
            }
            else if (_state == State.Exit)
            {
                MoveToExit();
                Send("maze", Utils.Str("change", _x, _y));
            }
        }

        private void HandleBlock(List<string> parameters)
        {
            // If the state is Exploring, a block means that the lane is ocupied. Should check for a different position or return.
            if (_state == State.Exploring)
            {
                // Todo: Excluding the one we walked before (maybe the others that were excluded?).
                List<int> bestDirs = FindBestDirection();

                if (bestDirs.Count == 0)
                {
                    _state = State.DeadEnd;
                    return;
                }
            }


            int oldX = _x;
            int oldY = _y;
            MoveRandomly();
            // The position has changed, make sure to push the old ones in the stacks if in special states.
            if (oldX != _x || oldY != _y)
            {
                if (_state == State.DeadEnd)
                {
                    // Put this position in the moving back stack.
                    _lastPositions.Push(Utils.Str(oldX, oldY));
                }
                else if (_state == State.Exit)
                {
                    // Put this position in the path to exit stack.
                    _pathToExit.Push(Utils.Str(oldX, oldY));
                }
            }

            Send("maze", Utils.Str("change", _x, _y));
        }

        private void MoveRandomly()
        {
            int d = Utils.RandNoGen.Next(4);
            switch (d)
            {
                case 0: if (_x > 0 && Utils.Maze[_x - 1, _y] == 0) _x--; break;
                case 1: if (_x < Utils.Size - 1 && Utils.Maze[_x + 1, _y] == 0) _x++; break;
                case 2: if (_y > 0 && Utils.Maze[_x, _y - 1] == 0) _y--; break;
                case 3: if (_y < Utils.Size - 1 && Utils.Maze[_x, _y + 1] == 0) _y++; break;
            }
        }

        private void MoveBestDirection()
        {
            List<int> bestDirs = FindBestDirection();
            if (bestDirs.Count == 0)
            {
                // No place to go. Change state to DeadEnd.
                _state = State.DeadEnd;
            } 
            else
            {
                if (_lastPositions.Count == 0 || _lastPositions.Peek() != Utils.Str(_x, _y))
                {
                    _lastPositions.Push(Utils.Str(_x, _y));
                }

                // Choose randomly a direction from BestDirs.
                int dir = bestDirs[Utils.RandNoGen.Next(bestDirs.Count)];
                // Sent to all the direction to go.
                Broadcast(Utils.Str("exploring", _x, _y, dir), false, "explorers_channel");
                _x += Utils.dWidth[dir];
                _y += Utils.dHeight[dir];
            }
        }

        private void MoveBackwords()
        {
            string lastPosition = _lastPositions.Pop();
            string[] positions = lastPosition.Split();
            int newX = int.Parse(positions[0]);
            int newY = int.Parse(positions[1]);

            // Never coming back in this direction.
            // newX + dx = _x
            // dx = _x - newX
            int dx = _x - newX;
            int dy = _y - newY;
            int dir;
            for (dir = 0; dir < 4 && (Utils.dWidth[dir] != dx || Utils.dHeight[dir] != dy); dir++);

            if (dir < 4)
            {
                Weights.Values[newX, newY, dir] = 0;
                // Send to others the coordinate to avoid.
                Broadcast(Utils.Str("avoid", newX, newY, dir), false, "explorers_channel");
            }

            _x = newX;
            _y = newY;
        }

        private void MoveToExit()
        {
            // Todo: Pop element from _pathToExit queue.
            string lastPosition = _pathToExit.Pop();
            string[] positions = lastPosition.Split();
            _x = int.Parse(positions[0]);
            _y = int.Parse(positions[1]);
        }

        private List<int> FindBestDirection(List<int> excluding = null)
        {
            // No exluding provided, then it is empty.
            if (excluding == null) excluding = new List<int>();

            // Initially take weight -1.
            double maxWeight = -1;
            List<int> bestDirs = new List<int>();
            for (int direction = 0; direction < 4; direction++)
            {
                if (excluding.Contains(direction)) continue;

                // Not going back when exploring.
                if (_lastPositions.Count != 0 && Utils.Str(_x + Utils.dWidth[direction], _y + Utils.dHeight[direction]) == _lastPositions.Peek())
                {
                    continue;
                }

                if (maxWeight < Weights.Values[_x, _y, direction])
                {
                    maxWeight = Weights.Values[_x, _y, direction];
                    bestDirs.Clear();
                    bestDirs.Add(direction);
                }
                else if (maxWeight == Weights.Values[_x, _y, direction])
                {
                    bestDirs.Add(direction);
                }
            }
            
            if (maxWeight <= 0)
            {
                bestDirs.Clear();
            }

            return bestDirs;
        }

        private void CreatePathToExit(int exitX, int exitY)
        {
            // Populate _pathToExit using Lee algorithm.
            int[,] borderedMap = new int[Utils.Maze.GetLength(0), Utils.Maze.GetLength(1)];
            Queue<KeyValuePair<int, int>> queue = new Queue<KeyValuePair<int, int>>();

            // Push the positon of the agent.
            queue.Enqueue(new KeyValuePair<int, int>(_x, _y));

            // The start is marked with 1.
            borderedMap[_x, _y] = 1;

            // While there are still places to visit.
            while(queue.Count > 0)
            {
                KeyValuePair<int, int> point = queue.Dequeue();
                // If it is the exit point, it is enough.
                if (point.Key == exitX && point.Value == exitY)
                {
                    break;
                }

                // Check all neighbours.
                for (int direction = 0; direction < 4; direction++)
                {
                    // Point must be reachable be agent (Excluds also the margins).
                    if (Weights.Values[point.Key, point.Value, direction] < 0.000000001) continue;

                    int newX = point.Key + Utils.dWidth[direction];
                    int newY = point.Value + Utils.dHeight[direction];

                    // Must be not visited.
                    if (borderedMap[newX, newY] != 0) continue;

                    // This is a neighbor that worths being visited.
                    borderedMap[newX, newY] = borderedMap[point.Key, point.Value] + 1;
                    queue.Enqueue(new KeyValuePair<int, int>(newX, newY));
                }
            }

            // Using the borderedMap values, recreate the path.
            int x = exitX;
            int y = exitY;
            _pathToExit.Push(Utils.Str(x, y));
            while (borderedMap[x, y] != 1)
            {
                // Choose the point with value equal to borderedMap[x,y] - 1.
                for(int direction = 0; direction < 4; direction++)
                {
                    int newX = x + Utils.dWidth[direction];
                    int newY = y + Utils.dHeight[direction];

                    // Must be withing the bounderies.
                    if (newX > -1 && newY > -1 && newX < Utils.Maze.GetLength(0) && newY < Utils.Maze.GetLength(1))
                    {
                        if (borderedMap[newX, newY] == borderedMap[x, y] - 1)
                        {
                            // We found the next point.
                            x = newX;
                            y = newY;
                            _pathToExit.Push(Utils.Str(x, y));
                            break;
                        }
                    }
                }
            }
        }
    }
}