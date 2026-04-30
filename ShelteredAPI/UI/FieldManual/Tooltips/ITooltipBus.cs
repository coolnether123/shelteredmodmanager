using System;

namespace ShelteredAPI.UI.FieldManual.Tooltips
{
    /// <summary>
    /// Broker between hover-trigger publishers and tooltip-display subscribers.
    /// Implementations track the order tooltips are pushed and expose the topmost
    /// as <see cref="Current"/>. When the stack is empty, <see cref="Current"/> falls
    /// back to <see cref="DefaultMessage"/>.
    /// </summary>
    internal interface ITooltipBus
    {
        /// <summary>The default message shown when nothing is hovered.</summary>
        TooltipMessage DefaultMessage { get; set; }

        /// <summary>The currently active message (top of stack, or default).</summary>
        TooltipMessage Current { get; }

        /// <summary>
        /// Pushes a message and returns an opaque token. Callers must hold the token
        /// and pass it to <see cref="Pop"/> when their hover ends.
        /// </summary>
        int Push(TooltipMessage message);

        /// <summary>
        /// Removes the message associated with <paramref name="token"/>. Safe to call
        /// with a stale or unknown token (no-op).
        /// </summary>
        void Pop(int token);

        /// <summary>Clears all pushed messages without firing intermediate events.</summary>
        void Clear();

        /// <summary>Fired whenever <see cref="Current"/> changes.</summary>
        event Action<TooltipMessage> Changed;
    }
}
