//Please see UncleMion's POnscripter Documentation here: https://www.drojf.com/nscripter/NScripter_API_Reference.html (mirror of website as original was down)

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PonscripterParser
{
    public static class MyExtensions
    {
        public static string Quote(this string str)
        {
            return $"\"{str}\"";
        }
    }

    abstract class FunctionHandler
    {
        public abstract string[] FunctionNames();
        public abstract void HandleFunctionNode(TreeWalker walker, FunctionNode function);

        public static T VerifyType<T>(Node n) {
            switch(n)
            {
                case T node:
                    return node;
            }

            throw new Exception($"Expected type {typeof(T)}, got {n.GetType()}");
        }
    }

    class IgnoreCaseDictionary<V>
    {
        Dictionary<string, V> innerDictionary;

        public IgnoreCaseDictionary()
        {
            this.innerDictionary = new Dictionary<string, V>();
        }

        public void Set(string key, V value)
        {
            this.innerDictionary[key.ToLower()] = value;
        }

        public bool TryGetValue(string key, out V value)
        {
            return this.innerDictionary.TryGetValue(key.ToLower(), out value);
        }

        public bool Contains(string key)
        {
            return this.innerDictionary.ContainsKey(key.ToLower());
        }
    }

    class FunctionHandlerLookup
    {
        IgnoreCaseDictionary<FunctionHandler> systemFunctions;
        public IgnoreCaseDictionary<int> userFunctions;

        public FunctionHandlerLookup()
        {
            this.systemFunctions = new IgnoreCaseDictionary<FunctionHandler>();
            this.userFunctions = new IgnoreCaseDictionary<int>();
        }

        public bool TryGetFunction(string functionName, out bool isUserFunction, out FunctionHandler retHandler)
        {
            //First, check user functions to see if function with name exists
            if(userFunctions.Contains(functionName))
            {
                retHandler = new UserFunctionHandler();
                isUserFunction = true;
                return true;
            }

            //Then, check system functions to see if function exists
            if (this.systemFunctions.TryGetValue(functionName, out FunctionHandler systemFunction))
            {
                retHandler = systemFunction;
                isUserFunction = false;
                return true;
            }

            //Finally, check if function has been overridden, if it has a '_' at the front ('_' prefixed function
            if(functionName.StartsWith("_") && 
                this.systemFunctions.TryGetValue(functionName.Substring(1), out FunctionHandler overridenFunction))
            {
                retHandler = overridenFunction;
                isUserFunction = false;
                return true;
            }

            retHandler = null;
            isUserFunction = false;
            return false;
        }

        public void RegisterUserFunction(string s)
        {
            userFunctions.Set(s, 0);
            //RegisterFunctionWithCheck(userFunctions, userFunctionHandler);
        }

        public void RegisterSystemFunction(FunctionHandler systemFunctionHandler)
        {
            RegisterFunctionWithCheck(systemFunctions, systemFunctionHandler);
        }

        private void RegisterFunctionWithCheck(IgnoreCaseDictionary<FunctionHandler> dict, FunctionHandler userFunctionHandler)
        {
            foreach (string function_name in userFunctionHandler.FunctionNames())
            {
                if (dict.Contains(function_name))
                {
                    throw new Exception($"User function {function_name} was defined twice");
                }
                else
                {
                    dict.Set(function_name, userFunctionHandler);
                }
            }
        }
    }

    class UserFunctionHandler : FunctionHandler
    {
        public override string[] FunctionNames() { throw new NotSupportedException(); }

        public override void HandleFunctionNode(TreeWalker walker, FunctionNode function)
        {
            StringBuilder tempBuilder = new StringBuilder();
            List<Node> arguments = function.GetArguments();
            int argCount = arguments.Count;

            tempBuilder.Append($"call {function.functionName}");

            //add first argument, the 'label' to call ('function' name)
            if(argCount > 0)
            {
                tempBuilder.Append("(");
                tempBuilder.Append(walker.TranslateExpression(arguments[0]));

                //append the function arguments, if any
                for (int i = 1; i < argCount; i++)
                {
                    tempBuilder.Append(", ");
                    tempBuilder.Append(walker.TranslateExpression(arguments[i]));
                }
                tempBuilder.Append(")");
            }

            walker.scriptBuilder.EmitStatement(tempBuilder.ToString());
        }
    }

    abstract class BinaryOpFunction : FunctionHandler
    {
        abstract public string Op();

        public override void HandleFunctionNode(TreeWalker walker, FunctionNode function)
        {
            List<Node> arguments = function.GetArguments(2);
            string lvalue = walker.TranslateExpression(arguments[0]);
            string assigned_value = walker.TranslateExpression(arguments[1]);
            walker.scriptBuilder.EmitPython($"{lvalue} {Op()} {assigned_value}");
        }
    }
    class IncHandler : FunctionHandler
    {
        public override string[] FunctionNames() => new string[] { "inc"};

        public override void HandleFunctionNode(TreeWalker walker, FunctionNode function)
        {
            List<Node> arguments = function.GetArguments(1);
            string lvalue = walker.TranslateExpression(arguments[0]);
            walker.scriptBuilder.EmitPython($"{lvalue} += 1");
        }
    }

    class DecHandler : FunctionHandler
    {
        public override string[] FunctionNames() => new string[] { "dec" };

        public override void HandleFunctionNode(TreeWalker walker, FunctionNode function)
        {
            List<Node> arguments = function.GetArguments(1);
            string lvalue = walker.TranslateExpression(arguments[0]);
            walker.scriptBuilder.EmitPython($"{lvalue} -= 1");
        }
    }

    class AddHandler : BinaryOpFunction
    {
        public override string[] FunctionNames() => new string[] { "add" };
        
        public override string Op() => "+=";
    }

    class MovHandler : BinaryOpFunction
    {
        public override string[] FunctionNames() => new string[] { "mov" };

        public override string Op() => "=";
    }

    class StringAliasHandler : FunctionHandler
    {
        public override string[] FunctionNames() => new string[] { "stralias" };

        public override void HandleFunctionNode(TreeWalker walker, FunctionNode function)
        {
            TreeWalker.HandleAliasNode(walker, function, isNumAlias: false);
        }
    }

    class NumAliasHandler : FunctionHandler
    {
        public override string[] FunctionNames() => new string[] { "numalias" };

        public override void HandleFunctionNode(TreeWalker walker, FunctionNode function)
        {
            TreeWalker.HandleAliasNode(walker, function, isNumAlias:true);
        }
    }

    class DefSubHandler : FunctionHandler
    {
        public override string[] FunctionNames() => new string[] { "defsub" };

        public override void HandleFunctionNode(TreeWalker walker, FunctionNode function)
        {
            List<Node> arguments = function.GetArguments(1);
            walker.functionLookup.RegisterUserFunction(VerifyType<AliasNode>(arguments[0]).aliasName);
        }
    }

    class GoSubHandler : FunctionHandler
    {
        public override string[] FunctionNames() => new string[] { "gosub" };

        public override void HandleFunctionNode(TreeWalker walker, FunctionNode function)
        {
            List<Node> arguments = function.GetArguments(1);
            LabelNode gosubTargetLabel = VerifyType<LabelNode>(arguments[0]);
            walker.scriptBuilder.EmitStatement($"call {gosubTargetLabel.labelName}");
        }
    }

    class JumpfHandler : FunctionHandler
    {
        //TODO: refactor handling of jumpf/~ - some logic defined here, and some logic is defined in top level handler
        //TODO: Renpy supports local label names (prefix with '.'), however this
        // may break some ponscripter scripts which (erroneously?) jumpf through subroutines
        // For now, leave as global labels.
        public override string[] FunctionNames() => new string[] { "jumpf" };

        public override void HandleFunctionNode(TreeWalker walker, FunctionNode function)
        {
            walker.sawJumpfCommand = true;
            walker.scriptBuilder.EmitStatement($"jump {GetJumpfLabelNameFromID(walker.jumpfTargetCount)}");
        }

        static public string GetJumpfLabelNameFromID(int jumpfID)
        {
            return $"jumpf_target_{jumpfID}";
        }

        static public string GetUnreachableJumpfLabelNameFromID(int jumpfID)
        {
            return $"unreachable_jumpf_target_{jumpfID}";
        }
    }


    class GetParamHandler : FunctionHandler
    {
        public override string[] FunctionNames() => new string[] { "getparam" };

        public override void HandleFunctionNode(TreeWalker walker, FunctionNode function)
        {
            for(int i = 0; i < function.GetArguments().Count; i++)
            {
                string s = walker.TranslateExpression(function.GetArguments()[i]);
                walker.scriptBuilder.EmitPython($"{s} = pons_args[{i}]");
            }           
        }
    }

    class GotoHandler : FunctionHandler
    {
        public override string[] FunctionNames() => new string[] { "goto" };

        public override void HandleFunctionNode(TreeWalker walker, FunctionNode function)
        {
            List<Node> arguments = function.GetArguments(1);
            LabelNode labelNode = VerifyType<LabelNode>(arguments[0]);

            walker.scriptBuilder.EmitStatement($"jump {TreeWalker.MangleLabelName(labelNode.labelName)}");
        }
    }

    class DimHandler : FunctionHandler
    {
        public override string[] FunctionNames() => new string[] { "dim" };

        public override void HandleFunctionNode(TreeWalker walker, FunctionNode function)
        {
            List<Node> arguments = function.GetArguments(1);
            ArrayReferenceNode arrayReference = VerifyType<ArrayReferenceNode>(arguments[0]);
            //walker.scriptBuilder.EmitPython($"{walker.TranslateArray(arrayReference) }");

            StringBuilder tempBuilder = new StringBuilder();

            tempBuilder.Append($"{TreeWalker.MangleArrayName(arrayReference.arrayName)} = Dim(");

            tempBuilder.Append($"{walker.TranslateExpression(arrayReference.nodes[0])}");
            
            for(int i = 1; i < arrayReference.nodes.Count; i++)
            {
                tempBuilder.Append($", {walker.TranslateExpression(arrayReference.nodes[i])}");
            }

            tempBuilder.Append(")");

            walker.scriptBuilder.EmitPython(tempBuilder.ToString());
        }
    }

    class NextHandler : FunctionHandler
    {
        public override string[] FunctionNames() => new string[] { "next" };

        public override void HandleFunctionNode(TreeWalker walker, FunctionNode function)
        {
            if(walker.forNextStatementIncrementString.Count == 0)
            {
                walker.scriptBuilder.AppendWarning("'Next' statement without matching 'For' statement");
            }

            walker.scriptBuilder.EmitPython(walker.forNextStatementIncrementString.Pop());

            //Reduce indent by 1 as while loop is finished
            walker.scriptBuilder.ModifyIndentPermanently(-1);
        }
    }

    class LspHandler : FunctionHandler
    {
        public override string[] FunctionNames() => new string[] { "lsp" };

        public override void HandleFunctionNode(TreeWalker walker, FunctionNode function)
        {
            List<Node> arguments = function.GetArguments(4,5);
            string spriteNumber = walker.TranslateExpression(arguments[0]);
            string filename_or_tag = walker.TranslateExpression(arguments[1]);
            string top_left_x = walker.TranslateExpression(arguments[2]);
            string top_left_y = walker.TranslateExpression(arguments[3]);

            string opacity = "100";
            if(arguments.Count >= 5)
            {
                opacity = walker.TranslateExpression(arguments[4]);
            }

            //ExtractTagsAndFilename(filename_or_tag, out string filename, out string tags);
            //string filename_no_ext = Path.GetFileNameWithoutExtension(filename);

            //save all the sprite information to the sprite number:sprite object hashmap
            //load and display the image
            walker.scriptBuilder.EmitPython($"pons_lsp({spriteNumber}, {filename_or_tag}, {top_left_x}, {top_left_y}, {opacity})");

            /*walker.scriptBuilder.EmitPython($"sprite_number[{}] = {}");

            walker.scriptBuilder.EmitStatement($"show {filename_no_ext}:");

            //TODO: x and y pos need to match the positioning of Ponscripter - currently taken as top left of screen coord.
            walker.scriptBuilder.EmitStatement($"xpos {top_left_x}");
            walker.scriptBuilder.EmitStatement($"ypos {top_left_y}");
            
            if (arguments.Count == 5)
            {
                string opacity = walker.TranslateExpression(arguments[4]);
                walker.scriptBuilder.EmitStatement($"alpha {opacity}");
            }*/


            //TODO: probably better to make a renpy function which handles all of this which mirrors the lsp of ponscripter.


            //Use something like this to implement
            //    $ renpy.show("eileen " + "vhappy", at_list=[makeTransform(200)], tag="tag1")
            /*Where makeTransform is:
             *     def makeTransform(xpos):
                    t = Transform()
                    t.xpos = xpos
                    return t
             * 
             * 
             * */


        }

        //TODO: need to do this in python as string value may not be known until runtime
        /*private void ExtractTagsAndFilename(string filenameWithTags, out string outputFilename, out string tags)
        {
            //for now tags is always output as null
            tags = null;

            //TODO: implement proper tag extraction
            string[] split_filename = filenameWithTags.Split(new char[] { ';' });
            if(split_filename.Length == 1)
            {
                outputFilename = split_filename[0];
            }
            else if(split_filename.Length == 2)
            {
                outputFilename = split_filename[1];
            }
            else
            {
                throw new Exception($"Invalid filename {filenameWithTags}");
            }
        }*/
    }

    // Handles spbtn and exbtn command
    class SpbtnExbtnHandler : FunctionHandler
    {
        // Defines a button from a sprite which has been previously loaded with 'lsp' or similar
        // The buttons are cleared when 'csp' or similar is used to clear the associated sprite.
        public override string[] FunctionNames() => new string[] { "spbtn", "exbtn" };

        public override void HandleFunctionNode(TreeWalker walker, FunctionNode function)
        {
            string spriteNumber;
            string buttonNumber;
            string sprite_control;

            if(function.functionName == "spbtn")
            {
                List<Node> arguments = function.GetArguments(2);
                spriteNumber = walker.TranslateExpression(arguments[0]);
                buttonNumber = walker.TranslateExpression(arguments[1]);
            }
            else
            {
                List<Node> arguments = function.GetArguments(3);
                spriteNumber = walker.TranslateExpression(arguments[0]);
                buttonNumber = walker.TranslateExpression(arguments[1]);
                sprite_control = walker.TranslateExpression(arguments[2]);
            }

            walker.scriptBuilder.EmitPython($"pons_spbtn({spriteNumber}, {buttonNumber})");
        }
    }


    class Btnwait2Handler : FunctionHandler
    {
        public override string[] FunctionNames() => new string[] { "btnwait2" };

        public override void HandleFunctionNode(TreeWalker walker, FunctionNode function)
        {
            List<Node> arguments = function.GetArguments(1);
            string btnWaitResultOutputVariable = walker.TranslateExpression(arguments[0]);

            //use "call" to show the buttons, then clear them after user presses on them
            //TODO: not sure whether to use "call" or "show" here - maybe 'show' because ponscripter won't automatically clear the images off the screen.
            walker.scriptBuilder.EmitStatement($"call screen MultiButton()");

            //Renpy places the result (which button id was pressed) in the `_return` variable. 
            //Copy from the renpy `_return` variable to the user defined output variable
            walker.scriptBuilder.EmitPython($"{btnWaitResultOutputVariable} = _return");
        }
    }

    class BtndefHandler : FunctionHandler
    {
        //Note about btndef:
        //
        //I'm not sure if btndef is used "properly" in modern ponscripter scripts - it appears mainly to be used to clear the previously registered buttons
        //
        //Btndef registers which image will be used for future `btn` calls
        //See this page for an example: http://binaryheaven.ivory.ne.jp/o_show/nscripter/tyuu/03.htm
        //
        //btndef "botan.jpg"
        //btn 1,10,10,50,50,0,0
        //btn 2,10,70,50,50,50,0
        //btn 3,10,130,50,50,100,0
        //

        //this isn't properly implemented - calling this just clears the previously bound buttons
        public override string[] FunctionNames() => new string[] { "btndef" };

        public override void HandleFunctionNode(TreeWalker walker, FunctionNode function)
        {
            List<Node> arguments = function.GetArguments(1);
            string btndefFilename = walker.TranslateExpression(arguments[0]);

            walker.scriptBuilder.EmitPython($"pons_btndef({btndefFilename})");
        }
    }

    class RenpyScriptBuilder
    {
        enum LineType
        {
            COMMENT,
            PYTHON,
            STATEMENT,
        }

        //Labels: 0 indent
        //Text/Dialogue: 1 indent
        //Python start: 1 indent
        //Python code: 2 or more indent (baseIndent)
        // - if statements increment indent for python statements on the same line in ponscripter (temporaryIndent)
        // - for statements increment indent until reaching the next 'next' statement in ponscripter (permanentIndent)

        StringBuilder init;
        StringBuilder body;
        StringBuilder current; //set to either init or body

        const string tabString = "    ";

        // Indent variables
        //const int baseIndent = 1;      //the base indent for all python code
        int temporaryIndent; //used for if statements (I think this only ever reaches 1 since ponscripter doesn't have proper nested if statements)
        int permanentIndent; //changes only for for loops. Preserved between ponscripter lines

        int pythonLineCount; //used to emit 'pass' statements if 'python:' block has no statements

        bool lastStatementWasPython;

        bool ponscripterDefineSectionMode;

        public RenpyScriptBuilder()
        {
            init = new StringBuilder(1_000_000);
            body = new StringBuilder(10_000_000);
            current = body;
            permanentIndent = 0;
            temporaryIndent = 0;
            lastStatementWasPython = false;
            pythonLineCount = 0;
            ponscripterDefineSectionMode = false;
        }

        public void SaveFile(string preludePath, string outputPath)
        {
            using (StreamWriter writer = File.CreateText(outputPath))
            {
                //write the prelude
                using(StreamReader reader = File.OpenText(preludePath))
                {
                    writer.Write(reader.ReadToEnd());
                }
                writer.Write(init.ToString());
                writer.Write(body.ToString());
            }
        }

        /*public void SetInitRegion()
        {
            this.ponscripterDefineSectionMode = true;
            current = init;
        }
        public void SetBodyRegion()
        {
            this.ponscripterDefineSectionMode = false;
            current = body;
        }*/

        public void ModifyIndentPermanently(int relativeChange)
        {
            permanentIndent += relativeChange;
            if (permanentIndent < 0)
            {
                throw new Exception("Permanent Indent became less than 0 - duplicate 'next' statements?");
            }
        }

        public void ModifyIndentTemporarily(int relativeChange)
        {
            temporaryIndent += relativeChange;
            if(temporaryIndent < 0)
            {
                throw new Exception("Temporary Indent became less than 0");
            }
        }

        public void ResetIndentAtEndOfLine()
        {
            temporaryIndent = 0;
        }

        public void AppendWarning(string warning)
        {
            string message = "WARNING: " + warning;
            Console.WriteLine(message);
            AppendComment(message);
        }

        public void AppendComment(string comment)
        {
            AppendLine("# " + comment, lineType: LineType.COMMENT);
        }

        public void EmitStatement(string line)
        {
            AppendLine(line, lineType: LineType.STATEMENT);
        }

        public void EmitPython(string line)
        {
            pythonLineCount++;
            string emittedLine = (ponscripterDefineSectionMode ? "" : "$ ") + line;
            AppendLine(emittedLine, lineType: LineType.PYTHON);
        }

        //Labels are always emitted with 0 indent
        public void EmitLabel(string line, bool isJumpfLabel)
        {
            int? indent_override = 0;

            if(isJumpfLabel || permanentIndent != 0 || temporaryIndent != 0)
            {
                indent_override = null;
            }

            AppendLine(line, lineType: LineType.STATEMENT, indent_override: indent_override);
            pythonLineCount = 0;
        }

        private void PreEmitHook(bool nextIsPython)
        {
            if(ponscripterDefineSectionMode)
            {
                if (nextIsPython && !lastStatementWasPython)
                {
                    //emit python block when transitioning from non-python to python
                    AppendLine("python:", 1);
                }
                else if (!nextIsPython && lastStatementWasPython && pythonLineCount == 0)
                {
                    //emit pass statement if:
                    // - transitioning from python to non-python
                    // - no python lines have been emitted.
                    // This is required as all python: blocks must contain at least one statement in them
                    AppendLine("pass", 2);
                }

                lastStatementWasPython = nextIsPython;
            }
        }

        private void AppendLine(string line, LineType lineType, int? indent_override=null)
        {
            int indent = GetBaseIndent() + permanentIndent + temporaryIndent;
            if(indent_override.HasValue)
            {
                indent = indent_override.Value;
            }            

            for (int i = 0; i < indent; i++)
            {
                current.Append(tabString);
            }

            current.AppendLine(line);
        }

        private int GetBaseIndent()
        {
            return ponscripterDefineSectionMode ? 2 : 1;
        }
    }

    class TreeWalker
    {
        public FunctionHandlerLookup functionLookup;
        public IgnoreCaseDictionary<int> numAliasDictionary;
        public IgnoreCaseDictionary<int> stringAliasDictionary;
        public RenpyScriptBuilder scriptBuilder;

        //TODO: encapsulate this in a class, this is too confusing.
        public int jumpfTargetCount;
        public bool sawJumpfCommand;
        public bool gotIfStatement;

        //TODO: encapsulate this in a class, this is too confusing.
        public Stack<string> forNextStatementIncrementString;

        public TreeWalker(RenpyScriptBuilder scriptBuilder)
        {
            this.functionLookup = new FunctionHandlerLookup();
            this.numAliasDictionary = new IgnoreCaseDictionary<int>();
            this.stringAliasDictionary = new IgnoreCaseDictionary<int>();
            this.scriptBuilder = scriptBuilder;

            // Variables used for jumpf command
            this.sawJumpfCommand = false;
            this.jumpfTargetCount = 0;

            this.gotIfStatement = false;

            this.forNextStatementIncrementString = new Stack<string>();

            //Register function handlers
            this.functionLookup.RegisterSystemFunction(new NumAliasHandler());
            this.functionLookup.RegisterSystemFunction(new StringAliasHandler());
            this.functionLookup.RegisterSystemFunction(new MovHandler());
            this.functionLookup.RegisterSystemFunction(new AddHandler());
            this.functionLookup.RegisterSystemFunction(new IncHandler());
            this.functionLookup.RegisterSystemFunction(new DecHandler());
            this.functionLookup.RegisterSystemFunction(new DefSubHandler());
            this.functionLookup.RegisterSystemFunction(new GetParamHandler());
            this.functionLookup.RegisterSystemFunction(new JumpfHandler());
            this.functionLookup.RegisterSystemFunction(new GotoHandler());
            this.functionLookup.RegisterSystemFunction(new GoSubHandler());
            this.functionLookup.RegisterSystemFunction(new DimHandler());
            this.functionLookup.RegisterSystemFunction(new NextHandler());
            this.functionLookup.RegisterSystemFunction(new LspHandler());
            this.functionLookup.RegisterSystemFunction(new SpbtnExbtnHandler());
            this.functionLookup.RegisterSystemFunction(new Btnwait2Handler());
            this.functionLookup.RegisterSystemFunction(new BtndefHandler());
        }

        public void WalkOneLine(List<Node> nodes)
        {
            foreach(Node n in nodes)
            {
                if(!HandleNode(n))
                {
                    string warningMessage = $"Node {n.GetLexeme()} is not handled";
                    scriptBuilder.AppendWarning(warningMessage);
                }
            }

            //TODO: for now, all if statements have a "pass" at the end of them
            if(gotIfStatement)
            {
                gotIfStatement = false;

                scriptBuilder.EmitStatement("pass");
            }

            //reset if statement marker upon reaching line end
            scriptBuilder.ResetIndentAtEndOfLine();
        }


        private bool HandleNode(Node n)
        {
            switch(n)
            {
                case DialogueNode dialogueNode:
                    scriptBuilder.EmitStatement($"narrator \"{dialogueNode.GetLexeme().text.Replace('"', ' ')}\"");
                    return true;

                case FunctionNode function:
                    return HandleFunction(function);

                case LabelNode labelNode:
                    //ignore 'start' of ponscripter script, since we want to control the 'start' label ourselves
                    if(labelNode.labelName == "start")
                    {
                        return true;
                    }

                    scriptBuilder.EmitLabel($"label {MangleLabelName(labelNode.labelName)}(*pons_args):", isJumpfLabel: false);
                    return true;

                case IfStatementNode ifNode:
                    string invertIfString = ifNode.isInverted ? "not " : "";
                    string ifCondition = TranslateExpression(ifNode.condition);
                    scriptBuilder.EmitStatement($"if {invertIfString}({ifCondition}):");
                    scriptBuilder.ModifyIndentTemporarily(1);
                    gotIfStatement = true;
                    return true;

                case ForStatementNode forNode:
                    //Translate the for statement to a while statement
                    //Note that Ponscripter for statements are inclusive of both the initial and final value
                    
                    string forVariable = TranslateExpression(forNode.forVariable);
                    string initialValue = TranslateExpression(forNode.startExpression);
                    string finalValue = TranslateExpression(forNode.endExpression);
                    string step = forNode.step == null ? "1" : TranslateExpression(forNode.step);

                    if(!int.TryParse(step, out int stepAsNumber))
                    {
                        throw new NotImplementedException("Non-numeric step not implemented (not sure if ponscripter ever supported it) - while comparison depends on positive/negative step");
                    }

                    //emit the initializer
                    scriptBuilder.EmitPython($"{forVariable} = {initialValue}");

                    //emit the while loop
                    if(stepAsNumber >= 0)
                    {
                        scriptBuilder.EmitStatement($"while {forVariable} <= {finalValue}:");
                    }
                    else
                    {
                        scriptBuilder.EmitStatement($"while {forVariable} >= {finalValue}:");
                    }

                    //Increase indent by 1 for the 'while' loop
                    scriptBuilder.ModifyIndentPermanently(1);

                    //Postpone emitting of the variable update command until the "next" statement is found
                    this.forNextStatementIncrementString.Push($"{forVariable} += {step}");

                    return true;

                case ColonNode colonNode:
                    //for now, just ignore colons
                    return true;

                case CommentNode commentNode:
                    scriptBuilder.AppendComment(commentNode.comment);
                    return true;

                case JumpfTargetNode jumpfTarget:
                    string label_prefix = sawJumpfCommand ?
                        JumpfHandler.GetJumpfLabelNameFromID(jumpfTargetCount) : 
                        JumpfHandler.GetUnreachableJumpfLabelNameFromID(jumpfTargetCount);
                    sawJumpfCommand = false;
                    scriptBuilder.EmitLabel($"label {label_prefix}:", isJumpfLabel: true);
                    jumpfTargetCount += 1;
                    return true;

                case ReturnNode returnNode:
                    if (returnNode.returnDestination != null)
                    {
                        throw new NotImplementedException();
                    }

                    scriptBuilder.EmitStatement("return");

                    return true;
            }

            return false;
        }

        private bool HandleFunction(FunctionNode function)
        {
            if(this.functionLookup.TryGetFunction(function.functionName, out bool _isUserFunction, out FunctionHandler handler))
            {
                handler.HandleFunctionNode(this, function);
                return true;
            }

            return false;
        }

        public void DefineArray(ArrayReferenceNode node)
        {

        }

        public string TranslateExpression(Node node)
        {
            switch(node)
            {
                case StringReferenceNode stringReference:
                    return GenerateStringAlias(TranslateExpression(stringReference.inner));

                case NumericReferenceNode numericReference:
                    return GenerateNumAlias(TranslateExpression(numericReference.inner));

                case BinaryOperatorNode bNode:
                    return $"({TranslateExpression(bNode.left)} {TranslateOperatorForRenpy(bNode.op.text)} {TranslateExpression(bNode.right)})";

                case UnaryNode uNode:
                    return $"{uNode.op}{TranslateExpression(uNode.inner)}";

                case AliasNode aNode:
                    string aliasName = aNode.aliasName;
                    return MangleAlias(aliasName);
                    /*
                    if (numAliasDictionary.Contains(aliasName))
                    {
                        return MangleNumalias(aliasName);
                    }
                    else if(numAliasDictionary.Contains(aliasName))
                    {
                        return MangleStralias(aliasName);
                    }
                    else
                    {
                        throw new Exception($"alias '{aliasName}' was used before it was defined");
                    }*/

                case ArrayReferenceNode arrayNode:
                    return TranslateArray(this, arrayNode);

                //TODO: could implement type checking for string/numeric types, but should do as part of a seprate process
                case StringLiteral stringLiteral:
                    //TODO: move this logic to the StringLiteral class
                    string stringWithQuotes = stringLiteral.value;
                    if (stringWithQuotes[0] != '"' || stringWithQuotes[stringWithQuotes.Length - 1] != '"')
                    {
                        throw new Exception("Invalid string literal");
                    }

                    return EscapeStringForPython(stringWithQuotes.Substring(1, stringWithQuotes.Length - 2));

                case NumericLiteral numericLiteral:
                    return numericLiteral.valueAsString;

                case LabelNode _:
                    //No user defined functions should take labels as arguments
                    //If a system function takes a label as argument, it should be handled separately, not here
                    throw new NotImplementedException();

                default:
                    throw new Exception($"Resolve reference couldn't handle node {node}");
            }
        }

        public string GenerateNumAlias(string lookupValue)
        {
            return $"pons_var[{lookupValue}]";
        }

        public string GenerateStringAlias(string lookupValue)
        {
            return $"pons_str[{lookupValue}]";
        }

        public static string MangleArrayName(string arrayName)
        {
            return "pons_arr_" + arrayName;
        }

        private string EscapeStringForPython(string s)
        {
            return $"r\"{s.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
        }

        private string TranslateOperatorForRenpy(string op)
        {
            switch (op)
            {
                case "<>":
                    return "!=";

                case "=":
                    return "==";

                case "&":
                case "&&":
                    return "and";
            }

            return op;
        }

        //For now, don't mangle label names at all
        public static string MangleLabelName(string labelName)
        {
            return $"{labelName}";
        }

        //public static string MangleNumalias(string aliasName) => $"numalias_{aliasName}";
        //public static string MangleStralias(string aliasName) => $"stralias_{aliasName}";
        public static string MangleAlias(string aliasName) => "alias_" + aliasName;
            
        public static void HandleAliasNode(TreeWalker walker, FunctionNode function, bool isNumAlias)
        {
            List<Node> arguments = function.GetArguments(2);

            string aliasName = FunctionHandler.VerifyType<AliasNode>(arguments[0]).aliasName;

            string aliasValue = walker.TranslateExpression(arguments[1]);

            Log.Information($"Received numalias {aliasName} = {aliasValue}");

            //string mangledAliasName = isNumAlias ? MangleNumalias(aliasName) : MangleStralias(aliasName);
            string mangledAliasName = MangleAlias(aliasName);
            walker.scriptBuilder.EmitPython($"{mangledAliasName} = {aliasValue}");

            if(isNumAlias)
            {
                walker.numAliasDictionary.Set(aliasName, 0);
            }
            else
            {
                walker.stringAliasDictionary.Set(aliasName, 0);
            }

        }

        public string TranslateArray(TreeWalker walker, ArrayReferenceNode arrayReference)
        {
            StringBuilder tempBuilder = new StringBuilder();

            tempBuilder.Append($"{MangleArrayName(arrayReference.arrayName)}");
            foreach (Node bracketedExpression in arrayReference.nodes)
            {
                tempBuilder.Append($"[{walker.TranslateExpression(bracketedExpression)}]");
            }

            return tempBuilder.ToString();
        }

    }
}
