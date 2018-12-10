using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PonscripterParser
{
    public class RPatterns
    {
        public const string IDENTIFIER_PATTERN = @"[a-zA-Z_]+[0-9a-zA-Z_]*";
    }
    public class RClickWait : SemanticRegexChangeMode
    {
        public RClickWait() : base(@"\G@", TokenType.ClickWait, LexingMode.Normal) { }
    }

    public class RPageWait : SemanticRegexChangeMode
    {
        public RPageWait() : base(@"\G\\", TokenType.PageWait, LexingMode.Normal) { }
    }

    public class RIgnoreNewLine : SemanticRegexChangeMode
    {
        public RIgnoreNewLine() : base(@"\G\/", TokenType.IgnoreNewLine, LexingMode.Normal) { }
    }

    public class RText : SemanticRegex
    {
        public RText() : base(@"\G[^@\\\/]+") { }

        public override SemanticRegexResult DoMatch(string s, int startat, LexingMode currentLexingMode)
        {
            Match m = pattern.Match(s, startat);

            if (m.Success)
            {
                if (currentLexingMode == LexingMode.Normal)
                {
                    Console.WriteLine("WARNING: matched text while in 'normal' mode");
                }

                return new SemanticRegexResult(TokenType.Text, m.Value, LexingMode.Normal);
            }

            return null;
        }
    }

    public class RString : SemanticRegexSameMode
    {
        //this is just \G"[^"]+"
        public RString() : base(@"\G""[^""]+""", TokenType.Literal) { }
    }

    public class RNumber : SemanticRegexSameMode
    {
        public RNumber() : base(@"\G\d+", TokenType.Literal) { }
    }

    public class ROperator : SemanticRegexSameMode
    {
        public ROperator() : base(@"\G[\+\-\*\/]", TokenType.Operator) { }
    }

    public class RBracket : SemanticRegexSameMode
    {
        public RBracket() : base(@"\G[\(\)]", TokenType.Colon) { }
    }

    public class RComma : SemanticRegexSameMode
    {
        public RComma() : base(@"\G,", TokenType.Comma) { }
    }

    public class RAlias : SemanticRegexSameMode
    {
        public RAlias() : base(@"\G" + RPatterns.IDENTIFIER_PATTERN, TokenType.Alias) { }
    }

    public class RLabel : SemanticRegexSameMode
    {
        public RLabel() : base(@"\G\*" + RPatterns.IDENTIFIER_PATTERN, TokenType.Alias) { }
    }

    public class RStringVariable : SemanticRegexSameMode
    {
        public RStringVariable() : base(@"\G\$" + RPatterns.IDENTIFIER_PATTERN, TokenType.StringVar) { }
    }

    public class RNumericVariable : SemanticRegexSameMode
    {
        public RNumericVariable() : base(@"\G\%" + RPatterns.IDENTIFIER_PATTERN, TokenType.NumericVar) { }
    }

    public class RColon : SemanticRegex
    {
        public RColon() : base(@"\G:") { }

        public override SemanticRegexResult DoMatch(string s, int startat, LexingMode currentLexingMode)
        {
            Match m = pattern.Match(s, startat);
            return m.Success ? new SemanticRegexResult(TokenType.Colon, m.Value, LexingMode.Normal) : null;
        }
    }

    public class RHat : SemanticRegex
    {
        public RHat() : base(@"\G\^") { }

        public override SemanticRegexResult DoMatch(string s, int startat, LexingMode currentLexingMode)
        {
            Match m = pattern.Match(s, startat);
            return m.Success ? new SemanticRegexResult(TokenType.Colon, m.Value, LexingMode.Text) : null;
        }
    }

    public class RFunctionCall : SemanticRegex
    {
        public static Dictionary<string, int> functionNames = new Dictionary<string, int>() {
            { "lsp", 3 },
            { "dwave", 2 },
            { "langen", 0 },
            {"langjp", 0 },
            {"getparam", -1 },  //-1 indicates varags
            //later these functions should be populated dynamically by scanning function defs!
            {"dwave_eng", 2 },
        };

        public RFunctionCall() : base(@"\G[!a-zA-Z_]+[0-9!a-zA-Z_]*")
        {
        }

        //for now, the caller should always decide whether to change lexing mode, not in the calee
        public override SemanticRegexResult DoMatch(string s, int startat, LexingMode currentLexingMode)
        {
            Match m = pattern.Match(s, startat);

            //For now, just treat anything that looks like a function as a function. Should revert this later
            if (m.Success)
            {
                bool found = functionNames.TryGetValue(m.Value, out int value);

                if (found)
                {
                    //if the function takes no arguments, immediately transition to normal mode
                    return new SemanticRegexResult(TokenType.FnCall, m.Value, value == 0 ? LexingMode.Normal : LexingMode.ExpressionStart);
                }
                else
                {
                    Console.WriteLine($"WARNING: [{m.Value}] looks like a function, but not found. Ignoring.");
                }
            }

            return null;
        }
    }
}
