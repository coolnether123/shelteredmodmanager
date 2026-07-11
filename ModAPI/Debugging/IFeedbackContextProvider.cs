using System.Collections.Generic;

namespace ModAPI.Debugging
{
    /// <summary>
    /// Supplies host-specific context for a feedback entry without coupling the overlay to a game.
    /// Implementations should return inexpensive snapshot values; provider failures are recorded by the overlay.
    /// </summary>
    public interface IFeedbackContextProvider
    {
        /// <summary>Returns key/value facts to append to the next entry.</summary>
        IEnumerable<KeyValuePair<string, string>> GetContextLines();
    }
}
