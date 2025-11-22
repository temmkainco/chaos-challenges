namespace Core
{
    public static class DeterministicRandom
    {
        private static System.Random _random;

        public static void InitializeWithSeed(int seed)
        {
            _random = new System.Random(seed);
        }

        public static void InitializeWithTime()
        {
            var timeSeed = System.DateTime.UtcNow.Ticks.GetHashCode();
            _random = new System.Random(timeSeed);
        }

        private static void EnsureInitialized()
        {
            if (_random == null)
                InitializeWithTime();
        }

        public static int Next()
        {
            EnsureInitialized();
            return _random.Next();
        }

        public static int Next(int min, int max)
        {
            EnsureInitialized();
            return _random.Next(min, max);
        }

        public static int Next(int max)
        {
            EnsureInitialized();
            return _random.Next(0, max);
        }

        public static double NextDouble()
        {
            EnsureInitialized();
            return _random.NextDouble();
        }
    }
}