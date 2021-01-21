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
        static string errorFileSavePath = "ponscripter_parser_errors.log";
        static Regex hexColorAnywhereRegex = new Regex(@"#[0-9abcdef]{6}", RegexOptions.IgnoreCase);

        /***
         * Also TODO:
         * 
         * 
         * Need to somehow handle cases like these where there is a clickwait after the slash on future lines
         * Or just detect these and manually fix them?
         Look for lines where there is a clickwait (@) even before any text has been emitted!
         1) identify noclear_cw (clickwaits at the start of a line). Mark as `noclear_cw` functions
         2) don't add a sl if there is a noclear_cw on the next text line.
        
            defsub *noclear_cw

            *noclear_cw
    mov %disable_adv_clear, 1
@/
    mov %disable_adv_clear, 0
return

warehous_i1e,62
*drojf_test
advchar "10"
langjp:dwave_jp 0, but_1e739:「死者ってのは安らかに眠る顔ってやつを拝ませてくれるんじゃねぇのかよ？！@:dwave_jp 0, but_1e740:　顔がねぇんだよ、俺の親父と、霧江さんの顔がねぇんだよッ！！/
sl
langen:dwave_eng 0, but_1e739:^"Dead people are supposed to have faces that look like they're sleeping peacefully, right?!^@:dwave_eng 0, but_1e740:^  They've got no faces, my dad and Kyrie-san have no faces!!^/
sl
se1 11
quakey 4,500

langjp@:dwave_jp 0, but_1e741:　どういう顔して死んじまったのか、…それすらもわからねぇんだよ！！@:dwave_jp 0, but_1e742:　何だよ俺はッ！！/
sl
langen@:dwave_eng 0, but_1e741:^  I don't even know what kind of faces they were wearing when they died!!^@:dwave_eng 0, but_1e742:^  What's wrong with me?!!^/
sl
se1 11
quakex 3,300

langjp@:dwave_jp 0, but_1e743:　親父たちのことを思い出す時は、このぐちゃぐちゃの化け物みてぇな顔をいつも思い出せってのかよ？！/
sl
langen@:dwave_eng 0, but_1e743:^  Will I have to see these smashed, monster-like faces every time I remember them?!^/
sl
se1 11
quakey 3,300

langjp@:dwave_jp 0, but_1e744:　そいつぁ最高だぜ、/
sl
langen@:dwave_eng 0, but_1e744:^  That's just great, ^/
sl
se1 11
quakex 4,500

langjp:voicedelay 3810:dwave_jp 0, but_1e745:クソ親父のにやにやとした顔を思い出さなくていいんだからよぉ、/
sl
advchar "-1"
langjp最高だぜ最高だぜ！！/
sl
advchar "10"
langen:voicedelay 3810:dwave_eng 0, but_1e745:^I didn't want to remember that old bastard's smug face anyway!^/
sl
advchar "-1"
langen^Just great, just great!!"^/
sl
se1 11
quakey 4,400
-----------------

Fix cases like these where an advchar would force the screen to clear when it should insert a clickwait instead:

            advchar "13"
langjp:dwave_jp 0, mar_4e996:「ひいッ！！！！」
sl
;＜真里亞
advchar "33"
langjp:dwave_jp 0, sak_4e391:『……………！！！』\
advchar "13"
langen:dwave_eng 0, mar_4e996:^"Eeek!!!!"
sl
advchar "33"
langen:dwave_eng 0, sak_4e391:^『......!!!』^\

         * 
         * */





        /** Completed Items

     * Lines like the following should also be affected (no / but just end of line)
   langen:dwave_eng 0, but_4e148:^"Yeah, that's it...!^@:dwave_eng 0, but_4e149:^  And, that person was...^
advchar "-1"
langen:voicedelay 1750:^...^/
     * 
     * ------------

         * ***/
        static List<string> debug_ignore_list = new List<string>
        {
            "bg ",
            "ld ",
            "cl ",
            "textoff",
            "fede ",
        };

        /// <summary>
        /// This function splits an nscripter game into its header, definition, and program sections.
        /// </summary>
        /// <param name="lines"></param>
        /// <returns></returns>
        /// TODO: write test which checks that header + "*define" + definition + "game\n*start "+ prorgam = entire script
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

        static bool ParseSection(string[] lines, SubroutineDatabase subroutineDatabase, bool isProgramBlock, out List<List<Node>> lines_nodes)
        {
            lines_nodes = new List<List<Node>>();
            bool error = false;

            using (StreamWriter writer = File.CreateText(errorFileSavePath))
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    try
                    {
                        LexerTest test = new LexerTest(line, subroutineDatabase);
                        test.LexSection(isProgramBlock);
                        Parser p = new Parser(test.lexemes, subroutineDatabase);
                        List<Node> nodes = p.Parse();
                        lines_nodes.Add(nodes);
                    }
                    catch (Exception e)
                    {
                        List<Node> dummyList = new List<Node>();
                        dummyList.Add(new CommentNode(new Lexeme(LexemeType.COMMENT, "<<<<<< PARSING ERROR >>>>>>")));
                        lines_nodes.Add(dummyList);
                        // If lexing or parsing a line fails, record this line as having an error, then move on
                        writer.WriteLine();
                        writer.WriteLine($"Error on line [{i+1}]: {line}");
                        writer.WriteLine($"Details: {e}");

                        error = true;
                    }
                }
            }

            // If any errors were found during lexing/parsing, notify the user
            if(error)
            {
                Console.Error.WriteLine($"ERROR: One or more errors were found! See file {errorFileSavePath} for details");
            }

            return error;
        }

        /// <summary>
        /// Dump top level of nodes for processing in another application or debugging
        /// NOTE: Since this only dumps the top level, only the first token of each node will be printed
        /// eg function arguments will have text as their function name, but the arguments won't be present in the output
        /// TODO: print all info in output, alternatively dump allLines structure as JSON or something
        /// </summary>
        /// <param name="lines"></param>
        /// <param name="subroutineDatabase"></param>
        /// <param name="savePath"></param>
        static bool DumpTopLevelNodes(string[] lines, SubroutineDatabase subroutineDatabase, string savePath)
        {
            RenpyScriptBuilder scriptBuilder = new RenpyScriptBuilder();

            bool error = ParseSection(lines, subroutineDatabase, isProgramBlock: true, out List<List<Node>> allLines);

            if (lines.Length != allLines.Count())
            {
                throw new Exception("lines weren't decoded correctly");
            }

            using (StreamWriter writer = File.CreateText(savePath))
            {
                foreach (var line in allLines)
                {
                    foreach (Node n in line)
                    {
                        writer.Write($"{n.GetLexeme().type}\0{n.GetLexeme().text}\0");
                    }
                    writer.WriteLine();
                }
            }

            return error;
        }

        static bool CompileScript(string[] lines, SubroutineDatabase subroutineDatabase)
        {
#if true
            RenpyScriptBuilder scriptBuilder = new RenpyScriptBuilder();
            TreeWalker walker = new TreeWalker(scriptBuilder);

            //CodeBlocks cbs = ReadSegments(lines);

            StringBuilder simpleWriter = new StringBuilder();
            StringBuilder debugBuilder = new StringBuilder();
            HashSet<string> modified_lines = new HashSet<string>();

            bool error = ParseSection(lines, subroutineDatabase, isProgramBlock: true, out List<List<Node>> allLines);
            if(error)
            {
                return error;
            }

            if(lines.Length != allLines.Count())
            {
                throw new Exception("lines weren't decoded correctly");
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
            return true;
        }

        static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Error: First argument of program must be the script to be parsed");
                Console.WriteLine("\nI suggest using this script for testing: https://github.com/07th-mod/umineko-question/raw/master/InDevelopment/ManualUpdates/0.utf");
                Console.WriteLine("\nVisual Studio users can follow these instructions to set program arguments: https://stackoverflow.com/questions/298708/debugging-with-command-line-parameters-in-visual-studio");
                Console.ReadKey();
                return -1;
            }

            // Read input script
            string inputFilePath = args[0];
            Console.WriteLine($"Processing input file [{inputFilePath}]");
            string[] lines = File.ReadAllLines(inputFilePath);

            // First pass of script is to build the function database
            SubroutineDatabase database = UserFunctionScanner.buildInitialUserList(lines, new SubroutineDatabase());

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

            bool dumpNodes = true;
            bool error = true;
            if (dumpNodes)
            {
                string savePath = @"ponscripter_script_nodes.txt";
                error = DumpTopLevelNodes(lines, database, savePath);
            }
            else
            {
                error = CompileScript(lines, database);
            }

            return error ? -2 : 0;
        }
    }
}
