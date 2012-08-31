using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Firefly.Tests.Extensions;

namespace Firefly.Tests.Fakes
{
    using AppFunc = Func<IDictionary<string, object>, Task>;
    public class FakeApp
    {
        public FakeApp()
        {
            ResponseStatus = 200;
            ResponseHeaders = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            ResponseBody = new FakeResponseBody();
            ResponseProperties = new Dictionary<string, object>();
            OptionReadRequestBody = false;
            OptionCallResultImmediately = true;
        }

        public int CallCount { get; set; }
        public IDictionary<string, object> Env { get; set; }
        public Action ResultCallback { get; set; }
        public Action<Exception> FaultCallback { get; set; }


        public IDictionary<string, string[]> RequestHeaders { get; set; }
        public FakeRequestBody RequestBody { get; set; }

        public int ResponseStatus { get; set; }
        public string ResponseReasonPhrase { get; set; }
        public IDictionary<string, string[]> ResponseHeaders { get; set; }
        public IDictionary<string, object> ResponseProperties { get; set; }
        public FakeResponseBody ResponseBody { get; set; }

        public bool OptionReadRequestBody { get; set; }
        public bool OptionCallResultImmediately { get; set; }


        public Task Call(IDictionary<string, object> env)
        {
            CallCount += 1;

            Env = env;

            var tcs = new TaskCompletionSource<object>();
            ResultCallback = () =>
            {
                env["owin.ResponseStatusCode"] = ResponseStatus;
                env["owin.ResponseReasonPhrase"] = ResponseReasonPhrase;
                var headers = (IDictionary<string, string[]>)env["owin.ResponseHeaders"];
                foreach(var kv in ResponseHeaders)
                {
                    headers[kv.Key] = kv.Value;
                }
                var output = (Stream)env["owin.ResponseBody"];
                ResponseBody
                    .Subscribe(output)
                    .CopyResultToCompletionSource(tcs, null);
            };
            FaultCallback = tcs.SetException;

            RequestHeaders = (IDictionary<string, string[]>)env["owin.RequestHeaders"];
            RequestBody = new FakeRequestBody((Stream)env["owin.RequestBody"]);

            var task = TaskHelpers.Completed();
            if (OptionReadRequestBody)
            {
                // read request body to nowhere, then call back result
                task = task.Then(() => RequestBody.Subscribe(CancellationToken.None));
            }
            if (OptionCallResultImmediately)
            {
                task = task.Then(() => ResultCallback());
            }
            task.Catch(
                info =>
                {
                    FaultCallback(info.Exception);
                    return info.Handled();
                });
            return tcs.Task;
        }


        public string RequestHeader(string name)
        {
            string[] values;
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
            string[] values;
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
