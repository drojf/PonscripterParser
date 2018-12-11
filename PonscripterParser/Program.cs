using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PonscripterParser
{
    class NamedRegex
    {
        public Regex regex;
        public TokenType tokenType;

        public NamedRegex(Regex r, TokenType t) {
            regex = r;
            tokenType = t;
        }
    }

    class Program
    {
        //must use \G to use in conjunction with 'startat' parameter of Match - apparently ^ means start of string ONLY, not startof 'startat'
        //public static readonly Regex clickWait = new Regex(@"\G@", RegexOptions.IgnoreCase);
        public static readonly Regex WHITESPACE_REGEX = new Regex(@"\G\s+", RegexOptions.IgnoreCase);
        public static string IDENTIFIER_PATTERN = @"[a-zA-Z_]+[0-9a-zA-Z_]*";

        public static NamedRegex R_CLICK_WAIT = new NamedRegex(new Regex(@"\G@"), TokenType.ClickWait);
        public static NamedRegex R_PAGE_WAIT = new NamedRegex(new Regex(@"\G\\"), TokenType.PageWait);
        public static NamedRegex R_IGNORE_NEW_LINE = new NamedRegex(new Regex(@"\G\/"), TokenType.IgnoreNewLine);
        public static NamedRegex R_TEXT = new NamedRegex(new Regex(@"\G[^@\\\/]+"), TokenType.Text);
        public static NamedRegex R_STRING = new NamedRegex(new Regex(@"\G""[^""]+"""), TokenType.Literal);
        public static NamedRegex R_NUMBER = new NamedRegex(new Regex(@"\G\d+"), TokenType.Literal);
        public static NamedRegex R_OPERATOR = new NamedRegex(new Regex(@"\G[\+\-\*\/]"), TokenType.Operator);
        public static NamedRegex R_BRACKET = new NamedRegex(new Regex(@"\G[\(\)]"), TokenType.Bracket);
        public static NamedRegex R_COMMA = new NamedRegex(new Regex(@"\G,"), TokenType.Comma);
        public static NamedRegex R_ALIAS = new NamedRegex(new Regex(@"\G" + IDENTIFIER_PATTERN), TokenType.Alias);
        public static NamedRegex R_LABEL = new NamedRegex(new Regex(@"\G\*" + IDENTIFIER_PATTERN), TokenType.Label);
        public static NamedRegex R_STRING_VARIABLE = new NamedRegex(new Regex(@"\G\$" + IDENTIFIER_PATTERN), TokenType.StringVar);
        public static NamedRegex R_NUMERIC_VARIABLE = new NamedRegex(new Regex(@"\G\%" + IDENTIFIER_PATTERN), TokenType.NumericVar);
        public static NamedRegex R_COLON = new NamedRegex(new Regex(@"\G:"), TokenType.Colon);
        public static NamedRegex R_HAT = new NamedRegex(new Regex(@"\G\^"), TokenType.Hat);
        //public static NamedRegex R_FUNCTION_CALL = new NamedRegex(new Regex(@"\G[!a-zA-Z_]+[0-9!a-zA-Z_]*"), TokenType.FnCall);

        public static Regex FUNCTION_CALL_REGEX = new Regex(@"\G[!a-zA-Z_]+[0-9!a-zA-Z_]*");
        /*public static readonly Regex langEnAtStartOfLine = new Regex(@"^\s*langen", RegexOptions.IgnoreCase);

        //NOTE: use https://regex101.com/ for testing/debugging these regexes (or rewrite using another type of pattern matching library)
        private static readonly List<NamedRegex> PossibleMatches = new List<NamedRegex>()
        {
            //matches the   [langen] command
            new NamedRegex(MatchType.langen,           @"\Glangen\s*", RegexOptions.IgnoreCase),
            //matches       [ : '] with some optional space on either side
            new NamedRegex(MatchType.colon,            @"\G\s*:\s*", RegexOptions.IgnoreCase),
            //matches       [dwave 0, hid_1e139]
            new NamedRegex(MatchType.dwaveAlias,       @"\Gdwave\s+\d+\s*,\s*\w+", RegexOptions.IgnoreCase),
            //matches       [dwave 0, "filepath\goes\here"]
            new NamedRegex(MatchType.dwavePath,        @"\Gdwave\s+\d+\s*,\s*""[^""]+?""", RegexOptions.IgnoreCase),
            //matches       [dwave 0, hid_1e139]
            new NamedRegex(MatchType.voiceDelayOrWait, @"\G((voicedelay)|(voicewait)) \d+", RegexOptions.IgnoreCase),
            //matches text, [^  And this isn't some small amount we're talkin's about.^@]
            new NamedRegex(MatchType.text,             @"(\^.*?)(@|\\\x10?\s*|\/|$)", RegexOptions.IgnoreCase), //@"\G\^.*?(@|\\(\x10)?\s*|(/\s*$)|$)", RegexOptions.IgnoreCase),
            //matches the pagewait symbol "\" (sometimes there are text enders without a text start
            new NamedRegex(MatchType.pageWait,         @"\G\\", RegexOptions.IgnoreCase),
            //matches the clickwait symbol "@"
            new NamedRegex(MatchType.clickWait,        @"\G@", RegexOptions.IgnoreCase),
            //matches !sd or !s0,!s100 etc.
            new NamedRegex(MatchType.textSpeed,        @"\G!s(d|(\d+))", RegexOptions.IgnoreCase),
            //matches !w100 or !d1000
            new NamedRegex(MatchType.waitOrDelay,      @"\G((!w)|(!d))\d+", RegexOptions.IgnoreCase),
            //matches a / at the end of a line (which disables the newline)
            new NamedRegex(MatchType.disableNewLine,   @"\G/\s*$", RegexOptions.IgnoreCase),
            //matches a color change command (6 digit hex, starting with #)
            new NamedRegex(MatchType.changeColor,      @"\G#[0-9abcdef]{6}\s*", RegexOptions.IgnoreCase),
            //semicolon comment at end of line. The game script actually allows semicolons inside text, so can't pre-filter for comments
            new NamedRegex(MatchType.comment,          @"\G\s*;.*$", RegexOptions.IgnoreCase),
            //whitespace at end of line. NOTE: the game ignores this, so nothing is emitted
            new NamedRegex(MatchType.whitespace_before_newline,       @"\G[\s\x10]+$", RegexOptions.IgnoreCase),
        };*/
        public static Dictionary<string, int> functionNames = new Dictionary<string, int>() {
            { "lsp", 3 },
            { "dwave", 2 },
            { "langen", 0 },
            {"langjp", 0 },
            {"getparam", -1 },  //-1 indicates varags
            //later these functions should be populated dynamically by scanning function defs!
            {"dwave_eng", 2 },
        };

        public static SemanticRegexResult SemanticRegexResultOrNull(NamedRegex nregex, string s, int startat, LexingMode newLexingMode)
        {
            Match m = nregex.regex.Match(s, startat);
            return m.Success ? new SemanticRegexResult(nregex.tokenType, m.Value, newLexingMode) : null;
        }

        //check for a function call. go back to normal mode if function has 0 arguments, otherwise go to function mode
        public static SemanticRegexResult CheckFunctionCall(string s, int startat)
        {
            Match m = FUNCTION_CALL_REGEX.Match(s, startat);

            bool found = functionNames.TryGetValue(m.Value, out int value);

            if (found)
            {
                //TODO: raise flag for user defined functions, as in those cases we're not sure how many arguments it takes

                //if the function takes no arguments, immediately transition to normal mode
                return new SemanticRegexResult(TokenType.FnCall, m.Value, value == 0 ? LexingMode.Normal : LexingMode.FunctionStart);
            }
            else
            {
                Console.WriteLine($"WARNING: [{m.Value}] looks like a function, but not found. Ignoring.");
                return null;
            }
        }

        public static SemanticRegexResult NormalModeMatch(string s, int startat)
        {
            SemanticRegexResult result =
                SemanticRegexResultOrNull(R_CLICK_WAIT, s, startat, LexingMode.Normal)
            ?? SemanticRegexResultOrNull(R_PAGE_WAIT, s, startat, LexingMode.Normal)
            ?? SemanticRegexResultOrNull(R_IGNORE_NEW_LINE, s, startat, LexingMode.Normal)
            ?? SemanticRegexResultOrNull(R_COLON, s, startat, LexingMode.Normal)
            ?? CheckFunctionCall(s, startat)
            ?? SemanticRegexResultOrNull(R_HAT, s, startat, LexingMode.Text)
            ?? SemanticRegexResultOrNull(R_TEXT, s, startat, LexingMode.Normal);

            return result ?? SemanticRegexResult.FailureAndTerminate();
        }

        public static SemanticRegexResult FunctionNotOperator(string s, int startat)
        {

            SemanticRegexResult nonOperatorResult =
                SemanticRegexResultOrNull(R_STRING, s, startat, LexingMode.ArgOperator) ??
                SemanticRegexResultOrNull(R_NUMBER, s, startat, LexingMode.ArgOperator) ??
                SemanticRegexResultOrNull(R_ALIAS, s, startat, LexingMode.ArgOperator) ??
                SemanticRegexResultOrNull(R_STRING_VARIABLE, s, startat, LexingMode.ArgOperator) ??
                SemanticRegexResultOrNull(R_NUMERIC_VARIABLE, s, startat, LexingMode.ArgOperator) ??
                SemanticRegexResultOrNull(R_BRACKET, s, startat, LexingMode.FunctionStart); //after a bracket in not operator mode, must either get another bracket or a non-operator

            return nonOperatorResult ?? SemanticRegexResult.FailureAndTerminate();
        }

        public static SemanticRegexResult OperatorOrComma(string s, int startat)
        {
            SemanticRegexResult operatorResult =
                SemanticRegexResultOrNull(R_OPERATOR, s, startat, LexingMode.FunctionStart) ??
                SemanticRegexResultOrNull(R_COMMA, s, startat, LexingMode.FunctionStart) ??
                SemanticRegexResultOrNull(R_BRACKET, s, startat, LexingMode.ArgOperator);   //after a bracket in op mode, stay in op mode

            return operatorResult ?? SemanticRegexResult.FailureAndChangeState(LexingMode.Normal);   
        }

        public static SemanticRegexResult TextModeMatch(string s, int startat)
        {
            SemanticRegexResult operatorResult = SemanticRegexResultOrNull(R_TEXT, s, startat, LexingMode.Normal);
            return operatorResult ?? SemanticRegexResult.FailureAndTerminate();
        }

        //TODO: convert to array of functions?
        public static SemanticRegexResult DoMatch(string s, int startat, LexingMode currentLexingMode)
        {
            switch(currentLexingMode)
            {
                case LexingMode.Normal:
                    return NormalModeMatch(s, startat);

                case LexingMode.FunctionStart:
                    return FunctionNotOperator(s, startat);

                case LexingMode.ArgOperator:
                    return OperatorOrComma(s, startat);

                case LexingMode.Text:
                    return TextModeMatch(s, startat);

                default:
                    throw new NotImplementedException("Lexing mode not implemented");
            }
        }

        static void ProcessSingleLine(string line)
        {
            line = line.TrimEnd();

            Console.WriteLine($"\nBegin processing line [{line}]");

            LexingMode lexingMode = LexingMode.Normal;
            int startat = 0;

            //even for lexing, we need to know how many arguments exist for each function
            //This is due to the ambiguity because the hat ^ symbol can either start text mode,
            //or act as opening/closing quotes for a string like ^"this is a string"^
            //If a function does not have count listed, just assume it is text mode
            //because this feature is used so infrequently.

            List<Token> tokens = new List<Token>();

            for(int iteration = 0; startat < line.Length && iteration < 1000; iteration++)
            {
                //skip any whitespace at start of line
                Match whitespace_match = WHITESPACE_REGEX.Match(line, startat);
                if(whitespace_match.Success)
                {
                    tokens.Add(new Token(TokenType.WhiteSpace, whitespace_match.Value));

                    //Console.WriteLine($"Skipping whitespace [{whitespace_match.Groups[0]}]");
                    startat += whitespace_match.Length;
                }

                //try all matches

                SemanticRegexResult result = DoMatch(line, startat, lexingMode);
                if(result == null)
                {
                    throw new Exception("Result is null!");
                }

                if(result.modeResult == ModeResult.Success)
                {
                    Console.Write($"Matched {result.token} ");
                    startat += result.token.tokenString.Length;
                    tokens.Add(result.token);


                    lexingMode = result.newLexingMode;
                    Console.Write($"Mode Changed To [{lexingMode}]");
                    Console.WriteLine();
                }
                else if (result.modeResult == ModeResult.FailureAndChangeState)
                {
                    lexingMode = result.newLexingMode;
                }
                else
                {
                    //lexing has failed
                    break;
                }



                if(iteration > 500)
                {
                    Console.WriteLine("Greater than 500 iterations - matching failed!");
                    break;
                }
            }


            if (startat < line.Length)
            {
                Console.Write("WARNING: line did not match to completion!");
            }
            else
            {
                Console.Write($"Successfully Parsed line");
            }

            Console.WriteLine($" Got {tokens.Count} Tokens");
        }

        static void Main(string[] args)
        {
            //allow UTF-8 characters to be shown in console
            Console.OutputEncoding = Encoding.UTF8;

            {
                const string script_name = @"example_input.txt";

                foreach (string line in File.ReadAllLines(script_name))
                {
                    ProcessSingleLine(line);
                    Console.ReadKey();
                }
            }

            Console.WriteLine("Program Finished");
            Console.ReadKey();
        }
    }
}
