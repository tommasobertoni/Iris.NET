using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Iris.NET
{
    [Serializable]
    public class Test
    {
        public string Message { get; set; }

        public object Data { get; set; }

        public override string ToString() => $"This was a test {Message}";
    }
}
