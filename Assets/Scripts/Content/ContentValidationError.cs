using System;

namespace RPG.Content
{
    /// <summary>One authored-content problem. <see cref="Code"/> is stable so tests and editor tooling can assert on
    /// the specific rule that failed, while <see cref="Source"/> names the asset that produced it.</summary>
    public readonly struct ContentValidationError
    {
        public ContentValidationError(string code, string message, string source)
        {
            if (string.IsNullOrEmpty(code)) throw new ArgumentException("A validation error requires a code.", nameof(code));
            if (string.IsNullOrEmpty(message)) throw new ArgumentException("A validation error requires a message.", nameof(message));

            Code = code;
            Message = message;
            Source = source ?? string.Empty;
        }

        public string Code { get; }

        public string Message { get; }

        public string Source { get; }

        public override string ToString() =>
            Source.Length == 0 ? $"{Code}: {Message}" : $"{Code}: {Message} [{Source}]";
    }
}
