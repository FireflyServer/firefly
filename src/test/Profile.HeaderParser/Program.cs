using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Dragonfly.Http;

namespace Profile.HeaderParser
{
    class Program
    {
        static IEnumerable<Func<Strat>> Strats()
        {
            yield return () => new Strat0();
            yield return () => new Strat1_Current();
            yield return () => new Strat2_Current();
            yield return () => new Strat3_FixedBytePtr();
            yield return () => new Strat4_GetStringSplit();
            yield return () => new Strat5_3_WithNoDictionary();
            yield return () => new Strat6_3_WithListDictionary();
        }

        static void Main(string[] args)
        {
            var datas = File.ReadAllText("Headers.txt")
                .Split(new[] { "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(chunk => Encoding.Default.GetBytes(chunk + "\r\n\r\nAll of the data"))
                .ToArray();

            for (; ; )
            {
                var samples = Enumerable.Range(0, 10);

                var measures = Strats()
                    .Select(s => new
                    {
                        Strat = s,
                        Stopwatch = samples
                            .Select(x => new Stopwatch())
                            .ToArray()
                    }).ToArray();

                Console.WriteLine("Starting");
                foreach (var sample in samples)
                {
                    foreach (var data in datas)
                    {
                        foreach (var measure in measures)
                        {
                            Action after = () => { };
                            measure.Stopwatch[sample].Start();
                            foreach (var loop in Enumerable.Range(0, 10000))
                            {
                                var strat = measure.Strat();

                                var baton = new Baton
                                                {
                                                    Buffer = new ArraySegment<byte>(data),
                                                    Complete = false,
                                                    Next = Connection.Next.ReadMore
                                                };
                                var endOfHeaders = false;
                                while (!endOfHeaders)
                                {
                                    if (!strat.TakeMessageHeader(baton, out endOfHeaders))
                                    {
                                        break;
                                    }
                                }


                                if (loop == 0 && sample == 0)
                                {
                                    after = () =>
                                                {
                                                    Console.WriteLine("{0}", strat.GetType().Name);
                                                    foreach (var kv in strat.Headers)
                                                    {
                                                        Console.WriteLine("  {0}: {1}", kv.Key, kv.Value);
                                                    }
                                                };
                                }
                            }
                            measure.Stopwatch[sample].Stop();
                            after();
                        }
                    }
                }

                foreach (var measure in measures)
                {

                    Console.WriteLine(
                        "{0} {1}\r\n  {2}",
                        measure.Strat().GetType().Name,
                        measure.Stopwatch
                            .Aggregate(TimeSpan.Zero, (a, b) => a.Add(b.Elapsed)),
                        measure.Stopwatch
                            .Select(x => x.ElapsedTicks)
                            .OrderBy(x => x)
                            .Aggregate("", (a, b) => a + " " + b));
                }
                Console.WriteLine("Done");
                Console.ReadLine();
            }
        }
    }
}
