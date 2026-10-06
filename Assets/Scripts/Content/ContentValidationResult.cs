using System.Collections.Generic;
using System.Text;

namespace RPG.Content
{
    /// <summary>The outcome of validating authored content. Errors keep the order they were reported in, so a
    /// deterministic builder produces a deterministic report.</summary>
    public sealed class ContentValidationResult
    {
        private readonly List<ContentValidationError> _errors;

        public ContentValidationResult(IReadOnlyList<ContentValidationError> errors)
        {
            _errors = new List<ContentValidationError>();
            if (errors == null) return;

            for (var index = 0; index < errors.Count; index++)
                _errors.Add(errors[index]);
        }

        public IReadOnlyList<ContentValidationError> Errors => _errors;

        public bool IsValid => _errors.Count == 0;

        public string Describe()
        {
            if (IsValid) return "Content is valid.";

            var builder = new StringBuilder();
            builder.Append(_errors.Count).Append(_errors.Count == 1 ? " content error" : " content errors");
            for (var index = 0; index < _errors.Count; index++)
                builder.Append('\n').Append("- ").Append(_errors[index]);

            return builder.ToString();
        }
    }
}
