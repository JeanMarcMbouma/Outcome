using System.Text;
using System.Text.RegularExpressions;

namespace BbQ.Outcome
{
    /// <summary>
    /// Formats message templates with named placeholders (e.g. <c>"User {UserId} not found"</c>)
    /// by replacing them positionally with the supplied arguments, similar to how <c>ILogger</c> works.
    /// </summary>
    internal static partial class ErrorMessageFormatter
    {
        public static string Format(string template, object?[] args)
        {
            if (args.Length == 0)
                return template;

            var index = 0;
            var sb = new StringBuilder(template.Length);
            var span = template.AsSpan();
            var pos = 0;

            while (pos < span.Length)
            {
                var open = span[pos..].IndexOf('{');
                if (open < 0)
                {
                    sb.Append(span[pos..]);
                    break;
                }

                open += pos;

                // Escaped {{ → literal {
                if (open + 1 < span.Length && span[open + 1] == '{')
                {
                    sb.Append(span[pos..(open + 1)]);
                    pos = open + 2;
                    continue;
                }

                var close = span[open..].IndexOf('}');
                if (close < 0)
                {
                    sb.Append(span[pos..]);
                    break;
                }

                close += open;

                sb.Append(span[pos..open]);

                if (index < args.Length)
                    sb.Append(args[index++]);
                else
                    sb.Append(span[open..(close + 1)]); // leave placeholder as-is if no arg

                pos = close + 1;
            }

            return sb.ToString();
        }
    }
}
