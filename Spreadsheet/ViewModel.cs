﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using IronPython.Hosting;
using Microsoft.Scripting.Hosting;

namespace Spreadsheet {
    public class SpreadsheetModel {
        private Dictionary<string, string> _data = new Dictionary<string, string>();
        private ScriptEngine _engine;
        private ScriptScope _scope;

        public SpreadsheetModel() { }

        private void InitializeScripts() {
            _engine = Python.CreateEngine();
            _scope = _engine.Runtime.CreateScope();
            var sum = @"
def sum2(*args):
    sum = 0
    for arg in args:
        sum += arg
    return sum
";
            _engine.Execute(sum, _scope);
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
                        return _engine.Execute(tokenizer.CurrentValue.ToString(), _scope).ToString();
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
                    return Calc(val.Substring(1) + Char.MinValue);
                } else {
                    return val;
                }
            } else {
                return String.Empty;
            }
        }

        // Always returns underlying storage
        public string GetExpression(string cell) {
            return _data[cell];
        }
    }

    public abstract class ViewModelBase : INotifyPropertyChanged, IDisposable {
        protected ViewModelBase() { }

        /// <summary>
        /// Returns the user-friendly name of this object.
        /// Child classes can set this property to a new value,
        /// or override it to determine the value on-demand.
        /// </summary>
        internal virtual string DisplayName { get; private set; }

        #region INotifyPropertyChanged Members

        /// <summary>
        /// Raised when a property on this object has a new value.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises this object's PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">The property that has a new value.</param>
        protected virtual void OnPropertyChanged(string propertyName) {
            PropertyChangedEventHandler handler = this.PropertyChanged;
            if (handler != null) {
                var e = new PropertyChangedEventArgs(propertyName);
                handler(this, e);
            }
        }

        #endregion // INotifyPropertyChanged Members

        #region IDisposable Members

        /// <summary>
        /// Invoked when this object is being removed from the application
        /// and will be subject to garbage collection.
        /// </summary>
        public void Dispose() {
            this.OnDispose();
        }

        /// <summary>
        /// Child classes can override this method to perform 
        /// clean-up logic, such as removing event handlers.
        /// </summary>
        protected virtual void OnDispose() {
        }

#if DEBUG
        /// <summary>
        /// Useful for ensuring that ViewModel objects are properly garbage collected.
        /// </summary>
        ~ViewModelBase() {
            string msg = string.Format("{0} ({1}) ({2}) Finalized", this.GetType().Name, this.DisplayName, this.GetHashCode());
            System.Diagnostics.Debug.WriteLine(msg);
        }
#endif

        #endregion // IDisposable Members
    }

    public class RowViewModelBase : ViewModelBase {
        internal SpreadsheetModel Model { get; private set; }
        private string _rowNumber;

        public RowViewModelBase(SpreadsheetModel model, int rowNumber) {
            Model = model;
            _rowNumber = rowNumber.ToString();
        }

        protected string GetCell(string column) {
            return Model.GetCell(column + _rowNumber);
        }

        protected string GetExpression(string column) {
            return Model.GetExpression(column + _rowNumber);
        }

        protected void SetCell(string column, string value) {
            base.OnPropertyChanged(column);
            Model.SetCell(column + _rowNumber, value);
        }
    }

    public class RowViewModel : RowViewModelBase {
        public RowViewModel(SpreadsheetModel model, int rowNumber) : base(model, rowNumber) { }

        public string A { get { return GetCell("A"); } set { SetCell("A", value); } }
        public string B { get { return GetCell("B"); } set { SetCell("B", value); } }
        public string C { get { return GetCell("C"); } set { SetCell("C", value); } }
        public string D { get { return GetCell("D"); } set { SetCell("D", value); } }
    }

    public class SpreadsheetViewModel : ViewModelBase {
        private ObservableCollection<RowViewModel> _rows;
        private SpreadsheetModel _model;

        public SpreadsheetViewModel(int rows, int cols) {
            _rows = new ObservableCollection<RowViewModel>();
            _model = new SpreadsheetModel();

            for (int i = 0; i < rows; i++) {
                _rows.Add(new RowViewModel(_model, i + 1));
            }
        }

        public IEnumerable DataSource { get { return _rows; } }
        public SpreadsheetModel Model { get { return _model; } }
    }
}