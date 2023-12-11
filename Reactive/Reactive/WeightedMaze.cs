namespace Reactive
{
    public class WeightedMaze
    {
        public double[,,] Values { get; }
        public int Width { get => Values.GetLength(0); }
        public int Height { get => Values.GetLength(1); }
        public int Directions { get => Values.GetLength(2); }

        public WeightedMaze(int width, int height)
        {
            Values = new double[width, height, 4];
        }
    }
}
