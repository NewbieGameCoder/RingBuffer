using System;
using System.Threading;

public class RingBuffer<T>
{
    private volatile Entity[] buffer;
    private const int BufferSize = 128;
    private SpinLock pushLock = new SpinLock(false);
    private volatile int pushIndex;
    private volatile int popIndex;

    public RingBuffer()
    {
        pushIndex = 0;
        popIndex = -1;
        buffer = new Entity[BufferSize];
    }

    //Suggest One Producer.
    public bool Push(ref T value)
    {
        bool bTaken = false;
        try
        {
            pushLock.Enter(ref bTaken);
            if (!bTaken) return false;

            int tempPopIndex = popIndex;
            if (pushIndex == tempPopIndex || (pushIndex - tempPopIndex - 1 == buffer.Length))
            {
                Resize(tempPopIndex);
            }

            var entity = buffer[pushIndex] ?? new Entity();
            entity.value = value;
            Volatile.Write(ref buffer[pushIndex], entity);
            pushIndex = (pushIndex + 1) % buffer.Length;
        }
        finally
        {
            if (bTaken)
            {
                pushLock.Exit(true);
            }
        }
        return true;
    }

    public bool Pop(out T value)
    {
        value = default(T);
        int tempPopIndex = popIndex;
        int nextPopIndex = (tempPopIndex + 1) % buffer.Length;
        if (nextPopIndex != pushIndex)
        {
            if (Interlocked.CompareExchange(ref popIndex, nextPopIndex, tempPopIndex) != tempPopIndex)
            {
                return false;
            }

            var entity = Volatile.Read(ref buffer[nextPopIndex]);
            if (entity != null)
            {
                value = entity.value;
                return true;
            }
        }

        return false;
    }

    private void Resize(int tempPopIndex)
    {
        int oldLen = buffer.Length;
        var tempBuffer = new Entity[oldLen * 2];
        Array.ConstrainedCopy(buffer, 0, tempBuffer, 0, oldLen);
        buffer = tempBuffer;
        if (pushIndex == tempPopIndex && tempPopIndex != oldLen - 1)
        {
            for (int i = tempPopIndex; i < oldLen; ++i)
            {
                var newEntity = new Entity();
                newEntity.value = buffer[i].value;
                Volatile.Write(ref buffer[oldLen + i], newEntity);
            }
            popIndex += oldLen;
        }
    }

    class Entity
    {
        public T value;
    }
}