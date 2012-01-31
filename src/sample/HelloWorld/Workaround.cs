using System;

namespace HelloWorld
{
    static class Workaround
    {
#pragma warning disable 169
        private static Action _1;
        private static Func<int, int, int> _2;
        private static Func<int, int> _3;
#pragma warning restore 169
    }
}
