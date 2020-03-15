using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace DotNetEnv
{
    internal class Parser
    {
        private static Regex ExportRegex = new Regex("^\\s*export\\s+");
        private static Regex VariableRegex = new Regex("\\$[0-9a-zA-Z_]+");

        private static bool IsComment(string line)
        {
            return line.Trim().StartsWith("#");
        }

        private static string GetQuotedValue(string input, string delimeter)
        {
            if (input.Contains(delimeter))
            {
                int quoteStart = input.IndexOf(delimeter);
                int quoteEnd = input.LastIndexOf(delimeter)+1;
                return input.Substring(quoteStart, quoteEnd);
            }
            return input;
        }
        private static string RemoveInlineComment(string line)
        {
            string value = GetQuotedValue(line, "\"");
            value = GetQuotedValue(value, "\'");

            if (IsQuoted(value))
                return value;
      
            int pos = value.IndexOf('#');
            return pos >= 0 ? value.Substring(0, pos) : line;
        }

        private static string RemoveExportKeyword(string line)
        {
            Match match = ExportRegex.Match(line);
            return match.Success ? line.Substring(match.Length) : line;
        }

        private static string ParseVariables(Vars vars, string line)
        {
            var result = line;
            while (true)
            {
                var match = VariableRegex.Match(result);
                if (match.Success)
                {
                    var key = match.Groups[0].Value.Substring(1);
                    var value = vars.ContainsKey(key) ? vars[key] : Environment.GetEnvironmentVariable(key);
                    result = Replace(result, match.Index, match.Length, value);
                }
                else
                {
                    break;
                }
            }

            return result;
        }

        private static string Replace(string s, int index, int length, string replacement)
        {
            var builder = new StringBuilder();
            builder.Append(s.Substring(0, index));
            builder.Append(replacement);
            builder.Append(s.Substring(index + length));
            return builder.ToString();
        }

        public static Vars Parse(
            string[] lines,
            bool trimWhitespace = true,
            bool isEmbeddedHashComment = true,
            bool unescapeQuotedValues = true,
            bool parseVariables = true
        )
        {
            Vars vars = new Vars();

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                // skip comments
                if (IsComment(line))
                    continue;

                line = RemoveExportKeyword(line);

                string[] keyValuePair = line.Split(new char[] { '=' }, 2);

                // skip malformed lines
                if (keyValuePair.Length != 2)
                    continue;

                if (isEmbeddedHashComment)
                {
                    keyValuePair[1] = RemoveInlineComment(keyValuePair[1]);
                }

                if (trimWhitespace)
                {
                    keyValuePair[0] = keyValuePair[0].Trim();
                    keyValuePair[1] = keyValuePair[1].Trim();
                }

                if (unescapeQuotedValues && IsQuoted(keyValuePair[1]))
                {
                    keyValuePair[1] = Unescape(
                        keyValuePair[1].Substring(1, keyValuePair[1].Length - 2)
                    );
                }

                if (parseVariables)
                {
                    keyValuePair[1] = ParseVariables(vars, keyValuePair[1]);
                }

                vars.Add(
                    keyValuePair[0],
                    keyValuePair[1]
                );
            }

            return vars;
        }

        private static bool IsQuoted(string s)
        {
            return s.Length > 1 && (
                (s[0] == '"' && s[s.Length - 1] == '"')
                || (s[0] == '\'' && s[s.Length - 1] == '\'')
            );
        }

        // copied from https://stackoverflow.com/questions/6629020/evaluate-escaped-string/25471811#25471811
        private static string Unescape(string s)
        {
            StringBuilder sb = new StringBuilder();
            Regex r = new Regex("\\\\[abfnrtv?\"'\\\\]|\\\\[0-3]?[0-7]{1,2}|\\\\u[0-9a-fA-F]{4}|\\\\U[0-9a-fA-F]{8}|.");
            MatchCollection mc = r.Matches(s, 0);

            foreach (Match m in mc)
            {
                if (m.Length == 1)
                {
                    sb.Append(m.Value);
                }
                else
                {
                    if (m.Value[1] >= '0' && m.Value[1] <= '7')
                    {
                        int i = Convert.ToInt32(m.Value.Substring(1), 8);
                        sb.Append((char)i);
                    }
                    else if (m.Value[1] == 'u')
                    {
                        int i = Convert.ToInt32(m.Value.Substring(2), 16);
                        sb.Append((char)i);
                    }
                    else if (m.Value[1] == 'U')
                    {
                        int i = Convert.ToInt32(m.Value.Substring(2), 16);
                        sb.Append(char.ConvertFromUtf32(i));
                    }
                    else
                    {
                        switch (m.Value[1])
                        {
                            case 'a':
                                sb.Append('\a');
                                break;
                            case 'b':
                                sb.Append('\b');
                                break;
                            case 'f':
                                sb.Append('\f');
                                break;
                            case 'n':
                                sb.Append('\n');
                                break;
                            case 'r':
                                sb.Append('\r');
                                break;
                            case 't':
                                sb.Append('\t');
                                break;
                            case 'v':
                                sb.Append('\v');
                                break;
                            default:
                                sb.Append(m.Value[1]);
                                break;
                        }
                    }
                }
            }

            return sb.ToString();
        }
    }
}
