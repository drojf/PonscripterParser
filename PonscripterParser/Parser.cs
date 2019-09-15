using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PonscripterParser
{
    class Node
    {
        public Lexeme lexeme;
        public Node(Lexeme lexeme)
        {
            this.lexeme = lexeme;
        }
    }

    class LabelNode : Node
    {
        string labelName;
        public LabelNode(Lexeme lexeme) : base(lexeme)
        {
            labelName = lexeme.text.TrimStart(new char[] { '*' });
        }
    }

    class NumericReferenceNode : Node
    {
        Node inner;
        public NumericReferenceNode(Lexeme lexeme, Node inner) : base(lexeme) {
            this.inner = inner;
        }
    }

    class StringReferenceNode : Node
    {
        Node inner;
        public StringReferenceNode(Lexeme lexeme, Node inner) : base(lexeme)
        {
            this.inner = inner;
        }
    }

    class AliasNode : Node
    {
        public AliasNode(Lexeme lexeme) : base(lexeme)
        {
        }
    }

    class ArrayReference : Node
    {
        List<Node> nodes;
        Lexeme arrayName;
        public ArrayReference(Lexeme lexeme, Lexeme arrayName) : base(lexeme)
        {
            this.nodes = new List<Node>();
            this.arrayName = arrayName;
        }

        public void AddBracketedExpression(Node node)
        {
            this.nodes.Add(node);
        }
    }

    class FunctionNode : Node
    {
        List<Node> nodes;
        public FunctionNode(Lexeme lexeme) : base(lexeme)
        {
            nodes = new List<Node>();
        }

        public void AddArgument(Node node)
        {
            nodes.Add(node);
        }
    }

    class HexColor : Node
    {
        public HexColor(Lexeme lexeme) : base(lexeme)
        {
        }
    }

    class StringLiteral : Node
    {
        public StringLiteral(Lexeme lexeme) : base(lexeme)
        {
        }
    }

    class NumericLiteral : Node
    {
        public NumericLiteral(Lexeme lexeme) : base(lexeme)
        {
        }
    }

    class Comment : Node
    {
        public Comment(Lexeme lexeme) : base(lexeme)
        {

        }
    }

    class IfStatementNode : Node
    {
        Node condition;
        public IfStatementNode(Lexeme lexeme, Node condition) : base(lexeme)
        {
            this.condition = condition;
        }
    }

    class ForStatementNode : Node
    {
        Node forVariable;
        Node startExpression;
        Node endExpression;
        Node step; //step is optional for for loops

        public ForStatementNode(Lexeme lexeme) : base(lexeme)
        {

        }

        public void SetForVariable(Node forVariable)
        {
            this.forVariable = forVariable;
        }

        public void SetStartExpression(Node start)
        {
            this.startExpression = start;
        }

        public void SetEndExpression(Node end)
        {
            this.endExpression = end;
        }

        public void SetStep(Node step)
        {
            this.step = step;
        }
    }

    class OperatorListNode : Node
    {
        List<Node> nodes;

        public OperatorListNode() : base(null)
        {
            this.nodes = new List<Node>();
        }

        public void push(Node node)
        {
            this.nodes.Add(node);
        }
    }

    class OperatorNode : Node
    {
        public OperatorNode(Lexeme lexeme) : base(lexeme)
        {
        }
    }

    class UnaryNode : Node
    {
        Node inner;
        public UnaryNode(Lexeme lexeme, Node inner) : base(lexeme)
        {
            this.inner = inner;
        }
    }

    class JumpfTarget : Node
    {
        public JumpfTarget(Lexeme lexeme) : base(lexeme)
        {
        }
    }

    class Colon : Node
    {
        public Colon(Lexeme lexeme) : base(lexeme)
        {
        }
    }

    class DialogueNode : Node
    {
        public DialogueNode(Lexeme lexeme) : base(lexeme)
        {
        }
    }

    class TextFormattingTagNode : Node
    {
        public TextFormattingTagNode(Lexeme lexeme) : base(lexeme)
        {
        }
    }

    class ClickWaitNode : Node
    {
        public ClickWaitNode(Lexeme lexeme) : base(lexeme)
        {
        }
    }

    class TextColorChangeNode : Node
    {
        public TextColorChangeNode(Lexeme lexeme) : base(lexeme)
        {
        }
    }

    class ReturnNode : Node
    {
        //this is never used/never happens in the umineko scripts
        LabelNode returnDestination; //can be null if no return destination given.
        public ReturnNode(Lexeme lexeme, LabelNode returnDestination) : base(lexeme)
        {
            this.returnDestination = returnDestination;
        }
    }

    class Parser
    {
        Lexeme debug_lastViewedLexeme;
        int pos;
        List<Lexeme> lexemes;
        List<Node> nodes;
        SubroutineDatabase subroutineDatabase;

        public Parser(List<Lexeme> lexemes, SubroutineDatabase subroutineDatabase)
        {
            this.lexemes = lexemes;
            this.nodes = new List<Node>();
            this.pos = 0;
            this.subroutineDatabase = subroutineDatabase;
        }

        public void Parse()
        {
            PrintLexemes(this.lexemes);

            while(HasNext())
            {
                Console.WriteLine($"Processing {Peek()}");

                SkipWhiteSpace();
                if (!HasNext())
                {
                    break;
                }

                nodes.Add(HandleTopLevel());
            }
        }

        public Node HandleTopLevel()
        {
            switch (Peek().type)
            {
                case LexemeType.COMMENT:
                    return new Comment(Pop());

                case LexemeType.LABEL:
                    //handle label - note that label "text" includes the "*" for now.
                    return new LabelNode(Pop());

                case LexemeType.WORD:
                    return HandleWord();

                //can't have an array reference/alias at top level I think. Must have a numeric reference.
                case LexemeType.NUMERIC_REFERENCE:
                    //this should only happen if you're trying to print a numeric reference/value
                    return HandleNumericValue();

                case LexemeType.STRING_REFERENCE:
                    //this should only happen if you're trying to print a string reference/value
                    return new StringReferenceNode(Pop(), HandleNumericValue());

                case LexemeType.JUMPF_TARGET:
                    return new JumpfTarget(Pop());

                case LexemeType.COLON:
                    return new Colon(Pop());

                case LexemeType.DIALOGUE:
                    return new DialogueNode(Pop());

                case LexemeType.FORMATTING_TAG:
                    return new TextFormattingTagNode(Pop());

                case LexemeType.AT_SYMBOL:
                case LexemeType.BACK_SLASH:
                    return new ClickWaitNode(Pop());

                case LexemeType.OPERATOR:
                    if(Peek().text == "/")
                    {
                        return new ClickWaitNode(Pop());
                    }
                    break;

                case LexemeType.HEX_COLOR:
                    return new TextColorChangeNode(Pop());
            }

            throw GetParsingException("Unexpected lexeme at top level");
        }

        public Node HandleWord()
        {
            if (Peek().text == "if" || Peek().text == "notif")
            {
                return HandleIfCondition();
            }
            else if (Peek().text == "for")
            {
                return HandleFor();
            }
            else if (Peek().text == "return")
            {
                return HandleReturn();
            }
            else
            {
                return HandleFunction();
            }
        }

        //Return statements can take either no argument, or a label as argument (the return destination).
        public Node HandleReturn()
        {
            Lexeme returnWord = Pop();
            LabelNode returnDestination = null;

            if(HasNext() && Peek().type == LexemeType.LABEL)
            {
                returnDestination = new LabelNode(Pop());
            }

            return new ReturnNode(returnWord, returnDestination);
        }

        public Node HandleIfCondition()
        {
            return new IfStatementNode(Pop(LexemeType.WORD), HandleExpression());
        }

        public Node HandleFor()
        {
            ForStatementNode forStatement = new ForStatementNode(Pop(LexemeType.WORD));

            //numeric reference to be assigned to (I think only an 
            SkipWhiteSpace();
            switch (Peek().type)
            {
                case LexemeType.NUMERIC_REFERENCE:
                    //TODO: should probably make a numeric referencec value specific fucntion
                    forStatement.SetStartExpression(HandleNumericValue());
                    break;

                case LexemeType.ARRAY_REFERENCE: //Not sure if ponscripter actually allows array variables to be iterated over
                    forStatement.SetStartExpression(HandleArray());
                    break;

                default:
                    throw GetParsingException("For loop contained a non-assignable first value");
            }

            //literal equals sign (in this case, it means assignment)
            SkipWhiteSpace();
            PopMessage(LexemeType.OPERATOR, "=", "Missing '=' in for loop");

            //numeric literal or variable
            forStatement.SetStartExpression(HandleExpression());

            //literal 'to'
            SkipWhiteSpace();
            PopMessage(LexemeType.WORD, "to", "Missing 'to' in for loop");

            //numeric literal or variable
            forStatement.SetEndExpression(HandleExpression());

            //optional 'step'
            SkipWhiteSpace();
            if (HasNext() && Peek().type == LexemeType.WORD && Peek().text == "step")
            {
                //literal 'step'
                Pop();

                //numeric literal or variable
                forStatement.SetStep(HandleExpression());
            }

            return forStatement;
        }

        public FunctionNode HandleFunction()
        {
            //get the function name
            SkipWhiteSpace();
            Lexeme functionNameLexeme = Pop(LexemeType.WORD);
            FunctionNode functionNode = new FunctionNode(functionNameLexeme);
            string functionName = functionNameLexeme.text;

            //If the function only takes one argument, stop here
            if(!subroutineDatabase.TryGetValue(functionName, out SubroutineInformation subroutineInformation))
            {
                throw GetParsingException($"Function {functionName} not in function database");
            }

            //If have reached the end of line/nothing left to parse, just return no arguments
            if (!HasNext())
            {
                return functionNode;
            }

            //If there is a comma after the function name, just ignore it
            if (Peek().type == LexemeType.COMMA)
            {
                Pop();
            }
            SkipWhiteSpace();

            Console.WriteLine($"Parsing function {functionName} which has {subroutineInformation.hasArguments} arguments");
            if (subroutineInformation.hasArguments)
            {
                //parse the first argument if it has more than one argument
                //TODO: should proabably group tokens here rather than doing later? not sure....

                functionNode.AddArgument(HandleExpression());

                while (HasNext())
                {
                    //Just assume there is one argument after each comma 
                    //If anything else is found besides a comma, assume function arguments have ended.
                    //TODO: (the engine will actually accept spaces instead of commas)
                    //TODO: perhaps should use colon ':' to determine function end if more than one function after each other?
                    SkipWhiteSpace();
                    if (Peek().type != LexemeType.COMMA)
                    {
                        break;
                    }
                    else
                    {
                        Pop();
                    }

                    SkipWhiteSpace();
                    functionNode.AddArgument(HandleExpression());
                }
            }

            return functionNode;
        }

        public Node HandleExpression()
        {
            //TODO: use alternate method (binary try) for expressions to give cleaner tree
            return HandleLogical();
        }

        public Node HandleLogical()
        {
            OperatorListNode list = new OperatorListNode();

            list.push(HandleComparison());
            SkipWhiteSpace();
            while (HasNext() && IsOperatorOfValue("&&", "&"))
            {
                SkipWhiteSpace();
                list.push(new OperatorNode(Pop()));
                SkipWhiteSpace();
                list.push(HandleComparison());
            }

            return list;
        }

        public Node HandleComparison()
        {
            OperatorListNode list = new OperatorListNode();

            list.push(HandleAddition());
            SkipWhiteSpace();
            while (HasNext() && IsOperatorOfValue("==", "!=", "<>", ">=", "<=", ">", "<", "="))
            {
                SkipWhiteSpace();
                list.push(new OperatorNode(Pop()));
                SkipWhiteSpace();
                list.push(HandleAddition());
            }

            return list;
        }

        public Node HandleAddition()
        {
            OperatorListNode list = new OperatorListNode();

            list.push(HandleTimes());
            SkipWhiteSpace();
            while (HasNext() && IsOperatorOfValue("+", "-"))
            {
                SkipWhiteSpace();
                list.push(new OperatorNode(Pop()));
                SkipWhiteSpace();
                list.push(HandleTimes());
            }

            return list;
        }

        public Node HandleTimes()
        {
            OperatorListNode list = new OperatorListNode();

            list.push(HandleUnary());
            SkipWhiteSpace();
            while (HasNext() && IsOperatorOfValue("*", "/"))
            {
                SkipWhiteSpace();

                //handle ambiguity when there is a "/" at the end of a line like  `voicedleay 1300/`
                // - Look ahead of the "/" symbol. 
                // - If anything other than whitespace/comment is found, then assume it's an operator
                // - If only whitespace/comment found and the end of lexemes is reached, assume it's a newline-ignore symbol
                int offset = 1;
                Lexeme futureLexeme;
                do
                {
                    if (!TokenExistsAt(offset))
                    {
                        return list;
                    }

                    futureLexeme = Peek(offset);
                    offset += 1;
                } while (futureLexeme.type == LexemeType.COMMENT || futureLexeme.type == LexemeType.WHITESPACE);

                SkipWhiteSpace();
                list.push(new OperatorNode(Pop()));
                SkipWhiteSpace();
                list.push(HandleUnary());
            }

            return list;
        }

        public Node HandleUnary()
        {
            SkipWhiteSpace();
            if (HasNext() && IsOperatorOfValue("-"))
            {
                //TODO: Not sure if repeated unaries are allowed like `-----5` - allow for now.
                return new UnaryNode(Pop(), HandleUnary());
            }
            else
            {
                return HandleBrackets();
            }
        }

        public Node HandleBrackets()
        {
            SkipWhiteSpace();
            if (Peek().type == LexemeType.L_ROUND_BRACKET)
            {
                Pop();
                Node temp = HandleExpression();
                if(Peek().type == LexemeType.R_ROUND_BRACKET)
                {
                    Pop();
                }
                else
                {
                    throw GetParsingException("Missing closing bracket ')'");
                }
                return temp;
            }
            else
            {
                return HandleExpressionData();
            }
        }

        public Node HandleExpressionData()
        {
            switch (Peek().type)
            {
                case LexemeType.HEX_COLOR:
                    return new HexColor(Pop());

                case LexemeType.WORD:
                    //must be a numalias or stringalias here I think
                    return new AliasNode(Pop());

                case LexemeType.STRING_LITERAL:
                    return new StringLiteral(Pop());

                case LexemeType.NUMERIC_LITERAL:
                    return new NumericLiteral(Pop());

                case LexemeType.LABEL:
                    return new LabelNode(Pop());

                case LexemeType.STRING_REFERENCE:
                    return new StringReferenceNode(Pop(), HandleNumericValue());

                case LexemeType.NUMERIC_REFERENCE:
                    return HandleNumericValue();

                case LexemeType.ARRAY_REFERENCE:
                    return HandleArray();
            }

            throw GetParsingException("Failed to handle Expression");
        }

        public Node HandleArray()
        {
            //First two lexemes must be question mark, then array name
            ArrayReference array = new ArrayReference(
                PopMessage(LexemeType.ARRAY_REFERENCE, "Missing Array ?"), 
                PopMessage(LexemeType.WORD, "Missing Array Name")
            );

            //Arrays must have at least 1 left bracket
            if (Peek().type != LexemeType.L_SQUARE_BRACKET)
            {
                throw GetParsingException("Got Array reference without array brackets");
            }

            while(HasNext() && Peek().type == LexemeType.L_SQUARE_BRACKET)
            {
                //Handle a  '[' EXPRESSION ']'
                Pop(LexemeType.L_SQUARE_BRACKET);
                array.AddBracketedExpression(HandleExpression());
                Pop(LexemeType.R_SQUARE_BRACKET);
            }

            return array;
        }

        // Handle a generic numeric value, which can come after a string or numeric reference, or inside an array `[]`
        public Node HandleNumericValue()
        {
            SkipWhiteSpace();
            switch (Peek().type)
            {
                case LexemeType.NUMERIC_REFERENCE:
                    return new NumericReferenceNode(Pop(), HandleNumericValue());

                case LexemeType.ARRAY_REFERENCE:
                    return HandleArray();

                case LexemeType.NUMERIC_LITERAL:
                    return new NumericLiteral(Pop());

                case LexemeType.WORD:
                    return new AliasNode(Pop());
            }

            throw GetParsingException("Failed to handle Numeric Value");
        }

        private Lexeme PopMessage(LexemeType expectedType, string expectedValue, string message)
        {
            return Pop(expectedType, expectedValue, message);
        }

        private Lexeme PopMessage(LexemeType expectedType, string message)
        {
            return Pop(expectedType, null, message);
        }

        private Lexeme Pop(LexemeType expectedType, string expectedValue)
        {
            return Pop(expectedType, expectedValue, null);
        }

        private Lexeme Pop(LexemeType expectedType)
        {
            return Pop(expectedType, null, null);
        }

        private Lexeme Pop(LexemeType? expectedType, string expectedValue, string message)
        {
            if (expectedValue != null && Peek().text != expectedValue)
            {
                throw GetParsingException($"Pop with wrong text value: got {Peek().text} expected {expectedValue}\nContext: {message}");
            }

            if (expectedType.HasValue && Peek().type != expectedType.Value)
            {
                throw GetParsingException("Pop with wrong type\nContext: {message}");
            }

            return Pop();
        }

        private Lexeme Pop()
        {
            Lexeme temp = Peek();
            debug_lastViewedLexeme = temp; //save last popped lexeme for debugging
            this.pos += 1;
            return temp;
        }

        private bool HasNext()
        {
            return TokenExistsAt(1);
        }

        private bool TokenExistsAt(int offset)
        {
            return (this.pos + offset) < this.lexemes.Count;
        }

        private void SkipWhiteSpace()
        {
            while (HasNext() && Peek().type == LexemeType.WHITESPACE)
            {
                Pop();
            }
        }

        private Lexeme Peek()
        {
            return Peek(0);
        }

        private Lexeme Peek(int offset)
        {
            debug_lastViewedLexeme = this.lexemes[this.pos + offset];
            return this.lexemes[this.pos + offset];
        }

        private bool IsOperatorOfValue(params string[] allowedOperators)
        {
            if (Peek().type != LexemeType.OPERATOR)
            {
                return false;
            }

            foreach (string op in allowedOperators)
            {
                if(Peek().text == op)
                {
                    return true;
                }
            }

            return false;
        }

        private Exception GetParsingException(string message)
        {
            return new Exception(PrintParsingWarning(message));
        }

        private string PrintParsingWarning(string message)
        {
            Console.WriteLine($"-----------------------");
            Console.WriteLine($"Warning: {message} {debug_lastViewedLexeme}");
            Console.WriteLine("Unparsed Lexemes so far");
            PrintLexemes(lexemes, this.pos);
            Console.WriteLine($"-----------------------");

            /*string fullMessage = $"{message} From:{this.line.Substring(this.pos)}";
            Console.WriteLine(fullMessage);

            Console.WriteLine("Lexemes lexed so far:");
            foreach (Lexeme lexeme in this.lexemes)
            {
                Console.WriteLine(lexeme);
            }*/

            return message;
        }

        private void PrintLexemes(List<Lexeme> lexemes, int start)
        {
            Console.Write("[");
            for (int i = start; i < this.lexemes.Count; i++)
            {
                Console.Write($"{this.lexemes[i]}");
                if(i < this.lexemes.Count-1)
                {
                    Console.Write(", ");
                }
            }
            Console.WriteLine("]");
        }

        private void PrintLexemes(List<Lexeme> lexemes)
        {
            PrintLexemes(lexemes, 0);
        }
    }
}
