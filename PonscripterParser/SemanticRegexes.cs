using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PonscripterParser
{
    public class SemanticRegexResult
    {
        public LexingMode newLexingMode;
        public Token token;

        public SemanticRegexResult(TokenType token, string tokenString, LexingMode newLexingMode)//Match m, LexingMode l, bool sucess)
        {
            this.token = new Token(token, tokenString);
            this.newLexingMode = newLexingMode;
        }
    }

    public abstract class SemanticRegex
    {
        protected Regex pattern;

        public SemanticRegex(string regexAsString)
        {
            this.pattern = new Regex(regexAsString);
        }

        public abstract SemanticRegexResult DoMatch(string s, int startat, LexingMode currentLexingMode);
    }

    //base class for patterns which don't change the current mode
    public abstract class SemanticRegexSameMode : SemanticRegex
    {
        readonly TokenType tokenType;

        public SemanticRegexSameMode(string regexPattern, TokenType tokenType) : base(regexPattern)
        {
            this.tokenType = tokenType;
        }

        public override SemanticRegexResult DoMatch(string s, int startat, LexingMode currentLexingMode)
        {
            Match m = pattern.Match(s, startat);

            return m.Success ? new SemanticRegexResult(tokenType, m.Value, currentLexingMode) : null;
        }
    }

    public abstract class SemanticRegexChangeMode : SemanticRegex
    {
        readonly TokenType tokenType;
        readonly LexingMode newMode;
        public SemanticRegexChangeMode(string regexPattern, TokenType tokenType, LexingMode newMode) : base(regexPattern)
        {
            this.tokenType = tokenType;
            this.newMode = newMode;
        }

        public override SemanticRegexResult DoMatch(string s, int startat, LexingMode currentLexingMode)
        {
            Match m = pattern.Match(s, startat);

            return m.Success ? new SemanticRegexResult(tokenType, m.Value, newMode) : null;
        }
    }
}
