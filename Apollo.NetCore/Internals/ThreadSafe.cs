using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Apollo.NetCore.Internals
{
    public class ThreadSafe<T> where T : class
    {
        private T _value;
        public ThreadSafe(T value)
        {
            _value = value;
        }

        public void WriteFullFence(T newValue)
        {
            _value = newValue;
            Interlocked.MemoryBarrier();
        }

        public T ReadFullFence()
        {
            var value = _value;
            Interlocked.MemoryBarrier();
            return value;
        }

        public T AtomicExchange(T newValue)
        {
            return Interlocked.Exchange(ref _value, newValue);
        }
    }
}
