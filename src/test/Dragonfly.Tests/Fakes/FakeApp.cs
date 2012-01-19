using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dragonfly.Utils;
using Gate.Owin;

namespace Dragonfly.Tests.Fakes
{
    public class FakeApp
    {
        public FakeApp()
        {
            ResponseStatus = "200 OK";
            ResponseHeaders = new Dictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase);
            ResponseBody = new FakeResponseBody();
        }

        public int CallCount { get; set; }
        public IDictionary<string, object> Env { get; set; }
        public IDictionary<string, IEnumerable<string>> RequestHeaders { get; set; }
        public FakeRequestBody RequestBody { get; set; }

        public string ResponseStatus { get; set; }
        public IDictionary<string, IEnumerable<string>> ResponseHeaders { get; set; }
        public FakeResponseBody ResponseBody { get; set; }

        public bool OptionReadRequestBody { get; set; }


        public void Call(IDictionary<string, object> env, ResultDelegate result, Action<Exception> fault)
        {
            CallCount += 1;
            Env = env;
            RequestHeaders = (IDictionary<string, IEnumerable<string>>)env["owin.RequestHeaders"];
            RequestBody = new FakeRequestBody((BodyDelegate)env["owin.RequestBody"]);

            if (OptionReadRequestBody)
            {
                RequestBody.Subscribe(
                    (data, continuation) => false,
                    fault,
                    () => result(ResponseStatus, ResponseHeaders, ResponseBody.Subscribe));
            }
            else
            {
                result(ResponseStatus, ResponseHeaders, ResponseBody.Subscribe);
            }
        }


        public string RequestHeader(string name)
        {
            IEnumerable<string> values;
            if (!RequestHeaders.TryGetValue(name, out values)
                || values == null
                || !values.Any())
            {
                return null;
            }
            return string.Join(",", values.ToArray());
        }
        public string ResponseHeader(string name)
        {
            IEnumerable<string> values;
            if (!ResponseHeaders.TryGetValue(name, out values)
                || values == null
                || !values.Any())
            {
                return null;
            }
            return string.Join(",", values.ToArray());
        }
    }
}
