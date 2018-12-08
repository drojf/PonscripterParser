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

        public static Dictionary<LexingMode, List<SemanticRegex>> lexingmodeToMatches = new Dictionary<LexingMode, List<SemanticRegex>>
        {
            {   LexingMode.Normal, new List<SemanticRegex>()
                {
                    new RClickWait(),
                    new RFunction(),
                }
            },

            {   LexingMode.Function, new List<SemanticRegex>()
                {
                    
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

        public abstract class SemanticRegex
        {
            protected Regex pattern;

            public SemanticRegex(string regexAsString)
            {
                this.pattern = new Regex(regexAsString);
            }

            public abstract Match DoMatch(string s, int startat);

            public abstract LexingMode UpdateLexingMode(LexingMode currentMode);
        }


        public class RClickWait : SemanticRegex
        {
            public RClickWait() : base(@"\G@")
            {
            }

            public override Match DoMatch(string s, int startat)
            {
                return pattern.Match(s, startat);
            }

            public override LexingMode UpdateLexingMode(LexingMode currentMode)
            {
                return LexingMode.Normal;
            }
        }


        public class RFunction : SemanticRegex
        {
            public RFunction() : base(@"\G[!a-zA-Z_]+")
            {
            }

            public override Match DoMatch(string s, int startat)
            {
                return pattern.Match(s, startat);
            }

            public override LexingMode UpdateLexingMode(LexingMode currentMode)
            {
                //TODO: - if function is known function -> function
                //otherwise -> text, and emit error
                return LexingMode.Function;
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
            Console.WriteLine($"Begin processing line {line}");

            LexingMode lexingMode = LexingMode.Normal;
            int startat = 0;

            for(int iteration = 0; line.Length > 0 && iteration < 1000; iteration++)
            {
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
                    Match m = pattern.DoMatch(line, startat);
                    if (m.Success)
                    {
                        Console.Write($"Matched {m.Groups[0]} ");
                        startat += m.Length;

                        lexingMode = pattern.UpdateLexingMode(lexingMode);
                        Console.Write($"Lexing Mode: {lexingMode}");
                        Console.WriteLine();
                        break;
                    }
                }
            }


            if (startat != line.Length - 1)
            {
                Console.WriteLine("WARNING: line did not match to completion!");
            }
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
