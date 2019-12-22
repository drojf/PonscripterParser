//Please see UncleMion's POnscripter Documentation here: https://www.drojf.com/nscripter/NScripter_API_Reference.html (mirror of website as original was down)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
        static bool reached_code_section;

        static Regex hexColorAnywhereRegex = new Regex(@"#[0-9abcdef]{6}", RegexOptions.IgnoreCase);

        //foreach (Lexeme lexeme in test.lexemes)
        //{
        //    simpleWriter.Append($"[{lexeme.text}]");
        //}

        //simpleWriter.Append($"\n");

        static bool LineIsEmptyText(List<Lexeme> lexemes)
        {
            // line must have at least one lexeme
            if (lexemes.Count <= 0)
            {
                return false;
            }

            // Line must have langjp or langen as first item
            string firstLexeme = lexemes[0].text.Trim().ToLower();
            if (firstLexeme != "langjp" && firstLexeme != "langen")
            {
                return false;
            }

            // If any lexeme is Dialogue, skip the node
            foreach (Lexeme lexeme in lexemes)
            {
                switch (lexeme.type)
                {
                    // Any numeric or string references at the top level will be printed out, therefore line is not empty
                    case LexemeType.NUMERIC_REFERENCE:
                    case LexemeType.STRING_REFERENCE:
                        return false;

                    // Only allow dialogue if it's not a special text command
                    case LexemeType.DIALOGUE:
                        string dialogueNoHexColor = hexColorAnywhereRegex.Replace(lexeme.text.Trim(), "");

                        // Don't count exclamation commands as it doesn't emit any characters
                        System.Text.RegularExpressions.Match result = Regexes.exclamationTextCommand.Match(dialogueNoHexColor.Trim());
                        if (!result.Success || result.Length != dialogueNoHexColor.Length)
                        {
                            return false;
                        }
                        break;
                }
            }

            return true;
        }

        static List<string> debug_ignore_list = new List<string>
        {
            "bg ",
            "ld ",
            "cl ",
            "textoff",
            "fede ",
        };

        // Insert a text-off before each wait if there is no text currently on screen (was cleared by a \ (backslash))
        static void WaitTextOffFix(string[] lines, List<List<Node>> nodes_list, SubroutineDatabase subroutineDatabase, StringBuilder simpleWriter, StringBuilder debugWriter)
        {
            bool debug_inSlashRegion = false;

            for (int line_no = 0; line_no < lines.Length; line_no++)
            {
                List<Node> nodes = nodes_list[line_no];
                string line = lines[line_no];

                if (line.ToLower().Contains("langen") || line.ToLower().Contains("langjp"))
                {
                    debug_inSlashRegion = false;
                }

                foreach (string ignore_item in debug_ignore_list)
                {
                    if (line.ToLower().StartsWith(ignore_item))
                    {
                        debug_inSlashRegion = false;
                    }
                }

                if (debug_inSlashRegion && line.StartsWith("wait"))
                {
                    simpleWriter.AppendLine("textoff");
                }

                if (line == "*ep1_scroll                   ;スクロール実行本体")
                {
                    reached_code_section = true;
                }
                if (reached_code_section)
                {
                    simpleWriter.AppendLine(line);
                    continue;
                }

                List<Lexeme> lexemes = nodes.Select(x => x.GetLexeme()).ToList();

                simpleWriter.AppendLine(line);

                //Check last lexeme for a "\"
                if (lexemes.Count > 0)
                {
                    Lexeme lastLexeme = lexemes[lexemes.Count - 1];
                    if (lastLexeme.type == LexemeType.BACK_SLASH)
                    {
                        debug_inSlashRegion = true;
                    }
                }
            }
        }


        static void AppendSLToForwardSlashAndBlankLine(string[] lines, List<List<Node>> nodes_list, SubroutineDatabase subroutineDatabase, StringBuilder simpleWriter, StringBuilder debugWriter)
        {
            for(int line_no = 0; line_no < lines.Length; line_no++)
            {
                List<Node> nodes = nodes_list[line_no];
                string line = lines[line_no];

                if (line == "*ep1_scroll                   ;スクロール実行本体")
                {
                    reached_code_section = true;
                }
                if (reached_code_section)
                {
                    simpleWriter.AppendLine(line);
                    continue;
                }

                List<Lexeme> lexemes = nodes.Select(x => x.GetLexeme()).ToList();

                simpleWriter.AppendLine(line);

                bool folowed_by_disable_adv_clear_clickwait = false;
                bool got_disable_adv_clear = false;
                for (int i = 1; i < 100; i++)
                {
                    int search_line = line_no + i;
                    if (search_line > lines.Length)
                    {
                        break;
                    }

                    if(lines[search_line].Trim() == "mov %disable_adv_clear, 1")
                    {
                        got_disable_adv_clear = true;
                    }
                    else if(lines[search_line].Trim() == "mov %disable_adv_clear, 0")
                    {
                        got_disable_adv_clear = false;
                    }

                    if(lines[search_line].ToLower().Contains("langen"))
                    {
                        if (got_disable_adv_clear && lines[search_line].ToLower().Contains("\\"))
                        {
                            folowed_by_disable_adv_clear_clickwait = true;
                        }

                        break;
                    }
                }

                //Check for a noclear_cw on next few lines
                bool followed_by_noclear_cw = false;
                for (int i = 1; i < 100; i++)
                {
                    int search_line = line_no + i;
                    if (search_line > lines.Length)
                    {
                        break;
                    }

                    if (lines[search_line].ToLower().Contains("noclear_cw"))
                    {
                        followed_by_noclear_cw = true;
                        break;
                    }

                    if (lines[search_line].ToLower().Contains("langen"))
                    {
                        break;
                    }
                }

                //Check for no ending at all (dialogue as last token)
                bool lastIsEmittedDialogue = false;
                if (lexemes.Count > 0)
                {
                    Lexeme lastLexeme = lexemes[lexemes.Count - 1];
                    if (lastLexeme.type == LexemeType.DIALOGUE)
                    {
                        if (!LineIsEmptyText(lexemes))
                        {
                            lastIsEmittedDialogue = true;
                        }
                    }
                }

                //Check last lexeme for a "/"
                bool lastIsSlash = false;
                if (lexemes.Count > 0)
                {
                    Lexeme lastLexeme = lexemes[lexemes.Count - 1];
                    if (lastLexeme.type == LexemeType.OPERATOR && lastLexeme.text.Trim() == "/")
                    {
                        lastIsSlash = true;
                        
                    }
                }

                //Check second last lexeme for a "@"
                bool secondLastIsAt = false;
                if (lexemes.Count > 1)
                {
                    Lexeme secondLastLexeme = lexemes[lexemes.Count - 2];
                    if (secondLastLexeme.type == LexemeType.AT_SYMBOL)
                    {
                        secondLastIsAt = true;
                    }
                }

                // Only insert a "sl" if there was no "@" before the slash, as an "@" already forces a clickwait
                if (!followed_by_noclear_cw && !folowed_by_disable_adv_clear_clickwait)
                {
                    if ((lastIsSlash && !secondLastIsAt) || lastIsEmittedDialogue)
                    {
                        simpleWriter.AppendLine($"sl");
                    }
                }

                foreach (Lexeme lexeme in lexemes)
                {
                    debugWriter.Append($"[{lexeme}]");
                }
                debugWriter.Append($"\n");
            }
        }



        static void AppendSLToForwardSlashAndBlankLine(string line, SubroutineDatabase subroutineDatabase, RenpyScriptBuilder scriptBuilder, TreeWalker walker, StringBuilder simpleWriter, StringBuilder debugWriter, HashSet<string> modified_lines, bool isProgramBlock)
        {
            if (line == "*ep1_scroll                   ;スクロール実行本体")
            {
                reached_code_section = true;
            }
            if (reached_code_section)
            {
                simpleWriter.AppendLine(line);
                return;
            }

            LexerTest test = new LexerTest(line, subroutineDatabase);
            test.LexSection(isProgramBlock);

            Parser p = new Parser(test.lexemes, subroutineDatabase);
            List<Node> nodes = p.Parse();

            List<Lexeme> lexemes = nodes.Select(x => x.GetLexeme()).ToList();

            simpleWriter.AppendLine(line);

            //Check for no ending at all (dialogue as last token)
            bool lastIsEmittedDialogue = false;
            if (lexemes.Count > 0)
            {
                Lexeme lastLexeme = lexemes[lexemes.Count - 1];
                if (lastLexeme.type == LexemeType.DIALOGUE)
                {
                    if (!LineIsEmptyText(lexemes))
                    {
                        lastIsEmittedDialogue = true;
                    }
                }
            }

            //Check last lexeme for a "/"
            bool lastIsSlash = false;
            if(lexemes.Count > 0)
            {
                Lexeme lastLexeme = lexemes[lexemes.Count - 1];
                if(lastLexeme.type == LexemeType.OPERATOR && lastLexeme.text.Trim() == "/")
                {
                    lastIsSlash = true;
                }
            }

            //Check second last lexeme for a "@"
            bool secondLastIsAt = false;
            if(lexemes.Count > 1)
            {
                Lexeme secondLastLexeme = lexemes[lexemes.Count - 2];
                if (secondLastLexeme.type == LexemeType.AT_SYMBOL)
                {
                    secondLastIsAt = true;
                }
            }

            // Only insert a "sl" if there was no "@" before the slash, as an "@" already forces a clickwait
            if ((lastIsSlash && !secondLastIsAt) || lastIsEmittedDialogue)
            {
                simpleWriter.AppendLine($"sl");
            }

            foreach (Lexeme lexeme in lexemes)
            {
                debugWriter.Append($"[{lexeme}]");
            }
            debugWriter.Append($"\n");
        }

    

        static void ProcessLine(string line, SubroutineDatabase subroutineDatabase, RenpyScriptBuilder scriptBuilder, TreeWalker walker, StringBuilder simpleWriter, StringBuilder debugWriter, HashSet<string> modified_lines, bool isProgramBlock)
        {
            //            Console.WriteLine(line);
            //scriptBuilder.AppendComment(line);
            if (line == "*ep1_scroll                   ;スクロール実行本体")
            {
                reached_code_section = true;
            }
            if (reached_code_section)
            {
                simpleWriter.AppendLine(line);
                return;
            }

            LexerTest test = new LexerTest(line, subroutineDatabase);
            test.LexSection(isProgramBlock);

            Parser p = new Parser(test.lexemes, subroutineDatabase);
            List<Node> nodes = p.Parse();

            List<Lexeme> lexemes = nodes.Select(x => x.GetLexeme()).ToList();


            bool lineIsEnglish = line.StartsWith("langen");
            bool probablyLineIsEmpty = lineIsEnglish && !line.Contains("^");

            bool lineIsEmpty = LineIsEmptyText(lexemes);


            if (lineIsEnglish)
            {
                if(probablyLineIsEmpty && !lineIsEmpty)
                {
                    debugWriter.AppendLine($"Line [{line}] is probably empty but was detected non-empty");
                }
                else if(!probablyLineIsEmpty && lineIsEmpty)
                {
                    debugWriter.AppendLine($"Line [{line}] is probably NOT empty but was detected as empty");
                }
            }


            if (lineIsEmpty)
            {
                modified_lines.Add(line);
                if (!in_ignore_region)
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

        static List<List<Node>> ParseSection(string[] lines, SubroutineDatabase subroutineDatabase, bool isProgramBlock)
        {
            List<List<Node>> lines_nodes = new List<List<Node>>();

            foreach (string line in lines)
            {
                LexerTest test = new LexerTest(line, subroutineDatabase);
                test.LexSection(isProgramBlock);

                Parser p = new Parser(test.lexemes, subroutineDatabase);
                List<Node> nodes = p.Parse();

                lines_nodes.Add(nodes);
            }

            return lines_nodes;
        }

        static void CompileScript(string[] lines, SubroutineDatabase subroutineDatabase)
        {
#if true
            RenpyScriptBuilder scriptBuilder = new RenpyScriptBuilder();
            TreeWalker walker = new TreeWalker(scriptBuilder);

            //CodeBlocks cbs = ReadSegments(lines);

            StringBuilder simpleWriter = new StringBuilder();
            StringBuilder debugBuilder = new StringBuilder();
            HashSet<string> modified_lines = new HashSet<string>();

            List<List<Node>> allLines = ParseSection(lines, subroutineDatabase, isProgramBlock: true);

            if(lines.Length != allLines.Count())
            {
                throw new Exception("lines weren't decoded correctly");
            }

            AppendSLToForwardSlashAndBlankLine(lines, allLines, subroutineDatabase, simpleWriter, debugBuilder);

            string debugPath = @"C:\drojf\large_projects\ponscripter_parser\renpy\ponscripty\game\debug.txt";
            string savePath = @"C:\drojf\large_projects\ponscripter_parser\renpy\ponscripty\game\script.rpy";
            scriptBuilder.SaveFile("prelude.rpy", savePath);

            using (StreamWriter writer = File.CreateText(savePath))
            {
                writer.Write(simpleWriter.ToString());
            }

            using (StreamWriter writer = File.CreateText(debugPath))
            {
                writer.WriteLine("Unique Modified Lines:");
                foreach (string s in modified_lines)
                {
                    writer.WriteLine(s.ToString());
                }
                writer.WriteLine("\n\n");

                writer.WriteLine("Possibly Missed lines:");
                writer.Write(debugBuilder.ToString());
            }

#else

            RenpyScriptBuilder scriptBuilder = new RenpyScriptBuilder();
            TreeWalker walker = new TreeWalker(scriptBuilder);

            CodeBlocks cbs = ReadSegments(lines);

            StringBuilder simpleWriter = new StringBuilder();
            StringBuilder debugBuilder = new StringBuilder();
            HashSet<string> modified_lines = new HashSet<string>();

            // Write to Init Region
            //scriptBuilder.SetBodyRegion();
            foreach (string line in cbs.header)
            {
                AppendSLToForwardSlashAndBlankLine(line, subroutineDatabase, scriptBuilder, walker, simpleWriter, debugBuilder, modified_lines, isProgramBlock: true);
            }

            foreach (string line in cbs.definition)
            {
                AppendSLToForwardSlashAndBlankLine(line, subroutineDatabase, scriptBuilder, walker, simpleWriter, debugBuilder, modified_lines, isProgramBlock: true);
            }

            // Write to Body Region
            //scriptBuilder.SetBodyRegion();
            foreach (string line in cbs.program)
            {

                AppendSLToForwardSlashAndBlankLine(line, subroutineDatabase, scriptBuilder, walker, simpleWriter, debugBuilder, modified_lines, isProgramBlock: true);
            }

            string debugPath = @"C:\drojf\large_projects\ponscripter_parser\renpy\ponscripty\game\debug.txt";
            string savePath = @"C:\drojf\large_projects\ponscripter_parser\renpy\ponscripty\game\script.rpy";
            scriptBuilder.SaveFile("prelude.rpy", savePath);

            using (StreamWriter writer = File.CreateText(savePath))
            {
                writer.Write(simpleWriter.ToString());
            }

            using (StreamWriter writer = File.CreateText(debugPath))
            {
                writer.WriteLine("Unique Modified Lines:");
                foreach (string s in modified_lines)
                {
                    writer.WriteLine(s.ToString());
                }
                writer.WriteLine("\n\n");

                writer.WriteLine("Possibly Missed lines:");
                writer.Write(debugBuilder.ToString());
            }
#endif

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
