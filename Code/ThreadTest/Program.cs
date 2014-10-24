using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ThreadTest
{
    internal static class Program
    {
        private const string tempFilePreffix = "temp_";

        private static int fileIndex = 0;

        private static readonly string testContents = string.Join("!", Enumerable.Range(0, 1000));

        private static void Main()
        {
            var actions = new Action[]
            {
                /*Actions.SleepOneMinuteAction, Actions.Download5Sites, Actions.Download20Sites, */ /*Actions.Sleep5SecondsAction,*/ 
                Actions.DiskLoad,
                Actions.ProcessorLoad,
                Actions.ProcessorLoadAndDiskLoad,
                Actions.ProcessorLoadSleepAndDiskLoad
            };

            var executors = new Action<Action, int>[] { Executors.ThreadPoolParallelExecutor, Executors.DirectThreadExecutor, Executors.PLinq };

            using (var outFile = File.Open("out.csv", FileMode.Create))
            {
                using (var outStream = new StreamWriter(outFile))
                {
                    outStream.WriteLine("Action, Elements count, Executor, Seconds");

                    outStream.AutoFlush = true;

                    foreach (var count in Enumerable.Range(0, 64).Select(pow => (int)(Math.Pow(2, ((double)pow) * 2 / 4))))
                    {
                        foreach (var action in actions)
                        {
                            foreach (var executor in executors)
                            {
                                var timer = Stopwatch.StartNew();

                                executor(action, count);

                                var outLine = string.Format("{0},{1},{2},{3}", action.Method.Name, count, executor.Method.Name, (long)timer.Elapsed.TotalSeconds);
                                outStream.WriteLine(outLine);
                                Console.Out.WriteLine(outLine);

                                Directory.GetFiles(Environment.CurrentDirectory, tempFilePreffix + "*").Select(f => new FileInfo(f).FullName).ToList().ForEach(File.Delete);
                            }
                        }
                    }
                }
            }
        }

        private static class Actions
        {
            internal static void Sleep15SecondsAction()
            {
                Thread.Sleep(TimeSpan.FromSeconds(15));
            }

            internal static void Sleep5SecondsAction()
            {
                Thread.Sleep(TimeSpan.FromSeconds(5));
            }

            internal static void SleepOneMinuteAction()
            {
                Thread.Sleep(TimeSpan.FromMinutes(1));
            }

            internal static void ProcessorLoadSleepAndDiskLoad()
            {
                ProcessorLoad();
                Sleep15SecondsAction();
                DiskLoad();
            }

            internal static void ProcessorLoadAndDiskLoad()
            {
                ProcessorLoad();
                DiskLoad();
            }

            internal static void ProcessorLoad()
            {
                int result = 0;

                for (int i = 0; i < 10000000; i++)
                {
                    Interlocked.Increment(ref result);
                }
            }

            internal static void DiskLoad()
            {
                var fileName = tempFilePreffix + Interlocked.Increment(ref fileIndex);

                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
                }

                for (int i = 0; i < 1000; i++)
                {
                    File.AppendAllText(fileName, testContents);
                }
            }

            private static void DownloadNSites(int count)
            {
                using (var client = new WebClient())
                {
                    Enumerable.Range(0, count).Sum(i => client.DownloadData("http://microsoft.com").Length);
                }
            }


            internal static void Download5Sites()
            {
                DownloadNSites(5);
            }

            internal static void Download20Sites()
            {
                DownloadNSites(20);
            }
        }

        private static class Executors
        {
            internal static void PLinq(Action action, int count)
            {
                Enumerable.Range(0, count).AsParallel().WithExecutionMode(ParallelExecutionMode.ForceParallelism).ForAll(i => action());
            }

            internal static void DirectThreadExecutor(Action action, int count)
            {
                var threads = Enumerable.Range(0, count)
                    .Select(i => new Thread(o => action())
                    {
                        IsBackground = false
                    }).ToList();

                threads.ForEach(t => t.Start());

                threads.ForEach(t => t.Join());
            }

            internal static void ThreadPoolParallelExecutor(Action action, int count)
            {
                int itemsLeft = count;

                Enumerable.Range(0, count).ToList().ForEach(i =>
                    ThreadPool.QueueUserWorkItem(state =>
                    {
                        action();
                        Interlocked.Decrement(ref itemsLeft);
                    }));

                while (Volatile.Read(ref itemsLeft) > 0)
                {
                    Thread.Sleep(500);
                }
            }
        }
    }
}
