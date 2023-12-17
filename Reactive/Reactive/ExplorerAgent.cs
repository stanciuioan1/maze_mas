using ActressMas;
using System;
using System.Collections.Generic;
using System.Linq;

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

            switch (action)
            {
                case "do_action":
                    HandleAction(parameters);
                    break;

                case "move":
                    HandleMove(parameters);
                    break;

                case "avoid":
                    HandleAvoid(parameters);
                    break;

                case "exploring":
                    HandleExplored(parameters);
                    break;

                case "block":
                    HandleBlock(parameters);
                    break;

                case "what_state":
                    HandleWhatState(message.Sender, parameters);
                    break;

                case "state":
                    HandleState(parameters);
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
            }
        }

        /// <summary>
        /// Action responsible with analising the current state and decide the next move.
        /// </summary>
        /// <param name="parameters"> Not used yet </param>
        private void HandleAction(List<string> parameters)
        {
            // The position did not change. Maybe being here after being blocked. (Can make a variable to check that)
            Console.WriteLine("{0}: HandleAction: State[{1}], Position({2}, {3})", Name, _state.ToString(), _x, _y);

            if (_state == State.Exploring)
            {
                // Don t know yet what to do.
                // Maybe just explore strategy.
                ExecuteExploringStrategy();
            }
            else if (_state == State.DeadEnd)
            {
                // Don t know yet what to do.
                // Maybe just try to go to the last position.
                ExecuteDeadEndStrategy();
            }
            else if (_state == State.Exit)
            {
                // Maybe just try to go to the next position to the exit.
                Send("maze", Utils.Str("try_move", _pathToExit.Peek()));
            }
        }

        /// <summary>
        /// Upon moving successfully, this method is called. Its responsability is to decide the next move with respect to the current state.
        /// </summary>
        /// <param name="parameters"></param>
        private void HandleMove(List<string> parameters)
        {
            Console.WriteLine("{0}: HandleMove: State[{1}], OldPosition({2}, {3}), NewPosition({4}, {5})", Name, _state.ToString(), _x, _y, parameters[0], parameters[1]);

            int oldX = _x;
            int oldY = _y;
            _x = int.Parse(parameters[0]);
            _y = int.Parse(parameters[1]);

            if (_state == State.NotStarted)
            {
                // Going to exploration <3.
                _state = State.Exploring;

                // Search for the next available directions with no exception.
                _nextDirections = GetNextDirectionsOrdered();

                // The state will be treated in the HandleAction() method.
                Send(Name, "do_action");

                // Explore directions and try to get there.
                // ExecuteExploringStrategy();
            }
            else if (_state == State.Exploring)
            {
                // Push the last into the _lastPositions stack.
                _lastPositions.Push(Utils.Str(oldX, oldY));

                // Send to all that position has been explored.
                int dir = Utils.GetDirrerentialDirection(oldX, oldY, _x, _y);
                Broadcast(Utils.Str("exploring", oldX, oldY, dir), true, "explorers_channel");

                // Create directions in which to move with exceptions of going back.
                _nextDirections = GetNextDirectionsOrdered(_lastPositions.ToList());

                // Explore directions and try to get there.
                ExecuteExploringStrategy();
            }
            else if (_state == State.DeadEnd)
            {
                // Send to all to avoid that position.
                // If I were to go back, avoid this direction.
                int dir = Utils.GetDirrerentialDirection(_x, _y, oldX, oldY);
                Weights.Values[_x, _y, dir] = 0;
                Broadcast(Utils.Str("avoid", _x, _y, dir), false, "explorers_channel");

                ExecuteDeadEndStrategy();
            }
            else if (_state == State.Exit)
            {
                // The first point it's its position.
                _pathToExit.Pop();

                // Successfully moved one position, get next one.
                string nextPosition = _pathToExit.Peek();

                Send("maze", Utils.Str("try_move", nextPosition));
            }
        }

        /// <summary>
        /// Method executed when the direction is blocked by an agent.
        /// Require some comunication between them to unblock if there is no other way to go.
        /// </summary>
        /// <param name="parameters"></param>
        private void HandleBlock(List<string> parameters)
        {
            Console.WriteLine("{0}: HandleBlock: State[{1}], Position({2}, {3}), Other[{4}]", Name, _state.ToString(), _x, _y, parameters[0]);

            // Should stop and reconsider state in each case?

            // If the state is Exploring, a block means that the lane is ocupied.
            if (_state == State.Exploring)
            {
                // Check if we stucked with another exploring agent and there is no other means of movement.
                if (_nextDirections.Count == 0 && parameters.Count > 0)
                {
                    // We are exchangeing informations with the agent.
                    Send(parameters[0], "what_state");
                }
                else
                {
                    ExecuteExploringStrategy();
                }
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

        /// <summary>
        /// Method used when communication between explorers emarge.
        /// One agent has found a dead end and will communicate to others to not go that direction.
        /// </summary>
        /// <param name="parameters"> Contains 3 items: X, Y, direction </param>
        private void HandleAvoid(List<string> parameters)
        {
            // When going to exit, there is a predefined path.
            if (_state == State.Exit) return;

            int avoidX = int.Parse(parameters[0]);
            int avoidY = int.Parse(parameters[1]);
            int avoidDir = int.Parse(parameters[2]);

            Weights.Values[avoidX, avoidY, avoidDir] = 0;
        }

        /// <summary>
        /// When exploring message is sent by another explorer.
        /// Update the path by slightly decreasing the value.
        /// </summary>
        /// <param name="parameters"> Contains 3 components: X, Y, directions </param>
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

        /// <summary>
        /// Other agents could ask this one for the state and position.
        /// Methid is responsible for sending back the requested information.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="parameters"></param>
        private void HandleWhatState(string sender, List<string> parameters)
        {
            Send(sender, Utils.Str("state", _state.ToString(), _x, _y));
        }

        /// <summary>
        /// When Block is received by agent while being in the Exploring phase, agent will communicate with the blocking one.
        /// This method is invoked upon receiving the requested information.
        /// </summary>
        /// <param name="parameters"></param>
        private void HandleState(List<string> parameters)
        {
            Console.WriteLine("{0}: HandleState: MyState[{1}], Position({2}, {3}), OtherState[{4}]", Name, _state.ToString(), _x, _y, parameters[0]);

            if (_state == State.DeadEnd && _state == State.Exit)
            {
                // If in the dead end state, no matter the case, try again.
                Send(Name, "do_action");
                return;
            }

            if (_state == State.Exploring && parameters[0] == State.DeadEnd.ToString())
            {
                // If meeting with an DeadEnd, change state to dead end.
                int otherX = int.Parse(parameters[1]);
                int otherY = int.Parse(parameters[2]);

                // Todo: Mark the direction as not visitable.
                int dir = Utils.GetDirrerentialDirection(_x, _y, otherX, otherY);
                Weights.Values[_x, _y, dir] = 0;

                _state = State.DeadEnd;
                Send(Name, "do_action");
                return;
            }

            if (_state == State.Exploring && parameters[0] == State.Exploring.ToString())
            {
                // What to do now? Both are exploring. Doing the same for now.
                // Could be an error here?
                Send(Name, "do_action");
                return;
            }
        }

        /// <summary>
        /// Method responsible with handling the case when agent entered the exit map position.
        /// </summary>
        /// <param name="parameters"></param>
        /// <param name="isFirst"> If it is the first agent to enter exit, it will inform others. </param>
        private void HandleExit(List<string> parameters, bool isFirst)
        {
            _x = int.Parse(parameters[0]);
            _y = int.Parse(parameters[1]);

            // Send to all others the position if it was first time discovered.
            if (isFirst)
            {
                Broadcast(Utils.Str("come", _x, _y), false, "explorers_channel");
            }

            // Stop the agent.
            Console.WriteLine("{0}: Stopped", Name);
            this.Stop();
        }

        /// <summary>
        /// Method responsible for handling the case when one agent has reached exit and anounced this one about the location.
        /// </summary>
        /// <param name="parameters"></param>
        private void HandleCome(List<string> parameters)
        {
            // Exit has been found.
            _state = State.Exit;

            // Create path to the exit using the weights and the provided location.
            int exitX = int.Parse(parameters[0]);
            int exitY = int.Parse(parameters[1]);
            CreatePathToExit(exitX, exitY);
        }

        private void ExecuteExploringStrategy()
        {
            // If no direction is available, change state to DeadEnd.
            if (_nextDirections.Count == 0)
            {
                _state = State.DeadEnd;
                Send(Name, "do_action");
            }
            else
            {
                // Else, go through the best direction available. (Should we just drop here the last direction?)
                // We could wait for a move in order to rewrite. Or better a block one, because this is the only one affecttng.
                // In what conditions do we need to check a direction 2 times? TODO.
                int bestDir = _nextDirections[_nextDirections.Count - 1];
                int bestX = _x + Utils.dWidth[bestDir];
                int bestY = _y + Utils.dHeight[bestDir];
                _nextDirections.RemoveAt(_nextDirections.Count - 1);
                Send("maze", Utils.Str("try_move", bestX, bestY));
            }
        }

        private void ExecuteDeadEndStrategy()
        {
            // Get rid of the last position only if we successfully moved.
            if (_lastPositions.Peek() == Utils.Str(_x, _y))
            {
                _lastPositions.Pop();
            }

            // Get the available directions besides moving back on the already explored path.
            List<string> exclude = new List<string>(_lastPositions);
            List<int> availableDirections = GetNextDirectionsOrdered(exclude);

            // Only going backward is available or no direction.
            if (availableDirections.Count == 0)
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

                Send(Name, "do_action");
            }
        }

        private List<int> GetNextDirectionsOrdered(List<string> exclude = null)
        {
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
            return nextDirections;
        }

        /// <summary>
        /// Method responsible for creating the shortest path from agent to exit.
        /// </summary>
        /// <param name="exitX"></param>
        /// <param name="exitY"></param>
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