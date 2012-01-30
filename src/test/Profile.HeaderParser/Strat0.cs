using System.Text;
using Firefly.Http;

namespace Profile.HeaderParser
{
    public class Strat0 : Strat
    {
        public override bool TakeMessageHeader(Baton baton, out bool endOfHeaders)
        {
            endOfHeaders = false;
            return false;
        }
    }
}
