using System;

namespace ModAPI.Networking.Tests
{
    internal sealed class TestCase
    {
        public string Name;
        public Action Body;

        public TestCase(string name, Action body)
        {
            Name = name;
            Body = body;
        }
    }
}
