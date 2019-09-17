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
        IgnoreCaseDictionary<FunctionHandler> userFunctions;

        public FunctionHandlerLookup()
        {
            this.systemFunctions = new IgnoreCaseDictionary<FunctionHandler>();
            this.userFunctions = new IgnoreCaseDictionary<FunctionHandler>();

            //Register predefined functions here?
        }

        public bool TryGetFunction(string functionName, out FunctionHandler retHandler)
        {
            //First, check user functions to see if function with name exists
            if(this.userFunctions.TryGetValue(functionName, out FunctionHandler userFunction))
            {
                retHandler = userFunction;
                return true;
            }

            //Then, check system functions to see if function exists
            if (this.systemFunctions.TryGetValue(functionName, out FunctionHandler systemFunction))
            {
                retHandler = systemFunction;
                return true;
            }

            //Finally, check if function has been overridden, if it has a '_' at the front ('_' prefixed function
            if(functionName.StartsWith("_") && 
                this.systemFunctions.TryGetValue(functionName.Substring(1), out FunctionHandler overridenFunction))
            {
                retHandler = overridenFunction;
                return true;
            }

            retHandler = null;
            return false;
        }

        public void RegisterUserFunction(FunctionHandler userFunctionHandler)
        {
            RegisterFunctionWithCheck(userFunctions, userFunctionHandler);
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


    abstract class AliasHandler : FunctionHandler
    {
        public override void HandleFunctionNode(TreeWalker walker, FunctionNode function)
        {
            //Force lowercase, as the game treats all keywords as case-insensitive
            string aliasName = function.arguments[0].lexeme.text.ToLower();

            string aliasValue = walker.TranslateExpression(function.arguments[1]);

            Log.Information($"Received numalias {aliasName} = {aliasValue}");
            walker.scriptBuilder.body.AppendLine($"{FunctionName()}_{aliasName} = {aliasValue}");

            //walker.numAliasDictionary.Set(aliasName, aliasValue);
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


    /*class VariableHandler
    {
        //handle string ($) variable, and numeric (%) variables

        //"resolve variable" function

        void ResolveNumericReference()
        {
            //?? how should this work?
            //an interpreter would resolve this to a number
            //need to figure out what this should resolve to for a cross-compiler.
            //maybe doesn't resolve at all
            //Probably best to work through some examples on paper on how it should be cross-compiled, or an intermediate representation.
        }

    }*/
    class RenpyScriptBuilder
    {
        public StringBuilder init;
        public StringBuilder body;
        //public StreamWriter init;
        //public StreamWriter body;

        public RenpyScriptBuilder()
        {
            init = new StringBuilder(1_000_000);
            body = new StringBuilder(10_000_000);
            //init = new StreamWriter(new MemoryStream(1_000_000));
            //body = new StreamWriter(new MemoryStream(10_000_000));
        }

        public void SaveFile(string outputPath)
        {
            //using(Stream writer = File.OpenWrite(outputPath))
            using (StreamWriter writer = File.CreateText(outputPath))
            {

                //init.BaseStream.CopyTo(writer);
                //body.BaseStream.CopyTo(writer);
                //writer.Flush();
                writer.Write(init.ToString());
                writer.Write(body.ToString());
            }
        }

        /*readonly StreamWriter writer;
        readonly string outputPath;

        public RenpyScriptBuilder(string outputPath)
        {
            this.outputPath = outputPath;
            writer = File.CreateText(outputPath);
            //Write init section here?
        }

        public void Dispose()
        {
            if (writer != null)
            {
                writer.Close();
            }
        }

        public void WriteLine(string line)
        {
            writer.WriteLine(line);
        }*/
    }

    class TreeWalker
    {
        List<Node> nodes;
        FunctionHandlerLookup functionLookup;
        public IgnoreCaseDictionary<int> numAliasDictionary;
        public IgnoreCaseDictionary<string> stringAliasDictionary;
        public RenpyScriptBuilder scriptBuilder;

        public TreeWalker(List<Node> nodes, RenpyScriptBuilder scriptBuilder)
        {
            this.nodes = nodes;

            this.functionLookup = new FunctionHandlerLookup();
            this.numAliasDictionary = new IgnoreCaseDictionary<int>();
            this.stringAliasDictionary = new IgnoreCaseDictionary<string>();
            this.scriptBuilder = scriptBuilder;

            //Register function handlers
            this.functionLookup.RegisterSystemFunction(new NumAliasHandler());
            this.functionLookup.RegisterSystemFunction(new StringAliasHandler());


            //switch(function.lexeme.text)
            //{
            //    case "numalias":
            //        break;

            //    case "stralias":
            //        break;

            //    case "defsub":
            //        break;

            //    case "mov":
            //        break;

            //    case "gosub":
            //        break;

            //    case "goto":
            //        break;

            //    case "bg":
            //        break;
            //}
        }

        public void Walk()
        {
            foreach(Node n in nodes)
            {
                if(!HandleNode(n))
                {
                    Console.WriteLine($"Warning: Node {n} is not handled");
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
            if(this.functionLookup.TryGetFunction(function.lexeme.text, out FunctionHandler handler))
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
                    return $"STRING_VARS[{TranslateExpression(stringReference.inner)}]";

                case NumericReferenceNode numericReference:
                    return $"NUM_VARS[{TranslateExpression(numericReference.inner)}]";

                case BinaryOperatorNode bNode:
                    return $"({bNode.left} {bNode.op} {bNode.right})";

                case UnaryNode uNode:
                    return $"{uNode.lexeme.text}{TranslateExpression(uNode.inner)}";

                case AliasNode aNode:
                    return aNode.lexeme.text;

                case ArrayReference arrayNode:
                    StringBuilder sb = new StringBuilder();
                    sb.Append(arrayNode.arrayName);
                    foreach(Node bracketedNode in arrayNode.nodes)
                    {
                        sb.Append($"[{TranslateExpression(bracketedNode)}]");
                    }
                    return sb.ToString();

                //TODO: could implement type checking for string/numeric types, but should do as part of a seprate process
                case StringLiteral stringLiteral:
                    return stringLiteral.lexeme.text;

                case NumericLiteral numericLiteral:
                    return numericLiteral.lexeme.text;

                default:
                    throw new Exception($"Resolve reference couldn't handle node {node}");
            }
        }
    }
}
