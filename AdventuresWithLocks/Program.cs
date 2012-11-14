using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace AdventuresWithLocks
{
    class Program
    {
        static List<NamedStatefulObject> _namedStatefulObjects;

        static void Main(string[] args)
        {
            _namedStatefulObjects = new List<NamedStatefulObject>();
            for (int i = 0; i < 1000; i++)
                _namedStatefulObjects.Add(new NamedStatefulObject(Guid.NewGuid().ToString()));

            ExecuteAndOutputTime(NamedMutex, "FullBlownResourceLock");
            //ExecuteAndOutputTime(NamedMutex, "NamedMutex");
            //ExecuteAndOutputTime(InternedString, "InternedString");
            //ExecuteAndOutputTime(NamedLocker, "NamedLocker");
            //ExecuteAndOutputTime(NamedReaderWriterLocker, "NamedReaderWriterLocker");

            Console.ReadLine();
        }

        static object _fullBlownResourceLocker = new object();
        static void FullBlownResourceLock(NamedStatefulObject namedStatefulObject)
        {
            lock (_fullBlownResourceLocker)
            {
                if (GetInteractionType() == InteractionType.Read)
                    namedStatefulObject.ReadState();
                else
                    namedStatefulObject.ChangeState();
            }
        }

        static void NamedMutex(NamedStatefulObject namedStatefulObject)
        {
            //here a mutex is broadcast to the entire OS. Handy to share a named lock cross process but slow.
            //oh yeah, make sure to pick a unique name!
            using (var mutex = new Mutex(false, namedStatefulObject.UniqueName))
            {
                mutex.WaitOne();

                if (GetInteractionType() == InteractionType.Read)
                    namedStatefulObject.ReadState();
                else
                    namedStatefulObject.ChangeState();

                mutex.ReleaseMutex();
            }
        }

        static void InternedString(NamedStatefulObject namedStatefulObject)
        {
            lock (string.Intern(namedStatefulObject.UniqueName))
            {
                if (GetInteractionType() == InteractionType.Read)
                    namedStatefulObject.ReadState();
                else
                    namedStatefulObject.ChangeState();
            }
        }

        static readonly NamedLocker _namedlocker = new NamedLocker();
        static void NamedLocker(NamedStatefulObject namedStatefulObject)
        {
            lock (_namedlocker.GetLock(namedStatefulObject.UniqueName))
            {
                if (GetInteractionType() == InteractionType.Read)
                    namedStatefulObject.ReadState();
                else
                    namedStatefulObject.ChangeState();
            }
        }

        static readonly NamedReaderWriterLocker _namedReaderWriterLocker = new NamedReaderWriterLocker();
        static void NamedReaderWriterLocker(NamedStatefulObject namedStatefulObject)
        {
            var interactionType = GetInteractionType();
            var rwLock = _namedReaderWriterLocker.GetLock(namedStatefulObject.UniqueName);
            try
            {
                if (interactionType == InteractionType.Read)
                {
                    rwLock.EnterReadLock();
                    namedStatefulObject.ReadState();
                }
                else
                {
                    rwLock.EnterWriteLock();
                    namedStatefulObject.ChangeState();
                }
            }
            finally
            {
                if (interactionType == InteractionType.Read)
                    rwLock.ExitReadLock();
                else
                    rwLock.ExitWriteLock();
            }
        }

        //use all 4 processors on my machine
        private const int ThreadCount = 4;
        static void ExecuteAndOutputTime(Action<NamedStatefulObject> task, string taskName)
        {
            Console.WriteLine("Beginning: " + taskName);
            var stopWatch = new Stopwatch();

            stopWatch.Start();

            ThreadPool.SetMinThreads(ThreadCount, ThreadCount);
            Parallel.For(0, ThreadCount, new ParallelOptions { MaxDegreeOfParallelism = ThreadCount }, i =>
            {
                for (int j = 0; j < 200; j++)
                    foreach (var namedStatefulObject in _namedStatefulObjects)
                        task(namedStatefulObject);
            });

            stopWatch.Stop();
            Console.WriteLine("Completed in MS:");
            Console.WriteLine(stopWatch.ElapsedMilliseconds);
            Console.WriteLine();
            Console.WriteLine();
        }

        private const double WritePercentage = .25;
        static InteractionType GetInteractionType()
        {
            var randInt = (new Random()).Next(0, 101);

            if (randInt <= 100 * WritePercentage)
                return InteractionType.Write;

            return InteractionType.Read;
        }

        enum InteractionType
        {
            Read,
            Write
        }
    }


    public class NamedStatefulObject
    {
        private int _privateState = -1;
        public string UniqueName { get; private set; }

        public NamedStatefulObject(string uniqueName)
        {
            UniqueName = uniqueName;
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        public void ChangeState()
        {
            //do something VERY thread unsafe
            for (int i = 0; i < 10; i++)
            {
                if (++_privateState != i)
                    throw new Exception("The code is not thread safe");
            }
            _privateState = -1;
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        public void ReadState()
        {
            //read takes about the same amount of time as change
            var local = -1;
            for (int i = 0; i < 10; i++)
            {
                local++;
                if (local != i)
                    throw new Exception("The code is not thread safe");
            }
            local = -1;
        }
    }
}
