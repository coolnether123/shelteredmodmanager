using System;
using System.Collections.Generic;

namespace Cortex.Core.Models
{
    public sealed class RuntimeLogEntry
    {
        public long Sequence;
        public string EntryId;
        public DateTime Timestamp;
        public string Level;
        public string Category;
        public string Source;
        public string Message;
        public int ThreadId;
        public int UnityFrame;
        public int RepeatCount;
        public List<RuntimeStackFrame> StackFrames;

        public RuntimeLogEntry()
        {
            StackFrames = new List<RuntimeStackFrame>();
        }
    }
}
