using System;
using System.Text;
using Dragonfly.Http;

namespace Profile.HeaderParser
{
    public class Strat4_GetStringSplit : Strat
    {
        public override bool TakeMessageHeader(Baton baton, out bool endOfHeaders)
        {
            endOfHeaders = false;
            var text = Encoding.Default.GetString(baton.Buffer.Array, baton.Buffer.Offset, baton.Buffer.Count);
            var lines = text.Split(new[] { "\r\n" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                if (line == "")
                {
                    endOfHeaders = true;
                    break;
                }
                var colonIndex = line.IndexOf(':');
                AddRequestHeader(line.Substring(0, colonIndex), line.Substring(colonIndex + 1));
            }
            return false;
        }
    }
}
