using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PonscripterParser
{
    public enum ModeResult
    {
        Success,
        FailureAndChangeState,
        FailureAndTerminate,
    }

    public class SemanticRegexResult
    {
        public ModeResult modeResult;
        public LexingMode newLexingMode;    //null if modeResult is FailureAndTerminate
        public Token token;                 //null if modeResult is FailureAndTerminate or FailureAndChangeState 

        private SemanticRegexResult() { }

        //failure and terminate
        public static SemanticRegexResult FailureAndTerminate()
        {
            return new SemanticRegexResult()
            {
                modeResult = ModeResult.FailureAndTerminate,
            };
        }

        //failure, and change state
        public static SemanticRegexResult FailureAndChangeState(LexingMode lexingMode)
        {
            return new SemanticRegexResult()
            {
                modeResult = ModeResult.FailureAndChangeState,
                newLexingMode = lexingMode,
            };
        }

        //success
        public SemanticRegexResult(TokenType token, string tokenString, LexingMode newLexingMode)//Match m, LexingMode l, bool sucess)
        {
            this.token = new Token(token, tokenString);
            this.newLexingMode = newLexingMode;
            this.modeResult = ModeResult.Success;
        }
    }
}
