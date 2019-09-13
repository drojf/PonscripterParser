using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PonscripterParser
{
    class RegexLexemeTypePair
    {
        public Regex regex;
        public LexemeType type;

        public RegexLexemeTypePair(Regex regex, LexemeType type)
        {
            this.regex = regex;
            this.type = type;
        }
    }

    class LexerTest
    {
        List<Lexeme> lexemes;
        string line;
        int pos;
        SubroutineDatabase subroutineDatabase;

        List<RegexLexemeTypePair> pairs = new List<RegexLexemeTypePair>()
        {
            Pair(Regexes.comment, LexemeType.COMMENT),
            Pair(Regexes.colon, LexemeType.COLON),
            Pair(Regexes.label, LexemeType.LABEL),
            Pair(Regexes.ponscripterFormattingTagRegion, LexemeType.FORMATTING_TAG),
            Pair(Regexes.tilde, LexemeType.JUMPF_TARGET),
            Pair(Regexes.hexColor, LexemeType.HEX_COLOR),
            Pair(Regexes.rSquareBracket, LexemeType.R_SQUARE_BRACKET),
            Pair(Regexes.rRoundBracket, LexemeType.R_ROUND_BRACKET),
            Pair(Regexes.forwardSlash, LexemeType.FORWARD_SLASH),
            Pair(Regexes.backSlash, LexemeType.BACK_SLASH),
            Pair(Regexes.atSymbol, LexemeType.AT_SYMBOL),
            Pair(Regexes.numericLiteral, LexemeType.NUMERIC_LITERAL),
            Pair(Regexes.normalStringLiteral, LexemeType.STRING_LITERAL),
            Pair(Regexes.hatStringLiteral, LexemeType.HAT_STRING_LITERAL),
        };

        List<RegexLexemeTypePair> nextMustBeExpressionList = new List<RegexLexemeTypePair>()
        {
            Pair(Regexes.lSquareBracket, LexemeType.L_SQUARE_BRACKET),
            Pair(Regexes.lRoundBracket, LexemeType.L_ROUND_BRACKET),
            Pair(Regexes.numericReferencePrefix, LexemeType.NUMERIC_REFERENCE),
            Pair(Regexes.stringReferencePrefix, LexemeType.STRING_REFERENCE),
            Pair(Regexes.arrayReferencePrefix, LexemeType.ARRAY_REFERENCE),
        };


        List<RegexLexemeTypePair> operators = new List<RegexLexemeTypePair>()
        {
            Pair(@">=", LexemeType.OPERATOR),
            Pair(@"<=",LexemeType.OPERATOR),
            Pair(@"!=",LexemeType.OPERATOR),
            Pair(@"<>",LexemeType.OPERATOR),
            Pair(@"==",LexemeType.OPERATOR), //equivalent to '='
            Pair(@"&&",LexemeType.OPERATOR), //equivalent to '&'
            Pair(@"\+",LexemeType.OPERATOR),
            Pair(@"-",LexemeType.OPERATOR),
            Pair(@"\*",LexemeType.OPERATOR),
            Pair(@"/",LexemeType.OPERATOR),
            Pair(@">",LexemeType.OPERATOR),
            Pair(@"<",LexemeType.OPERATOR),
            Pair(@"=",LexemeType.OPERATOR),
            Pair(@"&",LexemeType.OPERATOR),
        };

        public LexerTest(string line, SubroutineDatabase subroutineDatabase)
        {
            this.line = line;
            this.subroutineDatabase = subroutineDatabase;
            this.lexemes = new List<Lexeme>();

            Console.WriteLine($"Full Line [{line}]");
        }

        static private RegexLexemeTypePair Pair(Regex regex, LexemeType type)
        {
            return new RegexLexemeTypePair(regex, type);
        }

        static private RegexLexemeTypePair Pair(String regexPattern, LexemeType type)
        {
            return new RegexLexemeTypePair(Regexes.RegexFromStart(regexPattern, RegexOptions.IgnoreCase), type);
        }

        public bool TryEach(List<RegexLexemeTypePair> matchers)
        {
            foreach (RegexLexemeTypePair matcher in matchers)
            {
                if(TryPopRegex(matcher.regex, matcher.type))
                {
                    return true;
                }
            }

            return false;
        }

        public void LexSection(bool sectionAllowsText)
        {
            bool mustBeExpression = false;
            bool firstIteration = true;
            while (HasNext())
            {
                bool nextMustBeExpression = false;
                TryPopRegex(Regexes.whitespace, LexemeType.WHITESPACE);
                if(!HasNext())
                {
                    break;
                }

                if (!mustBeExpression && sectionAllowsText && (Peek() == '^' || Peek() == '!' || Peek() > 255))
                {

                    //only allow entering text mode if:
                    // - not followed by a comma, as that would imply the next lexeme is a function argument which cannot be text mode
                    //   EXAMPLE: dwave_eng 0, ev2_3e816 ^  Chiester Sisters!!^@:

                    // - not followed by an operator, as that would imply the next lexeme is a expression
                    //   Example: dwave_eng 0, ev2_3e816 + ^  Chiester Sisters!!^@:

                    // - not followed by a function name which takes arguments, as that would imply it's meant to be
                    // Example where it is allowed: langen ^She jumped and jumped and leapt and even flipped in midair, increasing that distance.^\
                    // Example where it NOT allowed: takesOneArgument ^She jumped and jumped and leapt and even flipped in midair, increasing that distance.^

                    PopDialogue();
                }
                else if((nextMustBeExpression || firstIteration) && TryPopRegex(Regexes.label, LexemeType.LABEL))
                {
                    //label must either be:
                    // - the first lexeme on the line, or 
                    // - an expression (function argument)
                }
                else if (TryEach(nextMustBeExpressionList))
                {
                    nextMustBeExpression = true;
                }
                else if (TryEach(operators))
                {
                    nextMustBeExpression = true;
                }
                else if(TryPopRegex(Regexes.comma, LexemeType.COMMA))
                {
                    nextMustBeExpression = true;
                }
                else if (TryEach(pairs))
                {

                }
                else if(TryPopRegex(Regexes.word, LexemeType.WORD, out Lexeme lexeme))
                {
                    if (!mustBeExpression)
                    {
                        string word = lexeme.text;
                        if(word == "to" || word == "step")
                        {
                            //In For loops, after "to" or "step" an expression is expected.
                            nextMustBeExpression = true;
                        }
                        else if (word == "if" || word == "notif" || word == "for")
                        {
                            //do nothing?
                        }
                        else if (subroutineDatabase.TryGetValue(word, out SubroutineInformation subroutineInformation))
                        {
                            if (subroutineInformation.hasArguments)
                            {
                                nextMustBeExpression = true;
                            }
                        }
                        else if(word.StartsWith("_") && subroutineDatabase.TryGetValue(word.Substring(1), out SubroutineInformation underscoreSubroutineInformation))
                        {
                            Console.WriteLine($"WARNING: it appears the function {word} is called with an underscore, even though it is not overriden");
                            if (underscoreSubroutineInformation.hasArguments)
                            {
                                nextMustBeExpression = true;
                            }
                        }
                        else
                        {
                            throw GetLexingException($"Unrecognized keyword or function \"{word}\"");
                        }
                    }
                }
                else if (sectionAllowsText && (Peek() == '`'))
                {
                    //For '`': Pretend single-byte text mode is just normal text mode for now...
                    PopDialogue();
                }
                else if (sectionAllowsText && TryPopRegex(Regexes.hexColor, LexemeType.HEX_COLOR))
                {
                    //For '#': If you encounter a color tag at top level, most likely it's for colored text.
                    //Should this enter text mode?
                    break;
                }
                else if (sectionAllowsText && (Peek() == '\''))
                {
                    PrintLexingWarning($"WARNING: Text-mode possibly entered unintentionally from character '{Peek()}' (ascii: {(int)Peek()})!");
                    PopDialogue();
                }
                else if (Peek() <= 8)
                {
                    PrintLexingWarning($"WARNING: Got control character '{Peek()}' (ascii: {(int)Peek()}), which will be ignored!");
                    lexemes.Add(new Lexeme(LexemeType.UNHANDLED_CONTROL_CHAR, Pop().ToString()));
                }
                else
                {
                    //error
                    throw GetLexingException("Unexpected character at top level");
                }

                mustBeExpression = nextMustBeExpression;
                firstIteration = false;
            }
        }

        private void PopDialogue()
        {
            int initial_position = this.pos;
            while (HasNext())
            {
                char next = Peek();

                if (next == '@' || Regexes.TextLineEnd.IsMatch(this.line, this.pos))
                {
                    break;
                }
                else
                {
                    Pop();
                }
            }

            this.lexemes.Add(new Lexeme(LexemeType.DIALOGUE, this.line.Substring(initial_position, this.pos - initial_position)));
        }

        private bool TryPopRegex(Regex r, LexemeType type, out Lexeme lexeme)
        {
            if (TryMatch(r, out Match match))
            {
                this.pos += match.Length;
                lexeme = new Lexeme(type, match.Value);
                lexemes.Add(lexeme);
                return true;
            }
            else
            {
                lexeme = null;
                return false;
            }
        }

        private bool TryPopRegex(Regex r, LexemeType type)
        {
            return TryPopRegex(r, type, out Lexeme _);
        }

        private bool TryMatch(Regex r, out Match m)
        {
            m = r.Match(this.line, this.pos);
            return m.Success;
        }

        private bool HasNext()
        {
            return this.pos < this.line.Length;
        }

        private Exception GetLexingException(string message)
        {
            return new Exception(PrintLexingWarning(message));
        }
        private string PrintLexingWarning(string message)
        {
            string fullMessage = $"{message} From:{this.line.Substring(this.pos)}";
            Console.WriteLine(fullMessage);

            Console.WriteLine("Lexemes lexed so far:");
            foreach(Lexeme lexeme in this.lexemes)
            {
                Console.WriteLine($"{lexeme.type}: {lexeme.text}");
            }

            return fullMessage;
        }

        private char Pop()
        {
            char retval = Peek();
            this.pos++;
            return retval;
        }

        private char Peek()
        {
            if (!HasNext())
            {
                throw new Exception("CharReader ran out of characters - missing EOL check?");
            }

            return line[this.pos];
        }

    }


    /*    class ParsingState
        {
            List<Lexeme> lexemes;
            string line;
            int pos;
            SubroutineDatabase subroutineDatabase;

            public bool TryMatch(Regex r, out Match m)
            {
                m = r.Match(this.line, this.pos);
                return m.Success;
            }

            public LexemeResult TryEach(List<LexemeMatcher> matchers)
            {
                foreach(LexemeMatcher matcher in matchers)
                {
                    LexemeResult result = matcher.TryPopLexeme(this);
                    if(result.IsMatch)
                    {
                        return result;
                    }
                }

                return LexemeResult.None;
            }
        }

        //TODO: need to handle structured entries like for loops
        class LexemeResult
        {
            public bool IsMatch;
            public string text;
            public LexemeType type;

            public LexemeResult(bool isMatch, string text, LexemeType type)
            {
                this.IsMatch = isMatch;
                this.text = text;
                this.type = type;
            }

            public LexemeResult(string text, LexemeType type)
            {
                this.IsMatch = true;
                this.text = text;
                this.type = type;
            }

            public static LexemeResult None = new LexemeResult(false, null, LexemeType.WHITESPACE);
        }

        interface LexemeMatcher
        {
            LexemeResult TryPopLexeme(ParsingState state);
        }

        class GenericRegexMatcher : LexemeMatcher
        {
            Regex r;
            LexemeType type;

            public GenericRegexMatcher(Regex r, LexemeType type)
            {
                this.r = r;
                this.type = type;
            }

            public LexemeResult TryPopLexeme(ParsingState state)
            {
                if(state.TryMatch(r, out Match m))
                {
                    return new LexemeResult(m.Value, type);
                }
                else
                {
                    return LexemeResult.None;
                }
            }
        }

        class Matchers
        {
            static GenericRegexMatcher NumericLiteral = new GenericRegexMatcher(Regexes.numericLiteral, LexemeType.NUMERIC_LITERAL);
            static GenericRegexMatcher NumAlias = new GenericRegexMatcher(Regexes.word, LexemeType.NUM_ALIAS);
        }

        class NumericReference : LexemeMatcher
        {

        }


        class CharReaderTest
        {
        }*/
}
