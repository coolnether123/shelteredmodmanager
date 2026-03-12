namespace Cortex.Shared
{
    public static class CompletionAugmentationPromptContract
    {
        public const string StrictCodeCompletionInstruction =
            "Complete only the code that should be inserted at the cursor. Return code only with no markdown or explanation. " +
            "Preserve the surrounding style, syntax, and naming conventions. Use only identifiers, members, namespaces, and patterns " +
            "that appear in the supplied prefix, suffix, declarations, and related snippets. Do not invent new methods, properties, " +
            "helper functions, comments, or business rules. Prefer the smallest continuation that completes the current statement or block. " +
            "Start exactly at the cursor on the current line and do not rewrite text before the caret, on the current line, or on previous lines. " +
            "If the cursor is inside a partially typed construct, complete that local construct before adding later statements. " +
            "Do not repeat completed code immediately above the cursor. If the correct continuation is unclear, return an empty string.";
    }
}
