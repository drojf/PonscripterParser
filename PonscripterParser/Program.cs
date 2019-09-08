using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
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
    {
        string line;
        int pos;

        public CharReader(string line)
        {
            this.line = line;
        }

        public Lexeme NextLexeme()
        {
            SkipWhiteSpace();

            if (!HasNext())
            {
                return null;
            }

            char next = Peek();

            //Need string handling (" string and ^ string)
            if(next == ';')
            {
                return PopComment();
            }
            else if (charIsWord(next))
            {
                return PopToken();
            }
            else if (next == '"' || next == '^')
            {
                return PopStringLiteral();
            }
            else
            {
                return PopSymbol();
            }
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
            return new Lexeme(Pop().ToString());
        }


        private Lexeme PopComment()
        {
            Lexeme retval = new Lexeme(this.line.Substring(this.pos));
            this.pos = this.line.Length;
            return retval;
        }

        private Lexeme PopToken()
        {
            int start = this.pos;
            int length = 0;
            while(HasNext() && charIsWord(Peek()))
            {
                Pop();
                length++;
            }

            return new Lexeme(this.line.Substring(start, length));
        }

        private Lexeme PopStringLiteral()
        {
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
    }

    class Program
    {
        static void ParseLine(string line, bool isProgramBlock)
        {
            CharReader cr = new CharReader(line);
            while(true)
            {
                Lexeme l = cr.NextLexeme();
                if(l == null)
                {
                    break;
                }
                else
                {
                    Console.WriteLine(l.text);
                }
            }
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

        static void RunParser(string[] lines)
        {
            CodeBlocks cbs = ReadSegments(lines);

            //PrintLines(cbs.definition);

            foreach (string line in cbs.definition) {
                ParseLine(line, isProgramBlock: false);
            }
        }

        static void Main(string[] args)
        {
            string[] lines = LoadScript();
            RunParser(lines);

            Console.WriteLine("----------------\nProgram Finished");
            Console.ReadKey();
        }
    }
}
