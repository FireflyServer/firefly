using System;
using Gate.Owin;

namespace HelloWorld
{
    static class Workaround
    {
#pragma warning disable 169
        private static Action _1;
        private static Func<int, int, int> _2;
#pragma warning restore 169
    }
}
