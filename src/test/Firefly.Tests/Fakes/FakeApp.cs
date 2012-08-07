using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Owin;
using Firefly.Tests.Extensions;

namespace Firefly.Tests.Fakes
{
    public class FakeApp
    {
        public FakeApp()
        {
            ResponseStatus = 200;
            ResponseHeaders = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            ResponseBody = new FakeResponseBody();
            OptionReadRequestBody = false;
            OptionCallResultImmediately = true;
        }

        public int CallCount { get; set; }
        public IDictionary<string, object> Env { get; set; }
        public Action<ResultParameters> ResultCallback { get; set; }
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


        public Task<ResultParameters> Call(CallParameters call)
        {
            CallCount += 1;

            Env = call.Environment;

            var tcs = new TaskCompletionSource<ResultParameters>();
            ResultCallback = tcs.SetResult;
            FaultCallback = tcs.SetException;

            RequestHeaders = call.Headers;
            RequestBody = new FakeRequestBody(call.Body);

            var task = TaskHelpers.Completed();
            if (OptionReadRequestBody)
            {
                // read request body to nowhere, then call back result
                task = task.Then(() => RequestBody.Subscribe(CancellationToken.None));
            }
            if (OptionCallResultImmediately)
            {
                task = task.Then(() => ResultCallback(new ResultParameters
                {
                    Status = ResponseStatus,
                    Headers = ResponseHeaders,
                    Body = ResponseBody.Subscribe,
                    Properties = ResponseProperties
                }));
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
