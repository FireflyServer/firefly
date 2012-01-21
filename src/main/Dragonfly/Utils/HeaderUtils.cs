using System;
using System.Collections.Generic;
using System.Linq;

namespace Dragonfly.Utils
{
    static class HeaderUtils
    {
        public static bool TryGet(this IDictionary<string, IEnumerable<string>> headers, string name, out string value)
        {
            IEnumerable<string> values;
            if (!headers.TryGetValue(name, out values) || values == null)
            {
                value = null;
                return false;
            }
            var count = values.Count();
            if (count == 0)
            {
                value = null;
                return false;
            }
            if (count == 1)
            {
                value = values.Single();
                return true;
            }
            value = String.Join(",", values.ToString());
            return true;
        }
    }
}
