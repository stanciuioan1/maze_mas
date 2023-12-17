using ActressMas;
using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;

namespace Reactive
{
    public class ExplorerAgent : Agent
    {
        private int _x, _y;
        private Stack<string> _lastPositions;
        private Stack<string> _pathToExit;
        private List<int> _nextDirections;
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
                case "what_state":
                    HandleWhatState(message.Sender, parameters);
                    break;

                case "state":
                    HandleState(parameters);
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

        private void HandleWhatState(string sender, List<string> parameters)
        {
            Send(sender, Utils.Str("state", _state.ToString()));
        }

        private void HandleState(List<string> parameters)
        {
            // In the meantime, exit state could be imposed. Do nothing in this case.
            if (_state == State.Exit) return;

            if (parameters[0] == State.Exploring.ToString())
            {
                // Create directions in which to move with exceptions of going back.
                _nextDirections = GetNextDirectionsOrdered(_lastPositions.ToList());

                // Explore directions and try to get there.
                ExecuteExploringStrategy();
            } 
            else if (parameters[0] == State.DeadEnd.ToString())
            {
                _state = State.DeadEnd;
                ExecuteDeadEndStrategy();
            }
        }

        private void HandleExit(List<string> parameters, bool isFirst)
        {
            _x = int.Parse(parameters[0]);
            _y = int.Parse(parameters[1]);

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

            // Todo: Delete that.
            StringBuilder stringBuilder = new StringBuilder("My position: (" + _x + ", " + _y + ")" + 
                System.Environment.NewLine + "Path to exit:");

            foreach (string position in _pathToExit)
            {
                stringBuilder.Append(" ");
                stringBuilder.Append("(" + position + ")");
            }
            Console.WriteLine(stringBuilder.ToString());
            // End todo.
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

            // Do nothing if value was previously 0.
            if (oldValue < 0.00001) return;

            // Value must be higher than 0.
            Weights.Values[avoidX, avoidY, avoidDir] = Math.Max(0.01, oldValue - 0.1);
        }

        private void HandleMove(List<string> parameters)
        {
            Console.WriteLine("{0}: My state in the beginning of move is {1}.", Name, _state.ToString());

            // This is triggered when the position has been accepted.
            int oldX = _x;
            int oldY = _y;
            _x = int.Parse(parameters[0]);
            _y = int.Parse(parameters[1]);

            if (_state == State.NotStarted)
            {
                _state = State.Exploring;

                // Create directions in which to move with exceptions of going back.
                _nextDirections = GetNextDirectionsOrdered();

                // Explore directions and try to get there.
                ExecuteExploringStrategy();
            }
            else if (_state == State.Exploring)
            {
                // Push the last into the _lastPositions stack.
                _lastPositions.Push(Utils.Str(oldX, oldY));

                // Send to all that position has been explored.
                int dir = Utils.GetDirrerentialDirection(oldX, oldY, _x, _y);
                if (dir != -1)
                {
                    Broadcast(Utils.Str("exploring", oldX, oldY, dir), true, "explorers_channel");
                }

                // Create directions in which to move with exceptions of going back.
                _nextDirections = GetNextDirectionsOrdered(_lastPositions.ToList());

                // Explore directions and try to get there.
                ExecuteExploringStrategy();
            }
            else if (_state == State.DeadEnd)
            {
                string lastPosition = Utils.Str(oldX, oldY);

                // Send to all to avoid that position.
                // If I were to go back, avoid this direction.
                int dir = Utils.GetDirrerentialDirection(_x, _y, oldX, oldY);
                Weights.Values[_x, _y, dir] = 0;
                Broadcast(Utils.Str("avoid", _x, _y, dir), false, "explorers_channel");

                ExecuteDeadEndStrategy(lastPosition);
            }
            else if (_state == State.Exit)
            {
                // The first point it's its position.
                _pathToExit.Pop();

                // Successfully moved one position, get next one.
                string nextPosition = _pathToExit.Peek();

                Send("maze", Utils.Str("try_move", nextPosition));
            }

            Console.WriteLine("{0}: My state in the end of move is {1}.", Name, _state.ToString());
        }

        private void HandleBlock(List<string> parameters)
        {
            // If the state is Exploring, a block means that the lane is ocupied. Should check for a different position or return.
            if (_state == State.Exploring)
            {
                // Todo: delete this.
                StringBuilder builder = new StringBuilder(Name + ": Blocked in exploring. Showcase my _nextDirections:" + System.Environment.NewLine);
                foreach (int direction in _nextDirections)
                {
                    builder.Append(direction + " ");
                }
                Console.WriteLine(builder.ToString());
                // End todo.

                // Check if we stucked with another exploring agent.
                if (_nextDirections.Count > 0 && parameters.Count > 0)
                {
                    // We are, exchange informations with the agent.
                    // If he is in deadline, we change our strategy to DeadEnd.
                    // If he is exploring, we will wait for him to move forward.
                    Send(parameters[0], "what_state");

                    // The next of the interaction will be held with respect to the result of the communication.
                    return;
                }

                ExecuteExploringStrategy();
            } 
            else if (_state == State.DeadEnd)
            {
                // Try again, it is a matter of time until it will be free.
                Send("maze", Utils.Str("try_move", _lastPositions.Peek()));
            }
            else if (_state == State.Exit)
            {
                // Try again, it is a matter of time until it will be free. (or is it?)
                Send("maze", Utils.Str("try_move", _pathToExit.Peek()));
            }
        }

        private void ExecuteExploringStrategy()
        {
            // If no direction is available, change state to DeadEnd.
            if (_nextDirections.Count == 0)
            {
                _state = State.DeadEnd;
                Send("maze", Utils.Str("try_move", _lastPositions.Peek()));
            }
            else
            {
                // Else, go through the best direction available.
                int bestDir = _nextDirections[_nextDirections.Count - 1];
                int bestX = _x + Utils.dWidth[bestDir];
                int bestY = _y + Utils.dHeight[bestDir];
                _nextDirections.RemoveAt(_nextDirections.Count - 1);
                Send("maze", Utils.Str("try_move", bestX, bestY));
            }
        }

        private void ExecuteDeadEndStrategy(string lastPosition = null)
        {
            // Get rid of the last position only if we successfully moved.
            if (_lastPositions.Peek() == Utils.Str(_x, _y))
            {
                _lastPositions.Pop();
            }

            // Get the available directions besides moving back on the already explored path.
            List<string> exclude = new List<string>(_lastPositions);
            if (lastPosition != null)
            {
                exclude.Add(lastPosition);
            }
            List<int> availableDirections = GetNextDirectionsOrdered(exclude);

            // Keeping going backwards.
            // If no more last positions, then it means that we are on the start position.
            // Go back to exploring state. Could lead to being blocked? TODO.
            if (availableDirections.Count == 0 && _lastPositions.Count != 0)
            {
                Send("maze", Utils.Str("try_move", _lastPositions.Peek()));
            }
            else
            {
                // There is a directions we can go. Change state to Exploring.
                Console.WriteLine("{0}: Entering Exploring mode within DeadEnd Strategy.", Name);

                _state = State.Exploring;

                // Update directions.
                _nextDirections = availableDirections;

                // Explore directions and try to get there.
                ExecuteExploringStrategy();
            }
        }

        private List<int> GetNextDirectionsOrdered(List<string> exclude = null)
        {
            // Todo: Delete this.
            StringBuilder stringBuilder1 = new StringBuilder("Adiacent directions:" + System.Environment.NewLine);
            for(int dir = 0; dir < 4; dir ++)
            {
                stringBuilder1.Append("Direction: " + dir);
                stringBuilder1.Append(" - Weight: " + Weights.Values[_x, _y, dir]);
                stringBuilder1.Append(System.Environment.NewLine);
            }
            Console.Write(stringBuilder1.ToString());
            // Todo - end.

            if (exclude == null) exclude = new List<string>();

            List<int> nextDirections = new List<int>();
            
            // This will randomize the direction chosen in the case of equal weights.
            int[] shuffledDirections = (new int[] { 0, 1, 2, 3 }).OrderBy(x => Guid.NewGuid()).ToArray();

            for (int index = 0; index < 4; index ++)
            {
                int direction = shuffledDirections[index];

                // Check if the weight is higher than 0.
                if (Weights.Values[_x, _y, direction] <= 0.0001) continue;

                int newX = _x + Utils.dWidth[direction];
                int newY = _y + Utils.dHeight[direction];

                // Check if the position is in the exclusion list.
                if (exclude.Contains(Utils.Str(newX, newY))) continue;
                nextDirections.Add(direction);
            }

            // Sort the nextDirections desc by the weight.
            Comparison<int> compareDirectionWeights = new Comparison<int>((dir1, dir2) => {
                return Weights.Values[_x, _y, dir1].CompareTo(Weights.Values[_x, _y, dir2]);
            });
            nextDirections.Sort(compareDirectionWeights);

            // Todo: Delete this.
            StringBuilder stringBuilder = new StringBuilder("Sorted list asc for the nextDirections:" + System.Environment.NewLine);
            foreach (int dir in nextDirections)
            {
                stringBuilder.Append("Direction: " + dir);
                stringBuilder.Append(" - Weight: " + Weights.Values[_x, _y, dir]);
                stringBuilder.Append(System.Environment.NewLine);
            }
            Console.Write(stringBuilder.ToString());
            // Todo - end.

            return nextDirections;
        }

        /*private void MoveRandomly()
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
*/

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

                    // Must be withinn the bounderies.
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