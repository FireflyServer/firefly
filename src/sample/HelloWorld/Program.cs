using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Firefly.Http;
using Owin;

namespace HelloWorld
{
    class Program
    {
        static void Main(string[] args)
        {
            var server = new ServerFactory();
            using (server.Create(App, 8080))
            {
                Console.WriteLine("Running server on http://localhost:8080/");
                Console.WriteLine("Press enter to exit");
                Console.ReadLine();
            }
        }

        private static Task<ResultParameters> App(CallParameters call)
        {
            return TaskHelpers.FromResult(new ResultParameters
            {
                Status = 200,
                Headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    {"Content-Type", new[] {"text/plain"}}
                },
                Body = output =>
                {
                    var bytes = Encoding.Default.GetBytes("Hello world!");
                    output.Write(bytes, 0, bytes.Length);
                    return TaskHelpers.Completed();
                },
                Properties = new Dictionary<string, object>()
            });
        }
    }
}
