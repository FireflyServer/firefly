using System;
using System.Collections.Generic;
using System.Linq;

namespace Firefly.Utils
{
    static class HeaderUtils
    {
        public static bool TryGet(this IDictionary<string, string[]> headers, string name, out string value)
        {
            string[] values;
            if (!headers.TryGetValue(name, out values) || values == null)
            {
                value = null;
                return false;
            }
            var count = values.Length;
            if (count == 0)
            {
                value = null;
                return false;
            }
            if (count == 1)
            {
                value = values[0];
                return true;
            }
            value = String.Join(",", values);
            return true;
        }
    }
}
