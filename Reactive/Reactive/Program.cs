using ActressMas;
using System.Threading;

namespace Reactive
{
    public class Program
    {
        private static void Main(string[] args)
        {
            EnvironmentMas env = new EnvironmentMas(0, 100);

            var mazeAgent = new MazeAgent();
            env.Add(mazeAgent, "maze");
            
            for (int i = 1; i <= Utils.NoExplorers; i++)
            {
                var explorerAgent = new ExplorerAgent();
                env.Add(explorerAgent, "explorer" + i);
                mazeAgent.Explorers.Add(explorerAgent);
            }

            Thread.Sleep(500);

            env.Start();
        }
    }
}