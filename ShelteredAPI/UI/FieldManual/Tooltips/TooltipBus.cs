using System;
using System.Collections.Generic;

namespace ShelteredAPI.UI.FieldManual.Tooltips
{
    /// <summary>
    /// Stack-based <see cref="ITooltipBus"/>. Most recent push wins; pop by token
    /// (so push order can be deeply nested without hover-exit lifetime quirks).
    /// </summary>
    internal sealed class TooltipBus : ITooltipBus
    {
        private struct Frame
        {
            public int Token;
            public TooltipMessage Message;
        }

        private readonly List<Frame> _stack = new List<Frame>();
        private int _nextToken = 1;
        private TooltipMessage _default = TooltipMessage.Empty;
        private TooltipMessage _lastPublished = TooltipMessage.Empty;

        public event Action<TooltipMessage> Changed;

        public TooltipMessage DefaultMessage
        {
            get { return _default; }
            set
            {
                _default = value;
                if (_stack.Count == 0) Publish(_default);
            }
        }

        public TooltipMessage Current
        {
            get { return _stack.Count == 0 ? _default : _stack[_stack.Count - 1].Message; }
        }

        public int Push(TooltipMessage message)
        {
            int token = _nextToken++;
            _stack.Add(new Frame { Token = token, Message = message });
            Publish(Current);
            return token;
        }

        public void Pop(int token)
        {
            if (token <= 0) return;
            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                if (_stack[i].Token == token)
                {
                    _stack.RemoveAt(i);
                    Publish(Current);
                    return;
                }
            }
        }

        public void Clear()
        {
            _stack.Clear();
            Publish(_default);
        }

        private void Publish(TooltipMessage msg)
        {
            // Cheap equality to avoid spurious refreshes.
            if (msg.Title == _lastPublished.Title
                && msg.Body == _lastPublished.Body
                && msg.Severity == _lastPublished.Severity)
                return;
            _lastPublished = msg;
            var handler = Changed;
            if (handler != null) handler(msg);
        }
    }
}
