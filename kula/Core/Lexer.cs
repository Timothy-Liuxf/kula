﻿using System;
using System.Collections.Generic;
using System.Text;

using Kula.Data;
using Kula.Util;

namespace Kula.Core
{
    class Lexer
    {
        private static readonly Lexer instance = new Lexer();
        public static Lexer Instance { get => instance; }
        
        private string sourceCode;
        private List<LexToken> tokenStream;
        
        static class Is
        {
            public static bool CNumber(char c) { return (c <= '9' && c >= '0') || c == '.' || c == '+' || c == '-'; }
            public static bool CName(char c) { return (c <= 'z' && c >= 'a') || (c <= 'Z' && c >= 'A') || (c == '_') || CNumber(c); }
            public static bool CSpace(char c) { return (c == '\n' || c == '\t' || c == '\r' || c == ' '); }
            public static bool CNewLine(char c) { return c == '\n'; }
            public static bool CBracket(char c) 
                { return c == '(' || c == '{' || c == ')' || c == '}' || c == '[' || c == ']' || c == '<' || c == '>'; }
            public static bool CAnnotation(char c) { return c == '#'; }
            public static bool CComma(char c) { return c == ','; }
            public static bool CColon(char c) { return c == ':'; }
            public static bool CEnd(char c) { return c == ';'; }
            public static bool CQuote(char c) { return c == '\"' || c == '\''; }
            public static bool CAssign(char c) { return (c == '='); }
        }
        private Lexer() { sourceCode = ""; }
        public Lexer Read(string code) { sourceCode = code; return this; }
        public Lexer Scan()
        {
            tokenStream = new List<LexToken>();
            LexTokenType? state = null;
            StringBuilder tokenBuilder = new StringBuilder();

            try
            {
                for (int i = 0; i < sourceCode.Length; ++i)
                {
                    if (state == null)
                    {
                        char c = sourceCode[i];
                        if (Is.CSpace(c)) { continue; }
                        else if (Is.CQuote(c))
                        {
                            state = LexTokenType.STRING;
                        }
                        else if (Is.CNumber(c))
                        {
                            tokenBuilder.Append(c);
                            state = LexTokenType.NUMBER;
                        }
                        else if (Is.CName(c))
                        {
                            tokenBuilder.Append(c);
                            state = LexTokenType.NAME;
                        }
                        else if (Is.CBracket(c) || Is.CAssign(c) || Is.CComma(c) || Is.CColon(c) || Is.CEnd(c))
                        {
                            tokenStream.Add(new LexToken(LexTokenType.SYMBOL, c.ToString()));
                        }
                        else if (Is.CAnnotation(c))
                        {
                            while (i + 1 < sourceCode.Length && !Is.CNewLine(sourceCode[++i])) { }
                        }
                    }
                    else
                    {
                        switch (state)
                        {
                            case LexTokenType.NAME:
                                {
                                    while (i < sourceCode.Length && Is.CName(sourceCode[i]))
                                    {
                                        tokenBuilder.Append(sourceCode[i++]);
                                    }
                                }
                                break;
                            case LexTokenType.NUMBER:
                                {
                                    while (i < sourceCode.Length && Is.CNumber(sourceCode[i]))
                                    {
                                        tokenBuilder.Append(sourceCode[i++]);
                                    }
                                }
                                break;
                            case LexTokenType.STRING:
                                {
                                    while (i < sourceCode.Length && !Is.CQuote(sourceCode[i]))
                                    {
                                        tokenBuilder.Append(sourceCode[i++]);
                                    }
                                    ++i;
                                }
                                break;
                            default:
                                break;
                        }
                        string tokenString = tokenBuilder.ToString();
                        tokenStream.Add(new LexToken((LexTokenType)state, tokenString));
                        state = null;
                        tokenBuilder.Clear();
                        --i;
                    }
                }
            }
            catch (Exception)
            {
                tokenStream.Clear();
                throw new KulaException.LexerException();
            }

            return this;
        }
        public Lexer Show()
        {
            if (tokenStream == null) { Scan(); }
            Console.WriteLine("Lexer ->");
            foreach (var token in tokenStream)
            {
                Console.ForegroundColor = LexToken.LexColorDict[token.Type];
                Console.Write("\t");
                Console.WriteLine(token);
            }
            Console.WriteLine();
            Console.ResetColor();
            return this;
        }
        public List<LexToken> Out() { return this.tokenStream; }
    }
}
