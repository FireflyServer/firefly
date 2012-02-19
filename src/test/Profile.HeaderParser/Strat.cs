using System;
using System.Collections.Generic;
using Firefly.Http;

namespace Profile.HeaderParser
{
    public abstract class Strat
    {
        protected Strat()
        {
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        protected Strat(IDictionary<string, string> headers)
        {
            Headers = headers;
        }

        public virtual void AddRequestHeader(string name, string value)
        {
            Headers.Add(name, value);
        }

        public IDictionary<string, string> Headers { get; set; }

        public abstract bool TakeMessageHeader(Baton baton, out bool endOfHeaders);
    }
}
