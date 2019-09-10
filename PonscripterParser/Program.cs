using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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
    }

    class CharReader
    {   //is \G required at start of regexes?
        Regex hexColorRegex = new Regex(@"#[0-9abcdef]{6}", RegexOptions.IgnoreCase);
        Regex numericVariableRegex = new Regex(@"%\w+", RegexOptions.IgnoreCase);
        Regex stringVariableRegex = new Regex(@"\$\w+", RegexOptions.IgnoreCase);
        Regex arrayRegex = new Regex(@"\?\w+(\[\w+\])+", RegexOptions.IgnoreCase);
        Regex labelRegex = new Regex(@"\*\w+", RegexOptions.IgnoreCase);
        Regex numericLiteralRegex = new Regex(@"-?\d+", RegexOptions.IgnoreCase);

        List<Regex> operatorRegexList;

        //TODO: support fchk command (`if fchk "file\path.bmp"`)
        string[] operatorList =
        {
            @"\+",
            @"-",
            @"\*",
            @"/",
            @">",
            @"<",
            @"=",
            @">=",
            @"<=",
            @"==",
            @"!=",
            @"<>",
            @"&&",
            //"\|\|", not sure if "\|\|" is supported
        };

        string line;
        int pos;
        SubroutineDatabase subroutineDatabase;

        public CharReader(string line, SubroutineDatabase subroutineDatabase)
        {
            this.line = line;
            this.subroutineDatabase = subroutineDatabase;
            this.operatorRegexList = operatorList.Select(op => new Regex(op)).ToList();

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
                else if(next == '~')
                {
                    //jumpf target, unless found in text region
                    retLexemes.Add(PopSymbol());
                }
                else if (charIsWord(next))
                {
                    //throw new NotImplementedException();
                    retLexemes.AddRange(ParseWord());
                }
                else if (sectionAllowsText && next == '^')
                {
                    retLexemes.Add(PopDialogue());
                }
                else if (sectionAllowsText && (next == '@' || next == '\\' || next == '/'))
                {
                    retLexemes.Add(PopSymbol());
                }
                else
                {
                    //error
                    throw new Exception($"Unexpected character: {line}");
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

            //check for keywords
            if(fn == "if" || fn == "notif")
            {
                lexemes.AddRange(ParseIfCondition());
            }
            else
            {
                lexemes.AddRange(ParseFunctionArguments(fn));
            }

            return lexemes;
        }

        public List<Lexeme> ParseIfCondition()
        {
            return PopExpression();
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

            char next = Peek();

            //try to determine how many arguments there are
            if (subroutineDatabase.TryGetValue(functionName, out SubroutineInformation subroutineInformation))
            {
                Console.WriteLine($"Parsing function {functionName} which has {subroutineInformation.hasArguments} arguments");
                if (subroutineInformation.hasArguments)
                {
                    //parse the first argument if it has more than one argument
                    //TODO: should proabably group tokens here rather than doing later? not sure....
                    lexemes.AddRange(PopExpression());

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
                        lexemes.Add(PopSymbol());

                        //Add the expected expression after each comma
                        lexemes.AddRange(PopExpression());
                        SkipWhiteSpace();
                    }
                }
            }
            else
            {
                throw new Exception($"Unknown number of args for {functionName}");
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
                if(TryPopExpressionOperator(out Lexeme lexeme))
                {
                    lexemes.Add(lexeme);
                }
                else
                {
                    break;
                }

                lexemes.Add(PopExpressionData());
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
                    return true;
                }
            }

            expressionOperator = null;
            return false;
        }

        // Pop the operand part of an expression like variables, string literals, array literals, labels etc.
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
                return PopRegexOrError(numericVariableRegex, "Failed to match (%) Numeric Variable");
            }
            else if (next == '$')
            {
                return  PopRegexOrError(stringVariableRegex, "Failed to match ($) String Variable");
            }
            else if (next == '#')
            {
                return PopRegexOrError(hexColorRegex, "Failed to match hexColor");
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
                return PopRegexOrError(numericLiteralRegex, "Failed to match numeric literal in expression");
            }

            return PopToken();
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
                throw new Exception($"Expected token on line {this.line}");
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

                if(next == '@' || next == '\\' || next == '/')
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
            return char.IsLetterOrDigit(c) || c == '_';
        }

        private string GetUnparsedLine()
        {
            return this.line.Substring(this.pos);
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


            foreach(KeyValuePair<string, SubroutineInformation> kvp in database.GetRawDict())
            {
                Console.WriteLine($"{kvp.Key}: {kvp.Value.hasArguments}");
            }

            RunParser(lines, database);

            Console.WriteLine("----------------\nProgram Finished");
            Console.ReadKey();
        }
    }
}
