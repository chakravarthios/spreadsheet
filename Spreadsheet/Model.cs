﻿using System;
using System.Collections.Generic;
using IronPython.Hosting;
using Microsoft.Scripting.Hosting;

namespace Spreadsheet {
    public class SpreadsheetModel {
        private Dictionary<string, string> _data = new Dictionary<string, string>();
        private Extensions _extensions;

        public SpreadsheetModel(Extensions extensions) {
            _extensions = extensions;
        }

        public string Calc(string expression) {
            if (String.IsNullOrEmpty(expression))
                return String.Empty;

            var tokenizer = new Tokenizer(expression);
            double acc = 0;
            double currentValue;
            Tokens state = Tokens.None;
            while (true) {
                bool resume = tokenizer.ReadNextToken();
                Tokens currentToken = tokenizer.CurrentToken;
                switch (currentToken) {
                    case Tokens.Number:
                    case Tokens.CellReference:
                        currentValue = currentToken == Tokens.Number ? (double)tokenizer.CurrentValue
                                                                     : Double.Parse(GetCell((string)tokenizer.CurrentValue));
                        switch (state) {
                            case Tokens.None:
                                acc = currentValue;
                                state = Tokens.Number;
                                break;
                            case Tokens.Add:
                                acc = acc + currentValue;
                                break;
                            case Tokens.Subtract:
                                acc = acc - currentValue;
                                break;
                            case Tokens.Multiply:
                                acc = acc * currentValue;
                                break;
                            case Tokens.Divide:
                                acc = acc / currentValue;
                                break;
                        }
                        state = Tokens.Number;
                        break;
                    case Tokens.Add:
                    case Tokens.Subtract:
                    case Tokens.Multiply:
                    case Tokens.Divide:
                        if (state == Tokens.Number) {
                            state = tokenizer.CurrentToken;
                        } else {
                            throw new ArgumentException("expected value but found an operator");
                        }
                        break;
                    case Tokens.Function:
                        return _extensions.Execute(tokenizer.CurrentValue.ToString()).ToString();
                }
                if (!resume)
                    break;
            }
            return acc.ToString();
        }

        public void SetCell(string cell, string value) {
            _data[cell] = value.Trim();
        }

        // Does calculation if necessary
        public string GetCell(string cell) {
            if (_data.ContainsKey(cell)) {
                var val = _data[cell];
                if (val.StartsWith("=") || val.StartsWith("@")) {
                    return Calc(val + Char.MinValue);
                } else {
                    return val;
                }
            } else {
                return String.Empty;
            }
        }

        // Always returns underlying storage
        public string GetExpression(string cell) {
            return _data.ContainsKey(cell) ? _data[cell] : String.Empty;
        }
    }
}