using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Firefly.Http;
using Gate;
using Gate.Builder;
using Gate.Middleware;
using Owin;

namespace HelloWorld
{
    class Program
    {
        static void Main(string[] args)
        {
            var server = new ServerFactory();

            var app = new AppBuilder().Build<AppDelegate>(
                builder => builder
                    .Use(ShowFormValues())
                    .Use(UrlRewrite("/", "/index.html"))
                    .UseStatic()
                    .Run(App));

            using (server.Create(app, 8080))
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

        static Func<AppDelegate, AppDelegate> ShowFormValues()
        {
            return app => call =>
            {
                var req = new Request(call);
                if (req.Method != "POST")
                    return app(call);

                return TaskHelpers.FromResult(
                    new ResultParameters
                    {
                        Status = 200,
                        Headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                        {
                            {"Content-Type", new[] {"text/plain"}}
                        },
                        Body = output => req.ReadFormAsync()
                            .Then(
                                form =>
                                {
                                    using (var writer = new StreamWriter(output))
                                    {
                                        foreach (var kv in form)
                                        {
                                            writer.Write(kv.Key);
                                            writer.Write(": ");
                                            writer.WriteLine(kv.Value);
                                        }
                                    }
                                }),
                        Properties = new Dictionary<string, object>()
                    });
            };
        }

        static Func<AppDelegate, AppDelegate> UrlRewrite(string match, string replace)
        {
            return app => call =>
            {
                var req = new Request(call);
                if (req.Path == match)
                    req.Path = replace;
                return app(call);
            };
        }
    }
}
