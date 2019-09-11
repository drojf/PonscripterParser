//Please see UncleMion's POnscripter Documentation here: https://www.drojf.com/nscripter/NScripter_API_Reference.html (mirror of website as original was down)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace PonscripterParser
{
    class CodeBlocks
    {
        public string[] header;
        public string[] definition;
        public string[] program;

        public CodeBlocks(string[] header, string[] definition, string[] program)
        {
            this.header = header;
            this.definition = definition;
            this.program = program;
        }
    }

    class Lexeme
    {
        public string text;

        public Lexeme(string text)
        {
            this.text = text;
        }

        public override string ToString()
        {
            return this.text;
        }
    }

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

        public List<Lexeme> ParseSection(bool sectionAllowsText)
        {
            List<Lexeme> retLexemes = new List<Lexeme>();
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
                else if(PopRegex(JumpfTarget, out Lexeme jumpfTargetLexeme))
                {
                    retLexemes.Add(jumpfTargetLexeme);
                }
                else if(PopRegex(ponscripterFormattingTagRegion, out Lexeme formattingTagRegion))
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

        public List<Lexeme> ParseWord()
        {
            //parse the function or keyword name
            Lexeme functionNameLexeme = PopToken();
            string fn = functionNameLexeme.text;
            List<Lexeme> lexemes = new List<Lexeme> { functionNameLexeme };
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

        public List<Lexeme> ParseIfCondition()
        {
            Console.WriteLine("Parsing 'if' condition");
            return PopExpression();
        }

        public List<Lexeme> ParseForBody()
        {
            Lexeme matchNumericValue()
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

            List<Lexeme> lexemes = new List<Lexeme>();

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

        public List<Lexeme> ParseFunctionArguments(string functionName)
        {
            List<Lexeme> lexemes = new List<Lexeme>();

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
                    List<Lexeme> firstArg = PopExpression();
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
                        Lexeme symbol = PopSymbol();
                        lexemes.Add(symbol);
                        //Console.WriteLine($"Symbol: {symbol}");

                        //Add the expected expression after each comma
                        List<Lexeme> expression = PopExpression();
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

        private List<Lexeme> PopExpression()
        {
            //TODO: assume an expression is just a token until proper expression handling added
            //return new List<Lexeme> { PopToken() }; 
            List<Lexeme> lexemes = new List<Lexeme> { PopExpressionData() };

            // Pop subsequent operator expression sequences (no support parenthesis '(' or ')' yet)
            while(true)
            {
                //Due to ambiguity with the pagewait operator ('/') and the divide operator ('/'),
                //Assume that the expression has ended if all that is left on the line is a '/'
                if(DividePageWaitAmbiguityRegex.IsMatch(this.line, this.pos))
                {
                    break;
                }

                if(TryPopExpressionOperator(out Lexeme lexeme))
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


        private bool TryPopExpressionOperator(out Lexeme expressionOperator)
        {
            SkipWhiteSpace();

            foreach(Regex r in this.operatorRegexList)
            {
                if (PopRegex(r, out Lexeme lexeme))
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
        private Lexeme PopExpressionData()
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

        private Lexeme PopNumericVariable()
        {
            return PopRegexOrError(numericVariableRegex, "Failed to match (%) Numeric Variable");
        }

        private Lexeme PopStringVariable()
        {
            return PopRegexOrError(stringVariableRegex, "Failed to match ($) String Variable");
        }

        private Lexeme PopNumericLiteral()
        {
            return PopRegexOrError(numericLiteralRegex, "Failed to match numeric literal");
        }

        private Lexeme PopHexColor()
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


        private Lexeme PopSymbol()
        {
            SkipWhiteSpace();
            return new Lexeme(Pop().ToString());
        }

        private Lexeme PopComment()
        {
            SkipWhiteSpace();
            Lexeme retval = new Lexeme(this.line.Substring(this.pos));
            this.pos = this.line.Length;
            return retval;
        }

        private Lexeme PopToken()
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

            return new Lexeme(this.line.Substring(start, length));
        }

        private Lexeme PopStringLiteral()
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
                    return new Lexeme(this.line.Substring(start, length));
                }
            }

            throw new Exception("Missing string delimiter");
        }

        private Lexeme PopRegexOrError(Regex r, string errormsg)
        {
            if (PopRegex(r, out Lexeme lexeme))
            {
                return lexeme;
            }
            else
            {
                throw new Exception(errormsg);
            }
        }

        private bool PopRegex(Regex r, out Lexeme lexeme)
        {
            SkipWhiteSpace();
            int start = this.pos;

            Match match = r.Match(this.line, start);
            if (match.Success)
            {
                this.pos += match.Length;
                lexeme = new Lexeme(match.Value);
                return true;
            }
            else
            {
                lexeme = null;
                return false;
            }
        }

        private Lexeme PopDialogue()
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

            return new Lexeme(this.line.Substring(start, length));
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

        private static Lexeme PrintTee(string message, Lexeme l)
        {
            Console.WriteLine($"{message} '{l}'");
            return l;
        }
    }

    class Program
    {
        static void ParseLine(string line, SubroutineDatabase subroutineDatabase, bool isProgramBlock)
        {
            CharReader cr = new CharReader(line, subroutineDatabase);
            List<Lexeme> l = cr.ParseSection(isProgramBlock);
            /*foreach(Lexeme s in l)
            {
                Console.WriteLine(s.text);
            }*/
        }

        static void PrintLines(string[] lines)
        {
            foreach (string line in lines)
            {
                Console.WriteLine(line);
            }
        }

        static string[] LoadScript()
        {
            const string script_name = @"C:\drojf\large_projects\umineko\umineko-question\InDevelopment\ManualUpdates\0.utf"; //@"example_input.txt";
            return File.ReadAllLines(script_name);
        }

        //TODO: write test which checks that header + "*define" + definition + "game\n*start "+ prorgam = entire script
        static CodeBlocks ReadSegments(string[] lines)
        {
            int? define_line = null;
            int? game_line = null;
            int? start_line = null;

            int line_number = 0;
            foreach(string line in lines)
            {
                string trimmed_line = line.Trim().ToLower();

                if(trimmed_line == "*define")
                {
                    if(define_line != null)
                    {
                        throw new SegmentException("More than one *define found");
                    }
                    else
                    {
                        define_line = line_number;
                    }
                }
                else if(trimmed_line == "game")
                {
                    if (game_line != null)
                    {
                        throw new SegmentException("More than one game exception");
                    }
                    else
                    {
                        game_line = line_number;
                    }

                } else if(trimmed_line == "*start")
                {
                    start_line = line_number;
                }

                line_number++;
            }

            if(!define_line.HasValue || !game_line.HasValue || !start_line.HasValue)
            {
                throw new SegmentException("One or more segments were missing (*define, game or *start)");       
            }

            //start of file to "*define" is header
            int header_size = define_line.Value;
            string[] header = new string[header_size];
            Array.Copy(lines, 0, header, 0, header_size);

            //from "*define" to "game" is define section
            int definition_size = game_line.Value - define_line.Value;
            string[] definition = new string[definition_size];
            Array.Copy(lines, define_line.Value, definition, 0, definition_size);

            //from "game" to "*start" is nothing (probably)

            //from "*start" to end of file is program
            int program_size = lines.Length - start_line.Value;
            string[] program = new string[program_size];
            Array.Copy(lines, start_line.Value, program, 0, program_size);

            return new CodeBlocks(header, definition, program);
        }

        static void RunParser(string[] lines, SubroutineDatabase subroutineDatabase)
        {
            CodeBlocks cbs = ReadSegments(lines);

            foreach (string line in cbs.program)
            {
                ParseLine(line, subroutineDatabase, isProgramBlock: true);
            }
        }

        static void Main(string[] args)
        {



            string[] lines = LoadScript();

            SubroutineDatabase database = new SubroutineDatabase();
            SubroutineDatabase functionDatabase = UserFunctionScanner.buildInitialUserList(lines, database);

            // Add predefined functions. If a function already exists, it will be added with an '_' prefix
            PredefinedFunctionInfoLoader.load("function_list.txt", database);

            // Add some extra pre-defined functions
            database["langen"] = new SubroutineInformation(false);
            database["langjp"] = new SubroutineInformation(false);

            database["endroll"] = new SubroutineInformation(true);
            database["steamsetachieve"] = new SubroutineInformation(true);


            foreach (KeyValuePair<string, SubroutineInformation> kvp in database.GetRawDict())
            {
                Console.WriteLine($"{kvp.Key}: {kvp.Value.hasArguments}");
            }

            RunParser(lines, database);

            Console.WriteLine("----------------\nProgram Finished");
            Console.ReadKey();
        }
    }
}
