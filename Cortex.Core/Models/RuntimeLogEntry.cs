using System;
using System.Collections.Generic;

namespace Cortex.Core.Models
{
    public sealed class RuntimeLogEntry
    {
        public DateTime Timestamp;
        public string Level;
        public string Category;
        public string Source;
        public string Message;
        public List<RuntimeStackFrame> StackFrames;

        public RuntimeLogEntry()
        {
            StackFrames = new List<RuntimeStackFrame>();
        }
    }
}
