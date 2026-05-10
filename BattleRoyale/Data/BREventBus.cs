using System;
using System.Collections.Generic;

namespace BattleRoyale
{
    public interface IBREvent { }

    public static class BREventBus
    {
        private static readonly Dictionary<Type, List<Delegate>> _handlers = new Dictionary<Type, List<Delegate>>();
        private static readonly object _lock = new object();

        public static void Subscribe<T>(Action<T> handler) where T : IBREvent
        {
            lock (_lock)
            {
                var key = typeof(T);
                if (!_handlers.ContainsKey(key))
                    _handlers[key] = new List<Delegate>();
                _handlers[key].Add(handler);
            }
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : IBREvent
        {
            lock (_lock)
            {
                var key = typeof(T);
                if (_handlers.TryGetValue(key, out var list))
                    list.Remove(handler);
            }
        }

        public static void Emit<T>(T evt) where T : IBREvent
        {
            List<Delegate> snapshot;
            lock (_lock)
            {
                if (!_handlers.TryGetValue(typeof(T), out var list))
                    return;
                snapshot = new List<Delegate>(list);
            }
            foreach (var h in snapshot)
                ((Action<T>)h)(evt);
        }
    }
}
