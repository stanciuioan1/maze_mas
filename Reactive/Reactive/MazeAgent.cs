﻿using ActressMas;
using Message = ActressMas.Message;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using System.Timers;


namespace Reactive
{
    public class MazeAgent : Agent
    {
        private MazeForm _formGui;
        public List<ExplorerAgent> Explorers { get; set; }
        public Dictionary<string, string> ExplorerPositions { get; set; }
        public Dictionary<string, bool> ExplorerVisible { get; set; }
        public ExplorerAgent SelectedExplorer;
        private string _startPosition;
        private string _exitPosition;
        private System.Timers.Timer _spawnTimer;
        private bool exitFound = false;

        private enum State { Spawning, Normal }

        public MazeAgent()
        {
            Explorers = new List<ExplorerAgent>();
            ExplorerPositions = new Dictionary<string, string>();
            ExplorerVisible = new Dictionary<string, bool>();

            // Find out _startPosition by checking the Maze for value 2.
            // Find out _endPosition by checking the Maze for value 3.
            for (int i = 0; i < Utils.Size; i++)
            {
                for (int j = 0; j < Utils.Size; j++)
                {
                    if (Utils.Maze[i, j] == 2)
                    {
                        _startPosition = Utils.Str(i, j);
                    }
                    else if (Utils.Maze[i, j] == 3)
                    {
                        _exitPosition = Utils.Str(i, j);
                    }
                }
            }

            Thread t = new Thread(new ThreadStart(GUIThread));
            t.Start();

            _spawnTimer = new System.Timers.Timer();
            _spawnTimer.Elapsed += SpawnTimeOut;
            _spawnTimer.Interval = Utils.SpawnDelay;
        }

        public void SpawnTimeOut(object sender, ElapsedEventArgs e)
        {
            Send(Name, "[SpawnTimeout]");
        }

        private void GUIThread()
        {
            _formGui = new MazeForm();
            _formGui.SetOwner(this);
            _formGui.ShowDialog();
            Application.Run();
        }

        public override void Setup()
        {
            Console.WriteLine("Starting " + Name);
            
            // Todo: Is this required?
            List<string> resPos = new List<string>();
            resPos.Add(_startPosition); 
            resPos.Add(_exitPosition);

            // Explorers are not visible in the beginning.
            foreach(ExplorerAgent explorer in Explorers)
            {
                ExplorerVisible[explorer.Name] = false;
            }

            // Start spawn timer;
            _spawnTimer.Start();
        }

        public override void Act(Message message)
        {
            // Only general messages are received.
            if (message.ConversationId != "") return;

            Console.WriteLine("\t[{1} -> {0}]: {2}", this.Name, message.Sender, message.Content);

            string action; string parameters;
            Utils.ParseMessage(message.Content, out action, out parameters);

            switch (action)
            {
                case "[SpawnTimeout]":
                    HandleSpawn();
                    break;

                case "position":
                    HandlePosition(message.Sender, parameters);
                    break;

                case "change":
                    HandleChange(message.Sender, parameters);
                    break;

                default:
                    break;
            }
            _formGui.UpdatePlanetGUI();
        }

        private void HandleSpawn()
        {
            _spawnTimer.Stop();

            int numberOfAvailable = 0;
            foreach(string explorer in ExplorerVisible.Keys)
            {
                if (ExplorerVisible[explorer] == false)
                {
                    numberOfAvailable++;
                }
            }

            Console.WriteLine("Number of available: " + numberOfAvailable);

            if (numberOfAvailable > 0)
            {
                // Select next explorer to spawn.
                string nextExplorer = null;
                bool isStartFree = true;
                foreach (ExplorerAgent explorer in Explorers)
                {
                    if (ExplorerVisible[explorer.Name] == false)
                    {
                        nextExplorer = explorer.Name;
                        break;
                    }
                    if (ExplorerPositions.ContainsKey(explorer.Name) && ExplorerPositions[explorer.Name].Equals(_startPosition))
                    {
                        isStartFree = false;
                    }
                }

                // Explorer to be spawn exists and the start position is free.
                if (nextExplorer != null && isStartFree)
                {
                    // First explorer is selected to draw its wights.
                    if (SelectedExplorer == null)
                    {
                        foreach(ExplorerAgent explorer in Explorers)
                        {
                            if (explorer.Name == nextExplorer)
                            {
                                SelectedExplorer = explorer;
                                break;
                            }
                        }
                    }
                    numberOfAvailable--;
                    ExplorerVisible[nextExplorer] = true;
                    Send(nextExplorer, Utils.Str("spawn", _startPosition));
                }
            }

            Console.WriteLine("Number of after match: " + numberOfAvailable);

            // There are still agents to be spawn.
            if (numberOfAvailable > 0) {
                _spawnTimer.Start();
            }
        }

        private void HandlePosition(string sender, string position)
        {
            ExplorerPositions.Add(sender, position);
            Send(sender, "move");
        }

        private void HandleChange(string sender, string position)
        {
            ExplorerPositions[sender] = position;

            foreach (string k in ExplorerPositions.Keys)
            {
                if (k == sender)
                    continue;
                if (ExplorerPositions[k] == position)
                {
                    Send(sender, "block");
                    return;
                }
            }

            if (position == _exitPosition)
            {
                if (exitFound)
                { 
                    Send(sender, "exit");
                }
                else
                {
                    Send(sender, "found");
                    exitFound = true;
                }

                // Kill agent.
                ExplorerVisible[sender] = false;
                ExplorerPositions.Remove(sender);

                Console.WriteLine("Remaining explorers: {0}", ExplorerPositions.Count);
                if(ExplorerPositions.Count == 0)
                {
                    this.Stop();
                }
            } 
            else
            {
                Send(sender, "move");
            }
        }
    }
}