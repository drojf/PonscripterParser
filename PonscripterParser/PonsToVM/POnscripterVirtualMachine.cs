using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PonscripterParser.PonsToVM
{
    interface GameDisplayInterface
    {
        void EmitDialogue(string dialogueString);
    }

    interface TreeHandler
    {
        bool HandleNode(Node n);

    }

    class VirtualMachineDriver : TreeHandler
    {
        GameDisplayInterface vm;
        ScriptPositionManager script;
        FunctionHandlerLookup3 userDefinedFunctionLookup;

        public VirtualMachineDriver(List<List<Node>> scriptNodes, GameDisplayInterface vm)
        {
            this.vm = vm;
            this.script = new ScriptPositionManager(scriptNodes);
            this.userDefinedFunctionLookup = new FunctionHandlerLookup3();
        }

        public bool Handle()
        {
            //Check if the current line is finished
            if(script.LineFinished())
            {
                //Jump to next valid line, and check if finished processing script
                bool scriptFinished = script.JumpNextValidLine();
                if(scriptFinished)
                {
                    return false;
                }
            }

            //Handle the next instruction
            HandleNode(script.ConsumeInstructionOnLine());
            return true;
        }

        public bool HandleNode(Node n)
        {
            switch(n)
            {
                case DialogueNode dialogueNode:
                    vm.EmitDialogue(dialogueNode.ToString());
                    return true;

                case FunctionNode function:
                    return HandleFunction(function);
            }

            return false;
        }

        /// <summary>
        /// To handle a function, first query the VM 
        /// </summary>
        /// <param name="n"></param>
        /// <returns>false if the function was unhandled, true otherwise</returns>
        public bool HandleFunction(FunctionNode n)
        {
            //1) check for calling the base version of an overriden function
            if(n.functionName.StartsWith("_"))
            {
                if(HandlePredefinedFunction(n, true))
                {
                    return true;
                }
                else
                {
                    //for now assume you can't define a user defined function starting with an underscore
                    throw new Exception($"Base function not found for function '{n.functionName}'");
                }
            }

            //2) check if a user defined function exists (query class "FunctionHandlerLookup2")
            //if it does, jump to it
            if(userDefinedFunctionLookup.TryGetFunction(n.functionName, out int functionLineNumber))
            {
                script.Jump(functionLineNumber);
                return true;
            }

            //3) check if a predefined function exists
            if(HandlePredefinedFunction(n, false))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns false if the function was unhandled
        /// </summary>
        /// <param name="n"></param>
        /// <returns></returns>
        public bool HandlePredefinedFunction(FunctionNode n, bool isBaseFunction)
        {
            string functionName = isBaseFunction ? n.functionName.Substring(1) : n.functionName;

            switch(functionName)
            {
                case "defsub":
                    List<Node> arguments = n.GetArguments(1);
                    userDefinedFunctionLookup.DefineUserFunction(VerifyType<AliasNode>(arguments[0]).aliasName);
                    return true;

                
            }

            return false;
        }

        public static T VerifyType<T>(Node n)
        {
            switch (n)
            {
                case T node:
                    return node;
            }

            throw new Exception($"Expected type {typeof(T)}, got {n.GetType()}");
        }
    }



    interface TreeHandler
    {
        

        bool HandleNode(Node n)
        {
            switch (n)
            {
                case DialogueNode dialogueNode:
                    
                    scriptBuilder.EmitStatement($"narrator \"{dialogueNode.GetLexeme().text.Replace('"', ' ')}\"");
                    return true;

                case FunctionNode function:
                    return HandleFunction(function);

                case LabelNode labelNode:
                    //ignore 'start' of ponscripter script, since we want to control the 'start' label ourselves
                    if (labelNode.labelName == "start")
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

                    if (!int.TryParse(step, out int stepAsNumber))
                    {
                        throw new NotImplementedException("Non-numeric step not implemented (not sure if ponscripter ever supported it) - while comparison depends on positive/negative step");
                    }

                    //emit the initializer
                    scriptBuilder.EmitPython($"{forVariable} = {initialValue}");

                    //emit the while loop
                    if (stepAsNumber >= 0)
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
    }

    class IntepreterHandler : TreeHandler
    {
        
        private bool HandleFunction(FunctionNode function)
        {
            if (this.functionLookup.TryGetFunction(function.functionName, out bool _isUserFunction, out FunctionHandler handler))
            {
                handler.HandleFunctionNode(this, function);
                return true;
            }

            return false;
        }

        public string TranslateExpression(Node node)
        {
            switch (node)
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

            if (isNumAlias)
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

    abstract class FunctionHandler2
    {
        public abstract string[] FunctionNames();
        public abstract void HandleFunctionNode(POnscripterVirtualMachine virtualMachine, RenpyScriptBuilder scriptBuilder);

        public static T VerifyType<T>(Node n)
        {
            switch (n)
            {
                case T node:
                    return node;
            }

            throw new Exception($"Expected type {typeof(T)}, got {n.GetType()}");
        }
    }


    class UserFunctionHandler2 : FunctionHandler2
    {
        public override string[] FunctionNames() { throw new NotSupportedException(); }

        public override void HandleFunctionNode(FunctionNode function, POnscripterVirtualMachine virtualMachine, RenpyScriptBuilder scriptBuilder)
        {
            //virtualMachine.callFunction(function.functionName)

            throw new NotImplementedException();

            StringBuilder tempBuilder = new StringBuilder();
            List<Node> arguments = function.GetArguments();
            int argCount = arguments.Count;

            tempBuilder.Append($"call {function.functionName}");

            //add first argument, the 'label' to call ('function' name)
            if (argCount > 0)
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


    class FunctionHandlerLookup3
    {
        // This dictionary is just used as a set
        public IgnoreCaseDictionary<bool> functionDefs;
        public IgnoreCaseDictionary<int> userFunctions;

        public FunctionHandlerLookup3()
        {
            this.functionDefs = new IgnoreCaseDictionary<bool>();
            this.userFunctions = new IgnoreCaseDictionary<int>();
        }

        /// <summary>
        /// Given a function's name, try to get the line number of the function
        /// </summary>
        /// <param name="functionName"></param>
        /// <returns></returns>
        public bool TryGetFunction(string functionName, out int value)
        {
            if(userFunctions.TryGetValue(functionName, out value))
            {
                return true;
            }

            return false;
        }

        public void DefineUserFunction(string functionName)
        {
            functionDefs.Set(functionName, true);
        }

        /// <summary>
        /// Register a function named functionName which starts at functionLineNumber
        /// </summary>
        /// <param name="labelName"></param>
        /// <param name="labelLineNumber"></param>
        /// <returns>true if the label was a function, false if it's a regular label</returns>
        /// <exception>Raises an Exception if a function is defined twice</exception>
        public bool TryResolveUserFunctionFromLabel(string labelName, int labelLineNumber)
        {
            if(!functionDefs.Contains(labelName))
            {
                return false;
            }

            if(userFunctions.Contains(labelName))
            {
                throw new Exception($"User function {labelName} was defined twice");
            }

            userFunctions.Set(labelName, labelLineNumber);
            return true;
        }
    }

    class FunctionHandlerLookup2
    {
        IgnoreCaseDictionary<FunctionHandler2> systemFunctions;
        public IgnoreCaseDictionary<int> userFunctions;

        public FunctionHandlerLookup2()
        {
            this.systemFunctions = new IgnoreCaseDictionary<FunctionHandler2>();
            this.userFunctions = new IgnoreCaseDictionary<int>();
        }

        public bool TryGetFunction(string functionName, out bool isUserFunction, out FunctionHandler2 retHandler)
        {
            //First, check user functions to see if function with name exists
            if (userFunctions.Contains(functionName))
            {
                retHandler = new UserFunctionHandler2();
                isUserFunction = true;
                return true;
            }

            //Then, check system functions to see if function exists
            if (this.systemFunctions.TryGetValue(functionName, out FunctionHandler2 systemFunction))
            {
                retHandler = systemFunction;
                isUserFunction = false;
                return true;
            }

            //Finally, check if function has been overridden, if it has a '_' at the front ('_' prefixed function
            if (functionName.StartsWith("_") &&
                this.systemFunctions.TryGetValue(functionName.Substring(1), out FunctionHandler2 overridenFunction))
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

        public void RegisterSystemFunction(FunctionHandler2 systemFunctionHandler)
        {
            RegisterFunctionWithCheck(systemFunctions, systemFunctionHandler);
        }

        private void RegisterFunctionWithCheck(IgnoreCaseDictionary<FunctionHandler2> dict, FunctionHandler2 userFunctionHandler)
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

    class TreeWalker2
    {
        public RenpyScriptBuilder scriptBuilder;
        public FunctionHandlerLookup2 functionLookup;

        public TreeWalker2(RenpyScriptBuilder scriptBuilder)
        {
            this.scriptBuilder = scriptBuilder;
            this.functionLookup = new FunctionHandlerLookup2();
        }

        public void WalkOneLine(List<Node> nodes)
        {
            foreach (Node n in nodes)
            {
                if (!HandleNode(n))
                {
                    string warningMessage = $"Node {n.GetLexeme()} is not handled";
                    scriptBuilder.AppendWarning(warningMessage);
                }
            }
        }

        private bool HandleFunction(FunctionNode function)
        {
            if (this.functionLookup.TryGetFunction(function.functionName, out bool _isUserFunction, out FunctionHandler2 handler))
            {
                handler.HandleFunctionNode(this, function);
                return true;
            }

            return false;
        }

        bool HandleNode(Node n)
        {
            switch (n)
            {
                case DialogueNode dialogueNode:
                    scriptBuilder.EmitStatement($"narrator \"{dialogueNode.GetLexeme().text.Replace('"', ' ')}\"");
                    return true;

                case FunctionNode function:
                    return HandleFunction(function);

                case LabelNode labelNode:
                    //ignore 'start' of ponscripter script, since we want to control the 'start' label ourselves
                    if (labelNode.labelName == "start")
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

                    if (!int.TryParse(step, out int stepAsNumber))
                    {
                        throw new NotImplementedException("Non-numeric step not implemented (not sure if ponscripter ever supported it) - while comparison depends on positive/negative step");
                    }

                    //emit the initializer
                    scriptBuilder.EmitPython($"{forVariable} = {initialValue}");

                    //emit the while loop
                    if (stepAsNumber >= 0)
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


    }
}
