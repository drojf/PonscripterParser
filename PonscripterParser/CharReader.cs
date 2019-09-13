//Please see UncleMion's POnscripter Documentation here: https://www.drojf.com/nscripter/NScripter_API_Reference.html (mirror of website as original was down)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PonscripterParser
{
    class Regexes {
        public static Regex hexColor = RegexFromStart(@"#[0-9abcdef]{6}", RegexOptions.IgnoreCase);
        public static Regex word = RegexFromStart(@"[a-zA-Z_][a-zA-Z0-9_]*", RegexOptions.IgnoreCase);
        public static Regex numericLiteral = RegexFromStart(@"-?\d+", RegexOptions.IgnoreCase);

        //For loop regexes
        public static Regex ForTo = RegexFromStart(@"to", RegexOptions.IgnoreCase);
        public static Regex ForEquals = RegexFromStart(@"=", RegexOptions.IgnoreCase);
        public static Regex ForStep = RegexFromStart(@"step", RegexOptions.IgnoreCase);

        //Text line end check
        public static Regex TextLineEnd = RegexFromStart(@"(/|\\)\s*(;.*)?$", RegexOptions.IgnoreCase);

        //Special case for partial divide expression/page wait ambiguity like "langen:voicedelay 1240/"
        //TODO: decide if this is a bug or not
        public static Regex DividePageWaitAmbiguity = RegexFromStart(@"/\s*(;.*)?$", RegexOptions.IgnoreCase);

        //Jumpf target ~ regex
        public static Regex JumpfTarget = RegexFromStart(@"\s*~\s*$", RegexOptions.IgnoreCase);

        //Ponscripter Formatting tag region
        public static Regex ponscripterFormattingTagRegion = RegexFromStart(@"~[^~]*~", RegexOptions.IgnoreCase);

        public static Regex whitespace = RegexFromStart(@"\s+", RegexOptions.IgnoreCase);

        public static Regex comment = RegexFromStart(@";.*", RegexOptions.IgnoreCase);

        public static Regex colon = RegexFromStart(@":", RegexOptions.IgnoreCase);
        public static Regex lSquareBracket = RegexFromStart(@"\[", RegexOptions.IgnoreCase);
        public static Regex rSquareBracket = RegexFromStart(@"\]", RegexOptions.IgnoreCase);
        public static Regex lRoundBracket = RegexFromStart(@"\(", RegexOptions.IgnoreCase);
        public static Regex rRoundBracket = RegexFromStart(@"\)", RegexOptions.IgnoreCase);
        public static Regex backSlash = RegexFromStart(@"\\", RegexOptions.IgnoreCase);
        public static Regex forwardSlash = RegexFromStart(@"/", RegexOptions.IgnoreCase);
        public static Regex atSymbol = RegexFromStart(@"@", RegexOptions.IgnoreCase);

        public static Regex label = RegexFromStart(@"\*\w+", RegexOptions.IgnoreCase);

        public static Regex tilde = RegexFromStart(@"~", RegexOptions.IgnoreCase);

        public static Regex numericReferencePrefix = RegexFromStart(@"\%", RegexOptions.IgnoreCase);
        public static Regex stringReferencePrefix = RegexFromStart(@"\$", RegexOptions.IgnoreCase);
        public static Regex arrayReferencePrefix = RegexFromStart(@"\?", RegexOptions.IgnoreCase);

        public static Regex normalStringLiteral = RegexFromStart("\"[^\"]*\"", RegexOptions.IgnoreCase);
        public static Regex hatStringLiteral = RegexFromStart(@"\^[^\^]\^", RegexOptions.IgnoreCase);

        public static Regex comma = RegexFromStart(@",", RegexOptions.IgnoreCase);


        //TODO: support fchk command (`if fchk "file\path.bmp"`)
        //NOTE: order here matters - must match from longest to shortest to prevent '=' matching '=='
        public static List<Regex> operatorRegexList = new List<String>() {
                @">=",
                @"<=",
                @"==",
                @"!=",
                @"<>",
                @"&&",
                @"\+",
                @"-",
                @"\*",
                @"/",
                @">",
                @"<",
                @"=",
                //"\|\|", not sure if "\|\|" is supported
        }.Select(op => RegexFromStart(op, RegexOptions.IgnoreCase)).ToList();

        public static Regex RegexFromStart(string s, RegexOptions options)
        {
            return new Regex(@"\G" + s, options);
        }
    }

    class ParserState
    {
        List<Lexeme> lexemes;
        string line;
        int pos;
        SubroutineDatabase subroutineDatabase;

        public ParserState(List<Lexeme> lexemes, string line, int pos, SubroutineDatabase subroutineDatabase)
        {
            this.lexemes = lexemes;
            this.line = line;
            this.pos = pos;
            this.subroutineDatabase = subroutineDatabase;
        }
    }


    /*class CharReader2
    {
        List<Lexeme> lexemes;
        string line;
        int pos;
        SubroutineDatabase subroutineDatabase;

        public CharReader2(string line, SubroutineDatabase subroutineDatabase)
        {
            this.line = line;
            this.subroutineDatabase = subroutineDatabase;
            this.lexemes = new List<Lexeme>();

            Console.WriteLine($"Full Line [{line}]");
        }

        public SaveState()
        {

        }

        public void ParseSection(bool sectionAllowsText)
        {
            List<Lexeme> retLexemes = new List<Lexeme>();
            while (HasNext())
            {
                TryPopRegex(Regexes.whitespace, LexemeType.WHITESPACE);

                if (TryPopRegex(Regexes.comment, LexemeType.COMMENT))
                {
                    continue;
                }
                else if (TryPopRegex(Regexes.colon, LexemeType.COLON))
                {
                    continue;
                }
                else if (TryPopRegex(Regexes.label, LexemeType.LABEL))
                {
                    continue;
                }
                else if (TryPopRegex(Regexes.ponscripterFormattingTagRegion, LexemeType.FORMATTING_TAG))
                {
                    continue;
                }
                else if (TryPopRegex(Regexes.tilde, LexemeType.JUMPF_TARGET))
                {
                    continue;
                }
                else if (TryMatch(Regexes.word, out Match m))
                {
                    ParseWord();
                    continue;
                }
                else if (sectionAllowsText && (next == '^' || next == '!' || next > 255))
                {
                    retLexemes.Add(PopDialogue());
                }
                else if (sectionAllowsText && (next == '`'))
                {
                    //For '`': Pretend single-byte text mode is just normal text mode for now...
                    retLexemes.Add(PopDialogue());
                }
                else if (sectionAllowsText && TryPopRegex(Regexes.hexColor, LexemeType.HEX_COLOR))
                {
                    //For '#': If you encounter a color tag at top level, most likely it's for colored text.
                    //Should this enter text mode?
                    break;
                }
                //else if (sectionAllowsText && next == '$')
                //{
                //    //This is actually when a string is printed like: 
                //    //mov $Free1,"Chasan、Arel、Phorlakh、そしてTaliahad。"
                //    //langjp: dwave_jp 0, mar_1e562_1:$Free1@
                //    retLexemes.Add(PopStringVariable());
                //}
                //else if(sectionAllowsText && next == '%')
                //{
                //This is actually when a number is printed like: 
                //mov $Free1,"Chasan、Arel、Phorlakh、そしてTaliahad。"
                //langjp: dwave_jp 0, mar_1e562_1:$Free1@
                //retLexemes.Add(PopStringLiteral());
                //}
                else if (sectionAllowsText && (next == '@' || next == '\\' || next == '/'))
                {
                    retLexemes.Add(PopSymbol());
                }
                else if (sectionAllowsText && (next == '\''))
                {
                    PrintLexingWarning($"WARNING: Text-mode possibly entered unintentionally from character '{next}' (ascii: {(int)next})!");
                    retLexemes.Add(PopDialogue());
                }
                else if (Peek() <= 8)
                {
                    PrintLexingWarning($"WARNING: Got control character '{next}' (ascii: {(int)next}), which will be ignored!");
                    lexemes.Add(new Lexeme(LexemeType.UNHANDLED_CONTROL_CHAR, Pop().ToString()));
                }
                else
                {
                    //error
                    ThrowLexingException("Unexpected character at top level");
                }
            }

            return retLexemes;
        }

        private void ParseWord()
        {
            //parse the function or keyword name
            if(!TryMatch(Regexes.word, out Match match))
            {
                throw GetLexingException("ParseWord was called without a valid word infront");
            }

            string word = match.Value;
            Console.WriteLine($"Got word {word}");

            //check for keywords
            if (word == "if")
            {
                lexemes.Add(new Lexeme(LexemeType.IF, word));
                ParseIfCondition();
                return;
            }
            else if (word == "notif")
            {
                lexemes.Add(new Lexeme(LexemeType.NOT_IF, word));
                ParseIfCondition();
                return;
            }
            else if (word == "for")
            {
                lexemes.Add(new Lexeme(LexemeType.FOR, word));
                ParseForBody();
                return;
            }
            else
            {
                //currently don't differentiate between user defined function and predefined functions
                lexemes.Add(new Lexeme(LexemeType.FUNCTION, word));
                ParseFunctionArguments(word);
                return;
            }

            throw GetLexingException("Failed to parse word");
        }

        public List<LexemeOld> ParseForBody()
        {
            LexemeOld matchNumericValue()
            {
                if (Peek() == '%')
                {
                    return PopNumericVariable();
                }
                else if (charIsWord(Peek()))
                {
                    //most likely a numalias
                    return PopToken();
                }
                else
                {
                    return PopNumericLiteral();
                }
            }

            List<LexemeOld> lexemes = new List<LexemeOld>();
            
            //numeric variable to be assigned to
            lexemes.Add(PopNumericVariable());

            //literal equals sign (in this case, it means assignment)
            lexemes.Add(PopRegexOrError(ForEqualsRegex, "Missing '=' in for loop"));

            //numeric literal or variable
            matchNumericValue();

            //literal 'to'
            lexemes.Add(PopRegexOrError(ForToRegex, "Missing 'to' in for loop"));

            //numeric literal or variable
            matchNumericValue();

            if (HasNext() && Peek() == 's')
            {
                //literal 'step'
                lexemes.Add(PopRegexOrError(ForStepRegex, "Missing 'to' in for loop"));

                //numeric literal or variable
                matchNumericValue();
            }

            return lexemes;
        }

        private void ParseIfCondition()
        {
            ParseExpression();
        }

        private void ParseExpression()
        {
            bool TryPopOperator()
            {
                foreach (Regex r in Regexes.operatorRegexList)
                {
                    if (TryPopRegex(r, LexemeType.OPERATOR))
                    {
                        return true;
                    }
                }

                return false;
            }

            // Pop subsequent operator expression sequences (no support parenthesis '(' or ')' yet)
            while (true)
            {
                //Due to ambiguity with the pagewait operator ('/') and the divide operator ('/'),
                //Assume that the expression has ended if all that is left on the line is a '/'
                if (TryMatch(Regexes.DividePageWaitAmbiguity, out Match _))
                {
                    break;
                }

                //Try to pop an operator. If no operator was popped, assume expression is finished
                if (!TryPopOperator())
                {
                    break;
                }

                //Each operator must be followed by some Expression Data
                ParseExpressionData();
            }
        }

        private void ParseExpressionData()
        {
            if (TryPopRegex(Regexes.normalStringLiteral, LexemeType.STRING_LITERAL))
            {
                //Normal string literal - "Hello World"
            }
            else if (TryPopRegex(Regexes.hatStringLiteral, LexemeType.HAT_STRING_LITERAL))
            {
                //Hat string literal - ^Hello World^
            }
            else if (TryMatch(Regexes.numericReferencePrefix, out Match _)) //next == '%')
            {
                //Numeric Reference - %
                ParseNumericReference();
            }
            else if (TryMatch(Regexes.stringReferencePrefix, out Match _))//next == '$')
            {
                //String Reference - $
                ParseStringReference();
            }
            else if (TryPopRegex(Regexes.hexColor, LexemeType.HEX_COLOR))
            {
                //hex color - terminal
            }
            else if (TryMatch(Regexes.arrayReferencePrefix, out Match _))
            {
                //array ?[$hello][5]
                ParseArray();
            }
            else if (TryPopRegex(Regexes.label, LexemeType.LABEL))
            {
                //label (*go_here)
            }
            else if (TryPopRegex(Regexes.numericLiteral, LexemeType.NUMERIC_LITERAL))
            {
                //Numeric Literal (-123124)
                return;
            }
            else if (TryPopRegex(Regexes.word, LexemeType.ALIAS))
            {
                //numalias or stralias
                return;
            }
            else
            {
                throw GetLexingException("Failed to pop expression data");
            }
        }

        private void ParseStringReference()
        {
            PopRegexOrError(Regexes.stringReferencePrefix, LexemeType.STRING_REFERENCE, "String reference was missing $");

            if (TryMatch(Regexes.numericReferencePrefix, out Match _))
            {
                //Numeric Reference
                ParseNumericReference();
            }
            else if(TryMatch(Regexes.arrayReferencePrefix, out Match _))
            {
                //Array Reference
                ParseArray();
            }
            else if (TryPopRegex(Regexes.numericLiteral, LexemeType.NUMERIC_LITERAL))
            {
                //Numeric Literal (-123124)
                return;
            }
            else if (TryPopRegex(Regexes.word, LexemeType.NUM_ALIAS))
            {
                //numalias (asdf)
                return;
            }
            else
            {
                throw GetLexingException("Failed to parse string reference");
            }
        }

        private void ParseNumericReference()
        {
            PopRegexOrError(Regexes.numericReferencePrefix, LexemeType.NUMERIC_REFERENCE, "Numeric reference was missing %");

            if(TryMatch(Regexes.numericReferencePrefix, out Match _))
            {
                //Numeric Reference
                ParseNumericReference();
            }
            else if(TryMatch(Regexes.arrayReferencePrefix, out Match _))
            {
                //Array Reference
                ParseArray();
            }
            else if (TryPopRegex(Regexes.numericLiteral, LexemeType.NUMERIC_LITERAL))
            {
                //Numeric Literal (-123124)
                return;
            }
            else if (TryPopRegex(Regexes.word, LexemeType.NUM_ALIAS))
            {
                //numalias (asdf)
                return;
            }
            else
            {
                throw GetLexingException("Failed to parse numeric reference");
            }
        }

        private void ParseArray()
        {
            //pop question mark

            //while(next is lbracket)
            //{

            //pop lbracket

            //ParseExpressionData()

            //pop rbracket
            //}
        }

        // Some lexeme which directly can be interpreted as a number, with no dereferencing
        //private void ParseNumericValue()
        //{
        //    if(TryPopRegex(Regexes.numericLiteral, LexemeType.NUMERIC_LITERAL))
        //    {
        //        //Numeric Literal (-123124)
        //        return;
        //    }
        //    else if(TryPopRegex(Regexes.word, LexemeType.NUM_ALIAS))
        //    {
        //        //numalias (asdf)
        //        return;
        //    }

        //    throw GetLexingException("Failed to parse numeric value");
        //}

        private void PopRegexOrError(Regex r, LexemeType lexemeType, string errormsg)
        {
            if (TryPopRegex(r, lexemeType))
            {
                return;
            }
            else
            {
                throw new Exception(errormsg);
            }
        }

        private bool TryPopRegex(Regex r, LexemeType type)
        {
            if (TryMatch(r, out Match match))
            {
                this.pos += match.Length;
                lexemes.Add(new Lexeme(type, match.Value));
                return true;
            }
            else
            {
                return false;
            }
        }

        //Match a regex against the current cursor position, without advancing the cursor.
        private bool TryMatch(Regex r, out Match m)
        {
            m = r.Match(this.line, this.pos);
            return m.Success;
        }

        private char Peek()
        {
            if (!HasNext())
            {
                throw new Exception("CharReader ran out of characters - missing EOL check?");
            }

            return line[this.pos];
        }

        private char Pop()
        {
            char retval = Peek();
            this.pos++;
            return retval;
        }

        private bool HasNext()
        {
            return this.pos < this.line.Length;
        }

        private Exception GetLexingException(string message)
        {
            throw new Exception(PrintLexingWarning(message));
        }

        private string PrintLexingWarning(string message)
        {
            string fullMessage = $"{message} From:{this.line.Substring(this.pos)}";
            Console.WriteLine(fullMessage);
            return fullMessage;
        }
    }*/

    class CharReader
    {   //is \G required at start of regexes?
        Regex hexColorRegex = RegexFromStart(@"#[0-9abcdef]{6}", RegexOptions.IgnoreCase);
        Regex numericVariableRegex = RegexFromStart(@"%\w+", RegexOptions.IgnoreCase);
        Regex stringVariableRegex = RegexFromStart(@"\$\w+", RegexOptions.IgnoreCase);
        Regex arrayRegex = RegexFromStart(@"\?\w+(\[%?\w+\])+", RegexOptions.IgnoreCase); //TODO: arrays are not lexed properly, they just use this regex!
        Regex labelRegex = RegexFromStart(@"\*\w+", RegexOptions.IgnoreCase);
        Regex numericLiteralRegex = RegexFromStart(@"-?\d+", RegexOptions.IgnoreCase);

        //For loop regexes
        Regex ForToRegex = RegexFromStart(@"to", RegexOptions.IgnoreCase);
        Regex ForEqualsRegex = RegexFromStart(@"=", RegexOptions.IgnoreCase);
        Regex ForStepRegex = RegexFromStart(@"step", RegexOptions.IgnoreCase);

        //Text line end check
        Regex TextLineEndRegex = RegexFromStart(@"(/|\\)\s*(;.*)?$", RegexOptions.IgnoreCase);

        //Special case for partial divide expression/page wait ambiguity like "langen:voicedelay 1240/"
        //TODO: decide if this is a bug or not
        Regex DividePageWaitAmbiguityRegex = RegexFromStart(@"/\s*(;.*)?$", RegexOptions.IgnoreCase);

        //Jumpf target ~ regex
        Regex JumpfTarget = RegexFromStart(@"\s*~\s*$", RegexOptions.IgnoreCase);

        //Ponscripter Formatting tag region
        Regex ponscripterFormattingTagRegion = RegexFromStart(@"~[^~]*~", RegexOptions.IgnoreCase);


        List<Regex> operatorRegexList;

        //TODO: support fchk command (`if fchk "file\path.bmp"`)
        //NOTE: order here matters - must match from longest to shortest to prevent '=' matching '=='
        string[] operatorList =
        {
            @">=",
            @"<=",
            @"==",
            @"!=",
            @"<>",
            @"&&",
            @"\+",
            @"-",
            @"\*",
            @"/",
            @">",
            @"<",
            @"=",
            //"\|\|", not sure if "\|\|" is supported
        };

        string line;
        int pos;
        SubroutineDatabase subroutineDatabase;

        public CharReader(string line, SubroutineDatabase subroutineDatabase)
        {
            this.line = line;
            this.subroutineDatabase = subroutineDatabase;
            this.operatorRegexList = operatorList.Select(op => RegexFromStart(op, RegexOptions.IgnoreCase)).ToList();

            Console.WriteLine($"Full Line [{line}]");
        }

        public List<LexemeOld> ParseSection(bool sectionAllowsText)
        {
            List<LexemeOld> retLexemes = new List<LexemeOld>();
            while(true)
            {
                SkipWhiteSpace();

                if (!HasNext())
                {
                    break;
                }

                char next = Peek();

                if (next == ';')
                {
                    retLexemes.Add(PopComment());
                }
                else if (next == ':')
                {
                    //colons are kind of not used
                    retLexemes.Add(PopSymbol());
                }
                else if (next == '*')
                {
                    //always expect a label name after each *
                    retLexemes.Add(PopSymbol());
                    retLexemes.Add(PopToken());
                }
                else if(PopRegex(JumpfTarget, out LexemeOld jumpfTargetLexeme))
                {
                    retLexemes.Add(jumpfTargetLexeme);
                }
                else if(PopRegex(ponscripterFormattingTagRegion, out LexemeOld formattingTagRegion))
                {
                    retLexemes.Add(formattingTagRegion);
                }
                else if (charIsWord(next))
                {
                    //throw new NotImplementedException();
                    retLexemes.AddRange(ParseWord());
                }
                else if (sectionAllowsText && (next == '^' || next == '!' || next > 255))
                {
                    retLexemes.Add(PopDialogue());
                }
                else if(sectionAllowsText && (next == '`'))
                {
                    //For '`': Pretend single-byte text mode is just normal text mode for now...
                    retLexemes.Add(PopDialogue());
                }
                else if(sectionAllowsText && (next == '#'))
                {
                    //For '#': If you encounter a color tag at top level, most likely it's for colored text.
                    retLexemes.Add(PopHexColor());
                }
                else if(sectionAllowsText && next == '$')
                {
                    //This is actually when a string is printed like: 
                    //mov $Free1,"Chasan、Arel、Phorlakh、そしてTaliahad。"
                    //langjp: dwave_jp 0, mar_1e562_1:$Free1@
                    retLexemes.Add(PopStringVariable());
                }
                //else if(sectionAllowsText && next == '%')
                //{
                    //This is actually when a number is printed like: 
                    //mov $Free1,"Chasan、Arel、Phorlakh、そしてTaliahad。"
                    //langjp: dwave_jp 0, mar_1e562_1:$Free1@
                    //retLexemes.Add(PopStringLiteral());
                //}
                else if (sectionAllowsText && (next == '@' || next == '\\' || next == '/'))
                {
                    retLexemes.Add(PopSymbol());
                }
                else if (sectionAllowsText && (next == '\''))
                {
                    PrintLexingWarning($"WARNING: Text-mode possibly entered unintentionally from character '{next}' (ascii: {(int) next})!");
                    retLexemes.Add(PopDialogue());
                }
                else if(next <= 8)
                {
                    PrintLexingWarning($"WARNING: Got control character '{next}' (ascii: {(int)next}), which will be ignored!");
                    retLexemes.Add(PopSymbol());
                }
                else
                {
                    //error
                    ThrowLexingException("Unexpected character at top level");
                }
            }

            return retLexemes;
        }

        public List<LexemeOld> ParseWord()
        {
            //parse the function or keyword name
            LexemeOld functionNameLexeme = PopToken();
            string fn = functionNameLexeme.text;
            List<LexemeOld> lexemes = new List<LexemeOld> { functionNameLexeme };
            Console.WriteLine($"Function name {fn}");

            //check for keywords
            if(fn == "if" || fn == "notif")
            {
                lexemes.AddRange(ParseIfCondition());
            }
            else if(fn == "for")
            {
                lexemes.AddRange(ParseForBody());
            }
            else
            {
                lexemes.AddRange(ParseFunctionArguments(fn));
            }

            return lexemes;
        }

        public List<LexemeOld> ParseIfCondition()
        {
            Console.WriteLine("Parsing 'if' condition");
            return PopExpression();
        }

        public List<LexemeOld> ParseForBody()
        {
            LexemeOld matchNumericValue()
            {
                SkipWhiteSpace();
                if (Peek() == '%')
                {
                    return PopNumericVariable();
                }
                else if(charIsWord(Peek()))
                {
                    //most likely a numalias
                    return PopToken();
                }
                else
                {
                    return PopNumericLiteral();
                }
            }

            List<LexemeOld> lexemes = new List<LexemeOld>();

            //numeric variable to be assigned to
            lexemes.Add(PopNumericVariable());

            //literal equals sign (in this case, it means assignment)
            lexemes.Add(PopRegexOrError(ForEqualsRegex, "Missing '=' in for loop"));

            //numeric literal or variable
            matchNumericValue();

            //literal 'to'
            lexemes.Add(PopRegexOrError(ForToRegex, "Missing 'to' in for loop"));

            //numeric literal or variable
            matchNumericValue();

            if (HasNext() && Peek() == 's')
            {
                //literal 'step'
                lexemes.Add(PopRegexOrError(ForStepRegex, "Missing 'to' in for loop"));

                //numeric literal or variable
                matchNumericValue();
            }

            return lexemes;
        }

        public List<LexemeOld> ParseFunctionArguments(string functionName)
        {
            List<LexemeOld> lexemes = new List<LexemeOld>();

            SkipWhiteSpace();

            //If have reached the end of line/nothing left to parse, just return no arguments
            if (!HasNext())
            {
                return lexemes;
            }

            //If there is a comma after the function name, just ignore it
            if(Peek() == ',')
            {
                //Keep the comma for now.
                lexemes.Add(PopSymbol());
                SkipWhiteSpace();
            }


            //try to determine how many arguments there are
            if (subroutineDatabase.TryGetValue(functionName, out SubroutineInformation subroutineInformation))
            {
                Console.WriteLine($"Parsing function {functionName} which has {subroutineInformation.hasArguments} arguments");
                if (subroutineInformation.hasArguments)
                {
                    //parse the first argument if it has more than one argument
                    //TODO: should proabably group tokens here rather than doing later? not sure....
                    List<LexemeOld> firstArg = PopExpression();
                    lexemes.AddRange(firstArg);
                    //Console.WriteLine($"First argument {firstArg}");

                    SkipWhiteSpace();
                    while (HasNext())
                    {
                        //Just assume there is one argument after each comma 
                        //(the engine will actually accept spaces instead of commas)
                        char nextChar = Peek();
                        if (nextChar != ',')
                        {
                            break;
                        }
                        LexemeOld symbol = PopSymbol();
                        lexemes.Add(symbol);
                        //Console.WriteLine($"Symbol: {symbol}");

                        //Add the expected expression after each comma
                        List<LexemeOld> expression = PopExpression();
                        lexemes.AddRange(expression);
                        //Console.WriteLine($"Symbol: {expression}");
                        SkipWhiteSpace();
                    }
                }
            }
            else
            {
                ThrowLexingException($"Unknown num args for function '{functionName}'");
            }

            return lexemes;
        }

        private List<LexemeOld> PopExpression()
        {
            //TODO: assume an expression is just a token until proper expression handling added
            //return new List<Lexeme> { PopToken() }; 
            List<LexemeOld> lexemes = new List<LexemeOld> { PopExpressionData() };

            // Pop subsequent operator expression sequences (no support parenthesis '(' or ')' yet)
            while(true)
            {
                //Due to ambiguity with the pagewait operator ('/') and the divide operator ('/'),
                //Assume that the expression has ended if all that is left on the line is a '/'
                if(DividePageWaitAmbiguityRegex.IsMatch(this.line, this.pos))
                {
                    break;
                }

                if(TryPopExpressionOperator(out LexemeOld lexeme))
                {
                    lexemes.Add(lexeme);
                }
                else
                {
                    break;
                }

                lexemes.Add(PrintTee("Popped Expression Data", PopExpressionData()));
            }

            return lexemes;
        }


        private bool TryPopExpressionOperator(out LexemeOld expressionOperator)
        {
            SkipWhiteSpace();

            foreach(Regex r in this.operatorRegexList)
            {
                if (PopRegex(r, out LexemeOld lexeme))
                {
                    expressionOperator = lexeme;
                    Console.WriteLine($"Popped Expression Operator {lexeme}");
                    return true;
                }
            }

            expressionOperator = null;
            return false;
        }

        // Pop the operand part of an expression like variables, string literals, array literals, labels etc.

        //TODO: need to support expression datas like: mov $?r[kin][0],"bmp\r_click\cha_btn\non"
        //possibly any combination of $?, $%tmp, if %%Free1 = 0 jumpf etc.
        //Maybe just allow lexing anything which looks reasonable, and validate it at a later stage
        //Also need to update if/for statements to allow the use of these types of expression data
        private LexemeOld PopExpressionData()
        {
            SkipWhiteSpace();
            char next = Peek();

            if (next == '"' || next == '^')
            {
                return PopStringLiteral();
            }
            else if (next == '%')
            {
                return PopNumericVariable();
            }
            else if (next == '$')
            {
                return PopStringVariable();
            }
            else if (next == '#')
            {
                return PopHexColor();
            }
            else if (next == '?')
            {
                return  PopRegexOrError(arrayRegex, "Failed to match array regex");
            }
            else if (next == '*')
            {
                //always expect a label name after each *
                return PopRegexOrError(labelRegex, "Failed to match array regex");
            }
            else if (next == '-' || char.IsDigit(next))
            {
                return PopNumericLiteral();
            }

            //this is probably a numalias or special word (like movie option "nosound")
            return PopToken();

            //throw GetLexingException("Failed to pop expression data");
        }

        private LexemeOld PopNumericVariable()
        {
            return PopRegexOrError(numericVariableRegex, "Failed to match (%) Numeric Variable");
        }

        private LexemeOld PopStringVariable()
        {
            return PopRegexOrError(stringVariableRegex, "Failed to match ($) String Variable");
        }

        private LexemeOld PopNumericLiteral()
        {
            return PopRegexOrError(numericLiteralRegex, "Failed to match numeric literal");
        }

        private LexemeOld PopHexColor()
        {
            return PopRegexOrError(hexColorRegex, "Failed to match hex color");
        }


        private bool HasNext()
        {
            return this.pos < this.line.Length;
        }

        private char Peek()
        {
            if (this.pos >= this.line.Length)
            {
                throw new Exception("CharReader ran out of characters - missing EOL check?");
            }

            return line[this.pos];
        }

        private char Pop()
        {
            char retval = Peek();
            this.pos++;
            return retval;
        }


        private LexemeOld PopSymbol()
        {
            SkipWhiteSpace();
            return new LexemeOld(Pop().ToString());
        }

        private LexemeOld PopComment()
        {
            SkipWhiteSpace();
            LexemeOld retval = new LexemeOld(this.line.Substring(this.pos));
            this.pos = this.line.Length;
            return retval;
        }

        private LexemeOld PopToken()
        {
            SkipWhiteSpace();
            int start = this.pos;
            int length = 0;
            while(HasNext() && charIsWord(Peek()))
            {
                Pop();
                length++;
            }

            if(length == 0)
            {
                ThrowLexingException($"PopToken() Failed");
            }

            return new LexemeOld(this.line.Substring(start, length));
        }

        private LexemeOld PopStringLiteral()
        {
            SkipWhiteSpace();
            int start = this.pos;

            //Take the first delimiter off
            char delimiter = Pop();
            int length = 1;

            while (HasNext())
            {
                char string_literal_or_delimiter = Pop();
                length++;

                if (string_literal_or_delimiter == delimiter)
                {
                    return new LexemeOld(this.line.Substring(start, length));
                }
            }

            throw new Exception("Missing string delimiter");
        }

        private LexemeOld PopRegexOrError(Regex r, string errormsg)
        {
            if (PopRegex(r, out LexemeOld lexeme))
            {
                return lexeme;
            }
            else
            {
                throw new Exception(errormsg);
            }
        }

        private bool PopRegex(Regex r, out LexemeOld lexeme)
        {
            SkipWhiteSpace();
            int start = this.pos;

            Match match = r.Match(this.line, start);
            if (match.Success)
            {
                this.pos += match.Length;
                lexeme = new LexemeOld(match.Value);
                return true;
            }
            else
            {
                lexeme = null;
                return false;
            }
        }

        private LexemeOld PopDialogue()
        {
            int start = this.pos;
            int length = 0;

            while (HasNext())
            {
                char next = Peek();

                if(next == '@' || TextLineEndRegex.IsMatch(this.line, start))
                {
                    break;
                }
                else
                {
                    Pop();
                    length++;
                }
            }

            return new LexemeOld(this.line.Substring(start, length));
        }

        private void SkipWhiteSpace()
        {
            while (HasNext() && char.IsWhiteSpace(Peek()))
            {
                Pop();
            }
        }

        private bool charIsWord(char c)
        {
            return c <= 255 && (char.IsLetterOrDigit(c) || c == '_');
        }

        private void ThrowLexingException(string message)
        {
            throw GetLexingException(message);
        }

        private Exception GetLexingException(string message)
        {
            throw new Exception(PrintLexingWarning(message));
        }
        private string PrintLexingWarning(string message)
        {
            string fullMessage = $"{message} From:{this.line.Substring(this.pos)}";
            Console.WriteLine(fullMessage);
            return fullMessage;
        }

        private static Regex RegexFromStart(string s, RegexOptions options)
        {
            return new Regex(@"\G" + s, options);
        }

        private static LexemeOld PrintTee(string message, LexemeOld l)
        {
            Console.WriteLine($"{message} '{l}'");
            return l;
        }
    }
}
