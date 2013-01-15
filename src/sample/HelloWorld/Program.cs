using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Firefly.Http;
using Firefly.Utils;
using Gate;
using Gate.Middleware;
using Owin;
using Owin.Builder;

namespace HelloWorld
{
    using AppFunc = Func<IDictionary<string, object>, Task>;
    class Program
    {
        static void Main(string[] args)
        {
            var server = new ServerFactory(new ConsoleTrace());

            var builder = new AppBuilder();
            builder
                .UseFunc(ShowFormValues)
                .UseFunc(UrlRewrite("/", "/index.html"))
                .UseStatic()
                .Run(new Program());

            var app = builder.Build<AppFunc>();

            using (server.Create(app, 8080))
            {
                Console.WriteLine("Running server on http://localhost:8080/");
                Console.WriteLine("Press enter to exit");
                Console.ReadLine();
            }
        }

        public Task Invoke(IDictionary<string, object> env)
        {
            var res = new Response(env) { ContentType = "text/plain" };
            using (var writer = new StreamWriter(res.OutputStream))
            {
                writer.Write("Hello world!");
            }
            return TaskHelpers.Completed();
        }

        static AppFunc ShowFormValues(AppFunc next)
        {
            return env =>
            {
                var req = new Request(env);
                if (req.Method != "POST")
                {
                    return next(env);
                }

                var res = new Response(env) { ContentType = "text/plain" };
                return req.ReadFormAsync().Then(
                    form =>
                    {
                        using (var writer = new StreamWriter(res.OutputStream))
                        {
                            foreach (var kv in form)
                            {
                                writer.Write(kv.Key);
                                writer.Write(": ");
                                writer.WriteLine(kv.Value);
                            }
                        }
                    });
            };
        }

        static Func<AppFunc, AppFunc> UrlRewrite(string match, string replace)
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

    class ConsoleTrace : IServerTrace
    {
        public void Event(TraceEventType type, TraceMessage message)
        {
            Console.WriteLine("{0} {1}", type, message);
        }
    }
}
