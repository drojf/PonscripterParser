//Please see UncleMion's POnscripter Documentation here: https://www.drojf.com/nscripter/NScripter_API_Reference.html (mirror of website as original was down)

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PonscripterParser
{
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
            return this.innerDictionary.ContainsKey(key);
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
            StringBuilder bodyBuilder = walker.scriptBuilder.body;
            List<Node> arguments = function.GetArguments();
            int argCount = arguments.Count;
            
            bodyBuilder.Append($"call {function.lexeme.text}");

            if(argCount > 0)
            {
                bodyBuilder.Append("(");

                //append the first argument
                bodyBuilder.Append(walker.TranslateExpression(arguments[0]));

                //append subsequent arguments
                for (int i = 1; i < argCount; i++)
                {
                    bodyBuilder.Append(", ");
                    bodyBuilder.Append(walker.TranslateExpression(arguments[i]));
                }

                bodyBuilder.Append(")");
            }

            bodyBuilder.AppendLine();
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
            walker.scriptBuilder.body.AppendLine($"{lvalue} {Op()} {assigned_value}");
        }
    }
    class IncHandler : FunctionHandler
    {
        public override string FunctionName() => "inc";

        public override void HandleFunctionNode(TreeWalker walker, FunctionNode function)
        {
            List<Node> arguments = function.GetArguments(1);
            string lvalue = walker.TranslateExpression(arguments[0]);
            walker.scriptBuilder.body.AppendLine($"{lvalue} += 1");
        }
    }

    class DecHandler : FunctionHandler
    {
        public override string FunctionName() => "dec";

        public override void HandleFunctionNode(TreeWalker walker, FunctionNode function)
        {
            List<Node> arguments = function.GetArguments(1);
            string lvalue = walker.TranslateExpression(arguments[0]);
            walker.scriptBuilder.body.AppendLine($"{lvalue} -= 1");
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
            walker.scriptBuilder.body.AppendLine($"{FunctionName()}_{aliasName} = {aliasValue}");
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

    class RenpyScriptBuilder
    {
        public StringBuilder init;
        public StringBuilder body;

        public RenpyScriptBuilder()
        {
            init = new StringBuilder(1_000_000);
            body = new StringBuilder(10_000_000);
        }

        public void SaveFile(string outputPath)
        {
            using (StreamWriter writer = File.CreateText(outputPath))
            {
                writer.Write(init.ToString());
                writer.Write(body.ToString());
            }
        }
    }

    class TreeWalker
    {
        public FunctionHandlerLookup functionLookup;
        public IgnoreCaseDictionary<int> numAliasDictionary;
        public IgnoreCaseDictionary<string> stringAliasDictionary;
        public RenpyScriptBuilder scriptBuilder;



        public TreeWalker(RenpyScriptBuilder scriptBuilder)
        {
            this.functionLookup = new FunctionHandlerLookup();
            this.numAliasDictionary = new IgnoreCaseDictionary<int>();
            this.stringAliasDictionary = new IgnoreCaseDictionary<string>();
            this.scriptBuilder = scriptBuilder;

            //Register function handlers
            this.functionLookup.RegisterSystemFunction(new NumAliasHandler());
            this.functionLookup.RegisterSystemFunction(new StringAliasHandler());
            this.functionLookup.RegisterSystemFunction(new MovHandler());
            this.functionLookup.RegisterSystemFunction(new AddHandler());
            this.functionLookup.RegisterSystemFunction(new IncHandler());
            this.functionLookup.RegisterSystemFunction(new DecHandler());
            this.functionLookup.RegisterSystemFunction(new DefSubHandler());
        }

        public void Walk(List<Node> nodes)
        {
            foreach(Node n in nodes)
            {
                if(!HandleNode(n))
                {
                    Console.WriteLine($"Warning: Node {n}:{n.lexeme.text} is not handled");
                }
            }
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
                    return $"({bNode.left} {TranslateOperatorForRenpy(bNode.op.text)} {bNode.right})";

                case UnaryNode uNode:
                    return $"{uNode.lexeme.text}{TranslateExpression(uNode.inner)}";

                case AliasNode aNode:
                    return aNode.lexeme.text;

                case ArrayReference arrayNode:
                    StringBuilder sb = new StringBuilder();
                    sb.Append(MangleArrayName(arrayNode.arrayName.text));
                    foreach(Node bracketedNode in arrayNode.nodes)
                    {
                        sb.Append($"[{TranslateExpression(bracketedNode)}]");
                    }
                    return sb.ToString();

                //TODO: could implement type checking for string/numeric types, but should do as part of a seprate process
                case StringLiteral stringLiteral:
                    return $"r\"{stringLiteral.lexeme.text.Trim(new char[] { '"' })}\"";

                case NumericLiteral numericLiteral:
                    return numericLiteral.lexeme.text;

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
    }
}
