using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Dragonfly.Http;
using Gate.Owin;

namespace HelloWorld
{
    class Program
    {
        static void Main(string[] args)
        {
            var server = new ServerFactory();
            using (server.Create(App, 8080))
            {
                Console.WriteLine("Running server on http://localhost:8008/");
                Console.WriteLine("Press enter to exit");
                Console.ReadLine();
            }
        }

        private static void App(IDictionary<string, object> env, ResultDelegate result, Action<Exception> fault)
        {
            result(
                "200 OK",
                new Dictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase)
                    {
                        {"Content-Type", new[] {"text/plain"}}
                    },
                (write, error, end) =>
                    {
                        var bytes = Encoding.Default.GetBytes("Hello world!");
                        write(new ArraySegment<byte>(bytes), null);
                        end();
                        return () => { };
                    });
        }
    }
}
