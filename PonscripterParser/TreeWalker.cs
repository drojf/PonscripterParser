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
        public abstract string FunctionName();
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
            if (dict.Contains(userFunctionHandler.FunctionName()))
            {
                throw new Exception($"User function {userFunctionHandler.FunctionName()} was defined twice");
            }
            else
            {
                dict.Set(userFunctionHandler.FunctionName(), userFunctionHandler);
            }
        }
    }

    class UserFunctionHandler : FunctionHandler
    {
        public override string FunctionName() { throw new NotSupportedException(); }

        public override void HandleFunctionNode(TreeWalker walker, FunctionNode function)
        {
            StringBuilder tempBuilder = new StringBuilder();
            List<Node> arguments = function.GetArguments();
            int argCount = arguments.Count;

            //add first argument, the 'label' to call ('function' name)
            tempBuilder.Append($"renpy.call({function.lexeme.text.Quote()}");

            //append the function arguments, if any
            for (int i = 0; i < argCount; i++)
            {
                tempBuilder.Append(", ");
                tempBuilder.Append(walker.TranslateExpression(arguments[i]));
            }

            //add closing bracket
            tempBuilder.Append(")");

            walker.scriptBuilder.EmitPython(tempBuilder.ToString());
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
        public override string FunctionName() => "inc";

        public override void HandleFunctionNode(TreeWalker walker, FunctionNode function)
        {
            List<Node> arguments = function.GetArguments(1);
            string lvalue = walker.TranslateExpression(arguments[0]);
            walker.scriptBuilder.EmitPython($"{lvalue} += 1");
        }
    }

    class DecHandler : FunctionHandler
    {
        public override string FunctionName() => "dec";

        public override void HandleFunctionNode(TreeWalker walker, FunctionNode function)
        {
            List<Node> arguments = function.GetArguments(1);
            string lvalue = walker.TranslateExpression(arguments[0]);
            walker.scriptBuilder.EmitPython($"{lvalue} -= 1");
        }
    }

    class AddHandler : BinaryOpFunction
    {
        public override string FunctionName() => "add";
        
        public override string Op() => "+=";
    }

    class MovHandler : BinaryOpFunction
    {
        public override string FunctionName() => "mov";

        public override string Op() => "=";
    }

    abstract class AliasHandler : FunctionHandler
    {
        public override void HandleFunctionNode(TreeWalker walker, FunctionNode function)
        {
            List<Node> arguments = function.GetArguments(2);

            //Force lowercase, as the game treats all keywords as case-insensitive
            string aliasName = arguments[0].lexeme.text.ToLower();

            string aliasValue = walker.TranslateExpression(arguments[1]);

            Log.Information($"Received numalias {aliasName} = {aliasValue}");
            walker.scriptBuilder.EmitPython($"{FunctionName()}_{aliasName} = {aliasValue}");
        }
    }

    class StringAliasHandler : AliasHandler
    {
        public override string FunctionName() => "stralias";
    }

    class NumAliasHandler : AliasHandler
    {
        public override string FunctionName() => "numalias";
    }

    class DefSubHandler : FunctionHandler
    {
        public override string FunctionName() => "defsub";

        public override void HandleFunctionNode(TreeWalker walker, FunctionNode function)
        {
            List<Node> arguments = function.GetArguments(1);
            walker.functionLookup.RegisterUserFunction(arguments[0].lexeme.text);
        }
    }

    class JumpfHandler : FunctionHandler
    {
        //TODO: refactor handling of jumpf/~ - some logic defined here, and some logic is defined in top level handler
        //TODO: Renpy supports local label names (prefix with '.'), however this
        // may break some ponscripter scripts which (erroneously?) jumpf through subroutines
        // For now, leave as global labels.
        public override string FunctionName() => "jumpf";

        public override void HandleFunctionNode(TreeWalker walker, FunctionNode function)
        {
            walker.sawJumpfCommand = true;
            walker.scriptBuilder.EmitPython($"renpy.jump({GetJumpfLabelNameFromID(walker.jumpfTargetCount).Quote()})");
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
        public override string FunctionName() => "getparam";

        public override void HandleFunctionNode(TreeWalker walker, FunctionNode function)
        {
            for(int i = 0; i < function.GetArguments().Count; i++)
            {
                string s = walker.TranslateExpression(function.GetArguments()[i]);
                walker.scriptBuilder.EmitPython($"{s} = args[{i}]");
            }           
        }
    }

    class GotoHandler : FunctionHandler
    {
        public override string FunctionName() => "goto";

        public override void HandleFunctionNode(TreeWalker walker, FunctionNode function)
        {
            List<Node> arguments = function.GetArguments(1);
            LabelNode labelNode = VerifyType<LabelNode>(arguments[0]);

            walker.scriptBuilder.EmitPython($"renpy.jump({TreeWalker.MangleLabelName(labelNode.labelName).Quote()})");
        }
    }

    class RenpyScriptBuilder
    {
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
        const int baseIndent = 2;      //the base indent for all python code
        int temporaryIndent; //used for if statements (I think this only ever reaches 1 since ponscripter doesn't have proper nested if statements)
        int permanentIndent; //changes only for for loops. Preserved between ponscripter lines

        int pythonLineCount; //used to emit 'pass' statements if 'python:' block has no statements

        bool lastStatementWasPython;

        public RenpyScriptBuilder()
        {
            init = new StringBuilder(1_000_000);
            body = new StringBuilder(10_000_000);
            current = init;
            permanentIndent = 0;
            temporaryIndent = 0;
            lastStatementWasPython = false;
            pythonLineCount = 0;
        }

        public void SaveFile(string outputPath)
        {
            using (StreamWriter writer = File.CreateText(outputPath))
            {
                writer.Write(init.ToString());
                writer.Write(body.ToString());
            }
        }

        public void SetInitRegion()
        {
            current = init;
        }
        public void SetBodyRegion()
        {
            current = body;
        }

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

        public void AppendComment(string comment)
        {
            AppendLine("# " + comment, baseIndent + permanentIndent + temporaryIndent);
        }

        public void EmitPython(string line)
        {
            pythonLineCount++;
            PreEmitHook(nextIsPython: true);
            AppendLine(line, baseIndent + permanentIndent + temporaryIndent);
        }

        //Labels are always emitted with 0 indent
        public void EmitLabel(string line)
        {
            pythonLineCount = 0;
            PreEmitHook(nextIsPython: false);
            AppendLine(line, 0);
        }

        private void PreEmitHook(bool nextIsPython)
        {
            if(nextIsPython && !lastStatementWasPython)
            {
                //emit python block when transitioning from non-python to python
                AppendLine("python:", 1);
            }
            else if(!nextIsPython && lastStatementWasPython && pythonLineCount == 0)
            {
                //emit pass statement if:
                // - transitioning from python to non-python
                // - no python lines have been emitted.
                // This is required as all python: blocks must contain at least one statement in them
                AppendLine("pass", 2);
            }

            lastStatementWasPython = nextIsPython;
        }

        private void AppendLine(string line, int indent)
        {
            for (int i = 0; i < indent; i++)
            {
                current.Append(tabString);
            }

            current.AppendLine(line);
        }
    }

    class TreeWalker
    {
        public FunctionHandlerLookup functionLookup;
        public IgnoreCaseDictionary<int> numAliasDictionary;
        public IgnoreCaseDictionary<string> stringAliasDictionary;
        public RenpyScriptBuilder scriptBuilder;

        //TODO: encapsulate this in a class, this is too confusing.
        public int jumpfTargetCount;
        public bool sawJumpfCommand;

        public TreeWalker(RenpyScriptBuilder scriptBuilder)
        {
            this.functionLookup = new FunctionHandlerLookup();
            this.numAliasDictionary = new IgnoreCaseDictionary<int>();
            this.stringAliasDictionary = new IgnoreCaseDictionary<string>();
            this.scriptBuilder = scriptBuilder;

            // Variables used for jumpf command
            this.sawJumpfCommand = false;
            this.jumpfTargetCount = 0;

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
        }

        public void WalkOneLine(List<Node> nodes)
        {
            foreach(Node n in nodes)
            {
                if(!HandleNode(n))
                {
                    string warningMessage = $"Warning: Node {n}:{n.lexeme.text} is not handled";
                    Console.WriteLine(warningMessage);
                    scriptBuilder.AppendComment(warningMessage);
                }
            }

            //reset if statement marker upon reaching line end
            scriptBuilder.ResetIndentAtEndOfLine();
        }


        private bool HandleNode(Node n)
        {
            switch(n)
            {
                case DialogueNode dialogue:
                    Console.WriteLine($"Display Text: {dialogue.lexeme.text}");
                    return true;

                case FunctionNode function:
                    return HandleFunction(function);

                case LabelNode labelNode:
                    //Labels should be emitted with zero indent - normal calls emitted with indent 1 (which are in the start: section)
                    string labelName = labelNode.lexeme.text.TrimStart(new char[] { '*' });
                    scriptBuilder.EmitLabel($"label {MangleLabelName(labelName)}(*args):");
                    return true;

                case IfStatementNode ifNode:
                    string invertIfString = ifNode.isInverted ? "!" : "";
                    string ifCondition = TranslateExpression(ifNode.condition);
                    scriptBuilder.EmitPython($"if {invertIfString}({ifCondition}):");
                    scriptBuilder.ModifyIndentTemporarily(1);
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
                    scriptBuilder.EmitLabel($"label {label_prefix}:");
                    jumpfTargetCount += 1;
                    return true;

                case ReturnNode returnNode:
                    if (returnNode.returnDestination != null)
                    {
                        throw new NotImplementedException();
                    }

                    scriptBuilder.EmitPython("renpy.return_statement()");

                    return true;
            }

            return false;
        }

        private bool HandleFunction(FunctionNode function)
        {
            if(this.functionLookup.TryGetFunction(function.lexeme.text, out bool _isUserFunction, out FunctionHandler handler))
            {
                handler.HandleFunctionNode(this, function);
                return true;
            }

            return false;
        }

        public void DefineArray(ArrayReference node)
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
                    return $"{uNode.lexeme.text}{TranslateExpression(uNode.inner)}";

                case AliasNode aNode:
                    return aNode.lexeme.text;

                case ArrayReference arrayNode:
                    //TODO: each dim'd array should use a custom python object which handles if read/written value is out of range without crashing
                    StringBuilder sb = new StringBuilder();
                    sb.Append(MangleArrayName(arrayNode.arrayName.text));
                    foreach(Node bracketedNode in arrayNode.nodes)
                    {
                        sb.Append($"[{TranslateExpression(bracketedNode)}]");
                    }
                    return sb.ToString();

                //TODO: could implement type checking for string/numeric types, but should do as part of a seprate process
                case StringLiteral stringLiteral:
                    string stringWithQuotes = stringLiteral.lexeme.text;
                    if (stringWithQuotes[0] != '"' || stringWithQuotes[stringWithQuotes.Length - 1] != '"')
                    {
                        throw new Exception("Invalid string literal");
                    }

                    return EscapeStringForPython(stringWithQuotes.Substring(1, stringWithQuotes.Length - 2));

                case NumericLiteral numericLiteral:
                    return numericLiteral.lexeme.text;

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
            return $"variable_array[{lookupValue}]";
        }

        public string GenerateStringAlias(string lookupValue)
        {
            return $"string_array[{lookupValue}]";
        }

        public string MangleArrayName(string arrayName)
        {
            return "pons_array_" + arrayName;
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
                    return "&&";
            }

            return op;
        }

        //For now, don't mangle label names at all
        public static string MangleLabelName(string labelName)
        {
            return $"{labelName}";
        }
    }
}
