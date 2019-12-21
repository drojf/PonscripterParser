//Please see UncleMion's POnscripter Documentation here: https://www.drojf.com/nscripter/NScripter_API_Reference.html (mirror of website as original was down)

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

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
        public LexemeType type;


        public Lexeme(LexemeType type, string text)
        {
            this.text = text;
            this.type = type;
        }

        public override string ToString()
        {
            return $"{type}: \"{text}\"";
        }
    }

    class Program
    {
        static bool in_ignore_region;
        static string prev_line = "";


        //foreach (Lexeme lexeme in test.lexemes)
        //{
        //    simpleWriter.Append($"[{lexeme.text}]");
        //}

        //simpleWriter.Append($"\n");


        static bool LineIsEmptyText(LexerTest test)
        {
            // line must have at least one lexeme
            if (test.lexemes.Count <= 0)
            {
                return false;
            }

            // Line must have langjp or langen as first item
            string firstLexeme = test.lexemes[0].text.Trim().ToLower();
            if (firstLexeme != "langjp" && firstLexeme != "langen")
            {
                return false;
            }

            // If any lexeme is Dialogue, skip the node
            foreach (Lexeme lexeme in test.lexemes)
            {
                // Only allow dialogue if it's not a special text command
                if (lexeme.type == LexemeType.DIALOGUE)
                {
                    // Don't count exclamation commands as it doesn't emit any characters
                    System.Text.RegularExpressions.Match result = Regexes.exclamationTextCommand.Match(lexeme.text.Trim());
                    if (!result.Success || result.Length != lexeme.text.Length)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        static void ProcessLine(string line, SubroutineDatabase subroutineDatabase, RenpyScriptBuilder scriptBuilder, TreeWalker walker, StringBuilder simpleWriter, bool isProgramBlock)
        {
            //            Console.WriteLine(line);
            //scriptBuilder.AppendComment(line);

            LexerTest test = new LexerTest(line, subroutineDatabase);
            test.LexSection(isProgramBlock);

            /*foreach (Lexeme lexeme in test.lexemes)
            {
                simpleWriter.Append($"[{lexeme}]");
            }

            simpleWriter.Append($"\n");
            */


            bool lineIsEmpty = LineIsEmptyText(test);

            if(lineIsEmpty)
            {
                if(!in_ignore_region)
                {
                    // Remove dupe lines
                    const string line_to_emit = "mov %disable_adv_clear, 1";
                    if(line_to_emit != prev_line)
                    {
                        simpleWriter.AppendLine(line_to_emit);
                    }

                    in_ignore_region = true;
                }
            }

            if (!lineIsEmpty)
            {
                if (in_ignore_region)
                {
                    const string line_to_emit = "mov %disable_adv_clear, 0";
                    if (line != line_to_emit)
                    {
                        simpleWriter.AppendLine(line_to_emit);
                    }

                    in_ignore_region = false;
                }
            }


            simpleWriter.AppendLine(line);


            prev_line = line;


            // Skip parsing for now as we don't need it
            //Parser p = new Parser(test.lexemes, subroutineDatabase);
            //List<Node> nodes = p.Parse();


            // For now, just parse, don't walk
            //walker.WalkOneLine(nodes);
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
            int program_size = lines.Length - game_line.Value;
            string[] program = new string[program_size];
            Array.Copy(lines, game_line.Value, program, 0, program_size);

            return new CodeBlocks(header, definition, program);
        }

        static void CompileScript(string[] lines, SubroutineDatabase subroutineDatabase)
        {
            RenpyScriptBuilder scriptBuilder = new RenpyScriptBuilder();
            TreeWalker walker = new TreeWalker(scriptBuilder);

            CodeBlocks cbs = ReadSegments(lines);

            StringBuilder simpleWriter = new StringBuilder();

            // Write to Init Region
            //scriptBuilder.SetBodyRegion();
            foreach (string line in cbs.header)
            {
                ProcessLine(line, subroutineDatabase, scriptBuilder, walker, simpleWriter, isProgramBlock: true);
            }

            foreach (string line in cbs.definition)
            {
                ProcessLine(line, subroutineDatabase, scriptBuilder, walker, simpleWriter, isProgramBlock: true);
            }

            // Write to Body Region
            //scriptBuilder.SetBodyRegion();
            foreach (string line in cbs.program)
            {

                ProcessLine(line, subroutineDatabase, scriptBuilder, walker, simpleWriter, isProgramBlock: true);
            }

            string savePath = @"C:\drojf\large_projects\ponscripter_parser\renpy\ponscripty\game\script.rpy";
            scriptBuilder.SaveFile("prelude.rpy", savePath);

            using (StreamWriter writer = File.CreateText(savePath))
            {
                writer.Write(simpleWriter.ToString());
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
            database["showlangen"] = new SubroutineInformation(false);
            database["showlangjp"] = new SubroutineInformation(false);
            database["langall"] = new SubroutineInformation(false); //TODO: not sure if this is a real function or a typo. Only occurs once in umineko question script.

            database["endroll"] = new SubroutineInformation(true);
            database["steamsetachieve"] = new SubroutineInformation(true);
            database["getreadlang"] = new SubroutineInformation(true);
            database["say"] = new SubroutineInformation(true);
            database["tachistate"] = new SubroutineInformation(true);


            foreach (KeyValuePair<string, SubroutineInformation> kvp in database.GetRawDict())
            {
                Console.WriteLine($"{kvp.Key}: {kvp.Value.hasArguments}");
            }

            CompileScript(lines, database);

            Console.WriteLine("----------------\nProgram Finished");
            Console.ReadKey();
        }
    }
}
