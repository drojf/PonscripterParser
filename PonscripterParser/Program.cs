using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PonscripterParser
{
    class Program
    {
        //must use \G to use in conjunction with 'startat' parameter of Match - apparently ^ means start of string ONLY, not startof 'startat'
        //public static readonly Regex clickWait = new Regex(@"\G@", RegexOptions.IgnoreCase);
        public static readonly Regex WHITESPACE_REGEX = new Regex(@"\G\s+", RegexOptions.IgnoreCase);

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

        public enum TokenType
        {
            ClickWait,
            PageWait,
            IgnoreNewLine,
            Text,
            FnCall,
            Colon,
            Literal,
            Operator,
            Comma,
            Alias, //numAlias, stringalias etc
        }

        public static HashSet<string> functionNames = new HashSet<string>() {
            "lsp",
            "dwave",
            "langen",
            "langjp",

            //later these functions should be populated dynamically by scanning function defs!
            "dwave_eng"
        };

        public static Dictionary<LexingMode, List<SemanticRegex>> lexingmodeToMatches = new Dictionary<LexingMode, List<SemanticRegex>>
        {
            {   LexingMode.Normal, new List<SemanticRegex>()
                {
                    new RClickWait(),
                    new RFunctionCall(), //-> Function Mode
                    new RColon(), //ignore repeated colons
                    new RHat(), //-> Text Mode
                    new RClickWait(),
                    new RPageWait(),
                    new RIgnoreNewLine(),
                    new RText(),
                }
            },

            {   LexingMode.Function, new List<SemanticRegex>()
                {
                    new RString(),
                    new RComma(),
                    new RNumber(),
                    new ROperator(),
                    new RBracket(),
                    new RAlias(),
                    new RColon(),   //-> normal mode
                    new RHat(),
                }
            },

            {   LexingMode.Text, new List<SemanticRegex>()
                {
                    new RText(),
                }
            },
        };

        public class Token
        {
            public TokenType tokenType;
            public string tokenString;

            public Token(TokenType tokenType, string tokenValue)
            {
                this.tokenType = tokenType;
                this.tokenString = tokenValue;
            }

            public override string ToString()
            {
                return $"{tokenType, 8}: [{tokenString}]";
            }
        }

        public abstract class SemanticRegex
        {
            protected Regex pattern;

            public SemanticRegex(string regexAsString)
            {
                this.pattern = new Regex(regexAsString);
            }

            public abstract SemanticRegexResult DoMatch(string s, int startat, LexingMode currentLexingMode);
        }

        //base class for patterns which don't change the current mode
        public class SemanticRegexSameMode : SemanticRegex
        {
            readonly TokenType tokenType;

            public SemanticRegexSameMode(string regexPattern, TokenType tokenType) : base(regexPattern)
            {
                this.tokenType = tokenType;
            }

            public override SemanticRegexResult DoMatch(string s, int startat, LexingMode currentLexingMode)
            {
                Match m = pattern.Match(s, startat);
                
                return m.Success ? new SemanticRegexResult(tokenType, m.Value, currentLexingMode) : null;
            }
        }

        public class SemanticRegexChangeMode : SemanticRegex
        {
            readonly TokenType tokenType;
            readonly LexingMode newMode;
            public SemanticRegexChangeMode(string regexPattern, TokenType tokenType, LexingMode newMode) : base(regexPattern)
            {
                this.tokenType = tokenType;
                this.newMode = newMode;
            }

            public override SemanticRegexResult DoMatch(string s, int startat, LexingMode currentLexingMode)
            {
                Match m = pattern.Match(s, startat);

                return m.Success ? new SemanticRegexResult(tokenType, m.Value, newMode) : null;
            }
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
            public RAlias() : base(@"\G[a-zA-Z_]+[0-9a-zA-Z_]*", TokenType.Alias) { }
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
            public RFunctionCall() : base(@"\G[!a-zA-Z_]+[0-9!a-zA-Z_]*")
            {
            }

            //for now, the caller should always decide whether to change lexing mode, not in the calee
            public override SemanticRegexResult DoMatch(string s, int startat, LexingMode currentLexingMode)
            {
                Match m = pattern.Match(s, startat);

                //For now, just treat anything that looks like a function as a function. Should revert this later
                if(m.Success)
                {
                    if(functionNames.Contains(m.Value))
                    {
                        return new SemanticRegexResult(TokenType.FnCall, m.Value, LexingMode.Function);
                    }
                    else
                    {
                        Console.WriteLine($"WARNING: [{m.Value}] looks like a function, but not found. Ignoring.");
                    }
                }

                return null;
            }
        }

        public class SemanticRegexResult
        {
            public LexingMode newLexingMode;
            public Token token;

            public SemanticRegexResult(TokenType token, string tokenString, LexingMode newLexingMode)//Match m, LexingMode l, bool sucess)
            {
                this.token = new Token(token, tokenString);
                this.newLexingMode = newLexingMode;
            }
        }

        public enum LexingMode
        {
            Normal,
            Text,
            Function,
        }


        static void ProcessSingleLine(string line)
        {
            line = line.TrimEnd();

            Console.WriteLine($"\nBegin processing line [{line}]");

            LexingMode lexingMode = LexingMode.Normal;
            int startat = 0;

            List<Token> tokens = new List<Token>();

            for(int iteration = 0; startat < line.Length && iteration < 1000; iteration++)
            {
                bool debug_substitution_made = false;

                //skip any whitespace at start of line
                Match whitespace_match = WHITESPACE_REGEX.Match(line, startat);
                if(whitespace_match.Success)
                {
                    //Console.WriteLine($"Skipping whitespace [{whitespace_match.Groups[0]}]");
                    startat += whitespace_match.Length;
                }

                //try all matches
                foreach (SemanticRegex pattern in lexingmodeToMatches[lexingMode])
                {
                    SemanticRegexResult result = pattern.DoMatch(line, startat, lexingMode);
                    if (result != null)
                    {
                        debug_substitution_made = true;
                        Console.Write($"Matched {result.token} ");
                        startat += result.token.tokenString.Length;
                        tokens.Add(result.token);

                        lexingMode = result.newLexingMode;
                        Console.Write($"Mode Changed To [{lexingMode}]");
                        Console.WriteLine();
                        break;
                    }
                }

                if(!debug_substitution_made)
                {
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
                }
            }

            Console.WriteLine("Program Finished");
            Console.ReadKey();
        }
    }
}
