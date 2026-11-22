using System;
using System.Collections.Generic;

namespace LastArchive
{
    // Web requests run synchronously under each player's lock. Terminal and desktop games use the default scope.
    internal sealed class GameScope
    {
        private static readonly GameScope Default = new();
        [ThreadStatic] private static GameScope _current;

        public static GameScope Current => _current ?? Default;
        public Dictionary<Type, List<Delegate>> Subscribers { get; } = new();
        public Dictionary<Type, object> Services { get; } = new();

        public T Run<T>(Func<T> action)
        {
            var previous = _current;
            _current = this;
            try
            {
                return action();
            }
            finally
            {
                _current = previous;
            }
        }
    }
}
