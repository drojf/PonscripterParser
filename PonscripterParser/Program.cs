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
            AtSymbol,
            FnCall,
            Colon,
            Literal,
            Operator,
            Comma,
        }

        public static HashSet<string> functionNames = new HashSet<string>() {
            "lsp",
            "dwave",
        };

        public static Dictionary<LexingMode, List<SemanticRegex>> lexingmodeToMatches = new Dictionary<LexingMode, List<SemanticRegex>>
        {
            {   LexingMode.Normal, new List<SemanticRegex>()
                {
                    new RClickWait(),
                    new RFunctionCall(),
                    new RColon(), //ignore repeated colons
                }
            },

            {   LexingMode.Function, new List<SemanticRegex>()
                {
                    new RString(),
                    new RComma(),
                    new RNumber(),
                    new ROperator(),
                    new RBracket(),
                    new RColon(),   //-> normal mode
                }
            },

            {   LexingMode.Text, new List<SemanticRegex>()
                {

                }
            },

            {   LexingMode.String, new List<SemanticRegex>()
                {

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

        public class SemanticRegexSimple : SemanticRegex
        {
            readonly TokenType tokenType;

            public SemanticRegexSimple(string regexPattern, TokenType tokenType) : base(regexPattern)
            {
                this.tokenType = tokenType;
            }

            public override SemanticRegexResult DoMatch(string s, int startat, LexingMode currentLexingMode)
            {
                Match m = pattern.Match(s, startat);
                
                return m.Success ? new SemanticRegexResult(tokenType, m.Value, currentLexingMode) : null;
            }
        }

        public class RClickWait : SemanticRegexSimple
        {
            public RClickWait() : base(@"\G@", TokenType.AtSymbol) { }
        }

        public class RString : SemanticRegexSimple
        {
            //this is just \G"[^"]+"
            public RString() : base(@"\G""[^""]+""", TokenType.Literal) { }
        }

        public class RNumber : SemanticRegexSimple
        {
            public RNumber() : base(@"\G\d+", TokenType.Literal) { }
        }
        
        public class ROperator : SemanticRegexSimple
        {
            public ROperator() : base(@"\G[\+\-\*\/]", TokenType.Operator) { }
        }

        public class RBracket : SemanticRegexSimple
        {
            public RBracket() : base(@"\G[\(\)]", TokenType.Colon) { }
        }

        public class RComma : SemanticRegexSimple
        {
            public RComma() : base(@"\G,", TokenType.Comma) { }
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

        public class RFunctionCall : SemanticRegex
        {
            public RFunctionCall() : base(@"\G[!a-zA-Z_]+")
            {
            }

            //for now, the caller should always decide whether to change lexing mode, not in the calee
            public override SemanticRegexResult DoMatch(string s, int startat, LexingMode currentLexingMode)
            {
                Match m = pattern.Match(s, startat);
                return m.Success ? new SemanticRegexResult(TokenType.FnCall, m.Value, LexingMode.Function) : null;
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
            String,
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
