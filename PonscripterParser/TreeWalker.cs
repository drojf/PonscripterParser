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

    class StringAliasHandler : FunctionHandler
    {
        public override string FunctionName() => "stralias";

        public override void HandleFunctionNode(TreeWalker walker, FunctionNode function)
        {
            TreeWalker.HandleAliasNode(walker, function, isNumAlias: false);
        }
    }

    class NumAliasHandler : FunctionHandler
    {
        public override string FunctionName() => "numalias";

        public override void HandleFunctionNode(TreeWalker walker, FunctionNode function)
        {
            TreeWalker.HandleAliasNode(walker, function, isNumAlias:true);
        }
    }

    class DefSubHandler : FunctionHandler
    {
        public override string FunctionName() => "defsub";

        public override void HandleFunctionNode(TreeWalker walker, FunctionNode function)
        {
            List<Node> arguments = function.GetArguments(1);
            walker.functionLookup.RegisterUserFunction(VerifyType<AliasNode>(arguments[0]).aliasName);
        }
    }

    class GoSubHandler : FunctionHandler
    {
        public override string FunctionName() => "gosub";

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
        public override string FunctionName() => "jumpf";

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
        public override string FunctionName() => "getparam";

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
        public override string FunctionName() => "goto";

        public override void HandleFunctionNode(TreeWalker walker, FunctionNode function)
        {
            List<Node> arguments = function.GetArguments(1);
            LabelNode labelNode = VerifyType<LabelNode>(arguments[0]);

            walker.scriptBuilder.EmitStatement($"jump {TreeWalker.MangleLabelName(labelNode.labelName)}");
        }
    }

    class DimHandler : FunctionHandler
    {
        public override string FunctionName() => "dim";

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
        public override string FunctionName() => "next";

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
            AppendLine("# " + comment, GetBaseIndent() + permanentIndent + temporaryIndent);
        }

        public void EmitStatement(string line)
        {
            AppendLine(line, GetBaseIndent() + permanentIndent + temporaryIndent);
        }

        public void EmitPython(string line)
        {
            pythonLineCount++;
            PreEmitHook(nextIsPython: true);
            string emittedLine = (ponscripterDefineSectionMode ? "" : "$ ") + line;
            AppendLine(emittedLine, GetBaseIndent() + permanentIndent + temporaryIndent);
        }

        //Labels are always emitted with 0 indent
        public void EmitLabel(string line, bool isJumpfLabel)
        {
            int indent = 0;

            if (isJumpfLabel || permanentIndent != 0 || temporaryIndent != 0)
            {
                indent = GetBaseIndent() + permanentIndent + temporaryIndent;
            }

            PreEmitHook(nextIsPython: false);
            AppendLine(line, indent);
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

        private void AppendLine(string line, int indent)
        {
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
