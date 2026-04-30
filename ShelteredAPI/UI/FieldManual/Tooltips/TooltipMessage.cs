namespace ShelteredAPI.UI.FieldManual.Tooltips
{
    internal enum TooltipSeverity
    {
        Info = 0,
        Hint = 1,
        Warning = 2
    }

    /// <summary>
    /// Immutable description of one tooltip. A short title plus an optional body.
    /// Severity drives presentation (color), not behavior.
    /// </summary>
    internal struct TooltipMessage
    {
        public readonly string Title;
        public readonly string Body;
        public readonly TooltipSeverity Severity;

        public TooltipMessage(string title, string body, TooltipSeverity severity)
        {
            Title = title;
            Body = body;
            Severity = severity;
        }

        public static TooltipMessage Info(string title, string body)
        {
            return new TooltipMessage(title, body, TooltipSeverity.Info);
        }

        public static TooltipMessage Hint(string body)
        {
            return new TooltipMessage(null, body, TooltipSeverity.Hint);
        }

        public bool IsEmpty
        {
            get { return string.IsNullOrEmpty(Title) && string.IsNullOrEmpty(Body); }
        }

        public static readonly TooltipMessage Empty = new TooltipMessage(null, null, TooltipSeverity.Info);
    }
}
