using Microsoft.AspNet.Http;
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNet.FeatureModel;

namespace Firefly
{
    /// <summary>
    /// Summary description for CallContext
    /// </summary>
    public class CallContext : 
        IHttpRequestFeature, 
        IHttpResponseFeature
    {
        public IFeatureCollection Features { private set; get; } = new FeatureCollection();
        public CallContext()
        {
            ((IHttpResponseFeature)this).StatusCode = 200;
            Features.Add(typeof(IHttpRequestFeature), this);
            Features.Add(typeof(IHttpResponseFeature), this);
        }

        Stream IHttpResponseFeature.Body { get; set; }

        Stream IHttpRequestFeature.Body { get; set; }

        IDictionary<string, string[]> IHttpResponseFeature.Headers { get; set; }

        IDictionary<string, string[]> IHttpRequestFeature.Headers { get; set; }

        bool IHttpResponseFeature.HeadersSent
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        string IHttpRequestFeature.Method { get; set; }

        string IHttpRequestFeature.Path { get; set; }

        string IHttpRequestFeature.PathBase { get; set; }

        string IHttpRequestFeature.Protocol { get; set; }

        string IHttpRequestFeature.QueryString { get; set; }

        string IHttpResponseFeature.ReasonPhrase { get; set; }

        string IHttpRequestFeature.Scheme { get; set; }

        int IHttpResponseFeature.StatusCode { get; set; }

        public void OnResponseCompleted(Action<object> callback, object state)
        {
            callback(state);
        }

        void IHttpResponseFeature.OnSendingHeaders(Action<object> callback, object state)
        {
        }
    }
}