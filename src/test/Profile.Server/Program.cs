using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Firefly.Http;
using Firefly.Utils;

namespace Profile.Server
{
    internal class Tracer : IServerTrace
    {
        public void Event(TraceEventType type, TraceMessage message)
        {
#if DEBUG
            Console.WriteLine("[{0} {1}]", type, message);
#endif
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            using (new ServerFactory(new Tracer()).Create(App, 9090))
            {
                Console.WriteLine("Enter exit to exit:");
                for (; ; )
                {
                    var cmd = Console.ReadLine();
                    switch (cmd)
                    {
                        case "gc":
                            GC.Collect();
                            break;
                        case "exit":
                            return;
                    }
                }
            }
        }

        static Task<ResultParameters> App(CallParameters call)
        {
            var requestPath = (string)call.Environment[OwinConstants.RequestPath];
            switch (requestPath)
            {
                case "/baseline":
                    return Baseline(call);
                case "/favicon.ico":
                    return Baseline(call);
                case "/":
                    return Welcome(call);
                default:
                    return NotFound(call);
            }
        }

        private static Task<ResultParameters> NotFound(CallParameters call)
        {
            return TaskHelpers.FromResult(
                new ResultParameters
                {
                    Status = 404,
                    Properties = new Dictionary<string, object>(),
                    Headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                    {
                        {"Content-Type", new[] {"text/plain"}},
                        {"Content-Length", new[] {"0"}},
                    },
                    Body = output => TaskHelpers.Completed()
                });
        }

        private static Task<ResultParameters> Baseline(CallParameters call)
        {
            return TaskHelpers.FromResult(
                new ResultParameters
                {
                    Status = 200,
                    Properties = new Dictionary<string, object>(),
                    Headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                    {
                        {"Content-Type", new[] {"text/plain"}},
                        {"Content-Length", new[] {"8"}},
                    },
                    Body = output =>
                    {
                        var bytes = Encoding.Default.GetBytes("Baseline");
                        output.Write(bytes, 0, bytes.Length);
                        return TaskHelpers.Completed();
                    }
                });
        }

        private static Task<ResultParameters> Welcome(CallParameters call)
        {
            return TaskHelpers.FromResult(
                new ResultParameters
                {
                    Status = 200,
                    Properties = new Dictionary<string, object>(),
                    Headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                    {
                        {"Content-Type", new[] {"text/html"}},
                        {"Content-Length", new[] {WelcomeText.Length.ToString(CultureInfo.InvariantCulture)}},
                    },
                    Body = output =>
                    {
                        var bytes = Encoding.Default.GetBytes(WelcomeText);
                        output.Write(bytes, 0, bytes.Length);
                        return TaskHelpers.Completed();
                    }
                });
        }

        private static string WelcomeText =
            @"
<!DOCTYPE html PUBLIC ""-//W3C//DTD XHTML 1.0 Strict//EN"" ""http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd"">
<html xmlns=""http://www.w3.org/1999/xhtml"">
<head>
<meta http-equiv=""Content-Type"" content=""text/html; charset=iso-8859-1"" />
<title>IIS7</title>
<style type=""text/css"">
<!--
body {
	color:#000000;
	background-color:#B3B3B3;
	margin:0;
}

#container {
	margin-left:auto;
	margin-right:auto;
	text-align:center;
	}

a img {
	border:none;
}

-->
</style>
</head>
<body>
<div id=""container"">
<a href=""http://go.microsoft.com/fwlink/?linkid=66138&amp;clcid=0x409""><img src=""http://localhost/welcome.png"" alt=""IIS7"" width=""571"" height=""411"" /></a>
</div>
</body>
</html>";
    }
}
