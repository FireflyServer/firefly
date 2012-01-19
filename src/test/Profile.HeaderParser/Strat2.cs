using System.Text;
using Dragonfly.Http;

namespace Profile.HeaderParser
{
    public class Strat2_Current : Strat
    {
        public override bool TakeMessageHeader(Baton baton, out bool endOfHeaders)
        {
            var remaining = baton.Buffer;
            endOfHeaders = false;
            if (remaining.Count < 2) return false;
            var ch0 = remaining.Array[remaining.Offset];
            var ch1 = remaining.Array[remaining.Offset + 1];
            if (ch0 == '\r' && ch1 == '\n')
            {
                endOfHeaders = true;
                baton.Skip(2);
                return true;
            }

            if (remaining.Count < 3) return false;
            var wrappedHeaders = false;
            var colonIndex = -1;
            var valueStartIndex = -1;
            var valueEndIndex = -1;
            for (var index = 0; index != remaining.Count - 2; ++index)
            {
                var ch2 = remaining.Array[remaining.Offset + index + 2];
                if (ch0 == '\r' &&
                    ch1 == '\n' &&
                    ch2 != ' ' &&
                    ch2 != '\t')
                {
                    var name = Encoding.Default.GetString(remaining.Array, remaining.Offset, colonIndex);
                    var value = "";
                    if (valueEndIndex != -1)
                        value = Encoding.Default.GetString(remaining.Array, remaining.Offset + valueStartIndex, valueEndIndex - valueStartIndex);
                    if (wrappedHeaders)
                        value = value.Replace("\r\n", " ");
                    AddRequestHeader(name, value);
                    baton.Skip(index + 2);
                    return true;
                }
                if (colonIndex == -1 && ch0 == ':')
                {
                    colonIndex = index;
                }
                else if (colonIndex != -1 &&
                    ch0 != ' ' &&
                    ch0 != '\t' &&
                    ch0 != '\r' &&
                    ch0 != '\n')
                {
                    if (valueStartIndex == -1)
                        valueStartIndex = index;
                    valueEndIndex = index + 1;
                }
                else if (!wrappedHeaders &&
                    ch0 == '\r' &&
                    ch1 == '\n' &&
                    (ch2 == ' ' ||
                    ch2 == '\t'))
                {
                    wrappedHeaders = true;
                }

                ch0 = ch1;
                ch1 = ch2;
            }
            return false;
        }
    }
}
