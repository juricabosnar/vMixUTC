﻿using GalaSoft.MvvmLight.CommandWpf;
using GalaSoft.MvvmLight.Messaging;
using System;
using System.Data;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Xml.Serialization;
using vMixAPI;
using vMixController.Classes;
using vMixController.Extensions;
using vMixController.PropertiesControls;
using vMixController.ViewModel;
using System.Globalization;
using System.ComponentModel;
using System.Windows.Media;
using System.IO;
using System.Net;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace vMixController.Widgets
{
    [Serializable]
    public class vMixControlButton : vMixControl
    {
        public override bool IsResizeableVertical => true;

        private const string MOMENTARY = "Momentary";
        private const string TOGGLE = "Toggle";
        private const string PRESS = "Press";

        private const string DEFAULT = "Default";
        private const string DEFAULTPRESSED = "Default+Pressed";

        Regex _isExpression = new Regex(@"([\+|\-])\=(\d+\.?\d*)");
        [NonSerialized]
        static Queue<Exception> _loggedExceptions = new Queue<Exception>();
        [NonSerialized]
        NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
        const string VARIABLEPREFIX = "_var";
        const string parameterName = "P";
        object parameterValue = null;
        static DateTime _lastShadowUpdate = DateTime.Now;
        static object _locker = new object();
        Stack<bool?> _conditions = new Stack<bool?>();
        [NonSerialized]
        CultureInfo _culture;
        [NonSerialized]
        private BackgroundWorker _activeStateUpdateWorker;
        [NonSerialized]
        private BackgroundWorker _executionWorker;
        [NonSerialized]
        Dictionary<string, string> _trackedValues = new Dictionary<string, string>();

        [NonSerialized]
        bool _stopThread = false;


        /// <summary>
        /// The <see cref="HasScriptErrors" /> property's name.
        /// </summary>
        public const string HasScriptErrorsPropertyName = "HasScriptErrors";

        private bool _hasScriptErrors = false;

        /// <summary>
        /// Sets and gets the HasScriptErrors property.
        /// Changes to that property's value raise the PropertyChanged event. 
        /// </summary>
        [XmlIgnore]
        public bool HasScriptErrors
        {
            get
            {
                return _hasScriptErrors;
            }

            set
            {
                if (_hasScriptErrors == value)
                {
                    return;
                }

                _hasScriptErrors = value;
                RaisePropertyChanged(HasScriptErrorsPropertyName);
            }
        }

        [XmlIgnore]
        public override State State
        {
            get
            {
                return base.State;
            }

            set
            {
                if (_internalState != null)
                    _internalState.OnStateSynced -= State_OnStateUpdated;

                base.State = value;

                if (_internalState != null)
                    _internalState.OnStateSynced += State_OnStateUpdated;
            }
        }


        /// <summary>
        /// The <see cref="Log" /> property's name.
        /// </summary>
        public const string LogPropertyName = "Log";

        private string _log = "";

        /// <summary>
        /// Sets and gets the Log property.
        /// Changes to that property's value raise the PropertyChanged event. 
        [XmlIgnore]
        public string Log
        {
            get
            {
                return _log.TrimStart();
            }

            set
            {
                if (_log == value)
                {
                    return;
                }

                _log = value;
                RaisePropertyChanged(LogPropertyName);
            }
        }

        private void AddLog(string s, params object[] p)
        {
            Log += "\r\n" + string.Format(s, p);
        }

        private void ClearLog()
        {
            Log = "";
        }

        /// <summary>
        /// The <see cref="BlinkBorderColor" /> property's name.
        /// </summary>
        public const string BlinkBorderColorPropertyName = "BlinkBorderColor";
        [NonSerialized]
        private Color _blinkBorderColor = ViewModel.vMixWidgetSettingsViewModel.Colors[0].B;

        /// <summary>
        /// Sets and gets the BorderColor property.
        /// Changes to that property's value raise the PropertyChanged event. 
        /// </summary>
        public Color BlinkBorderColor
        {
            get
            {
                return _blinkBorderColor;
            }

            set
            {
                if (_blinkBorderColor == value)
                {
                    return;
                }

                _blinkBorderColor = value;
                RaisePropertyChanged(BlinkBorderColorPropertyName);
            }
        }

        public override void ShadowUpdate()
        {

            if (IsStateDependent && _internalState != null && (DateTime.Now - _lastShadowUpdate).TotalSeconds > 0.01)
            {
                _internalState.UpdateAsync();
                _lastShadowUpdate = DateTime.Now;
            }
            base.ShadowUpdate();
        }

        private void State_OnStateUpdated(object sender, StateSyncedEventArgs e)
        {
            if (e.Successfully)
                RealUpdateActiveProperty(false, null, State);

        }

        private void RealUpdateActiveProperty(bool skipStateDependency = false, vMixAPI.State stateToCheck = null, vMixAPI.State currentState = null)
        {
            stateToCheck = stateToCheck ?? _internalState;
            currentState = currentState ?? State;

            if ((!IsStateDependent && skipStateDependency) || stateToCheck == null || _activeStateUpdateWorker.IsBusy) return;
            _activeStateUpdateWorker.RunWorkerAsync(new State[] { stateToCheck, currentState });
        }

        public override string Type
        {
            get
            {
                return Extensions.LocalizationManager.Get("Button");
            }
        }

        /// <summary>
        /// The <see cref="Commands" /> property's name.
        /// </summary>
        public const string CommandsPropertyName = "Commands";

        private ObservableCollection<vMixControlButtonCommand> _commands = new ObservableCollection<vMixControlButtonCommand>();

        /// <summary>
        /// Sets and gets the Actions property.
        /// Changes to that property's value raise the PropertyChanged event. 
        /// </summary>
        public ObservableCollection<vMixControlButtonCommand> Commands
        {
            get
            {
                return _commands;
            }

            set
            {
                if (_commands == value)
                {
                    return;
                }

                _commands = value;
                RaisePropertyChanged(CommandsPropertyName);
            }
        }

        /// <summary>
        /// The <see cref="AutoStart" /> property's name.
        /// </summary>
        public const string AutoStartPropertyName = "AutoStart";

        private bool _autoStart = false;

        /// <summary>
        /// Sets and gets the AutoStart property.
        /// Changes to that property's value raise the PropertyChanged event. 
        /// </summary>
        public bool AutoStart
        {
            get
            {
                return _autoStart;
            }

            set
            {
                if (_autoStart == value)
                {
                    return;
                }

                _autoStart = value;
                RaisePropertyChanged(AutoStartPropertyName);
            }
        }

        /// <summary>
        /// The <see cref="Enabled" /> property's name.
        /// </summary>
        public const string EnabledPropertyName = "Enabled";

        private bool _enabled = true;

        /// <summary>
        /// Sets and gets the Enabled property.
        /// Changes to that property's value raise the PropertyChanged event. 
        /// </summary>
        [XmlIgnore]
        public bool Enabled
        {
            get
            {
                return _enabled;
            }

            set
            {
                if (_enabled == value)
                {
                    return;
                }

                _enabled = value;
                RaisePropertyChanged(EnabledPropertyName);
            }
        }

        /// <summary>
        /// The <see cref="Active" /> property's name.
        /// </summary>
        public const string ActivePropertyName = "Active";

        private bool _active = false;

        /// <summary>
        /// Sets and gets the Active property.
        /// Changes to that property's value raise the PropertyChanged event. 
        /// </summary>
        [XmlIgnore]
        public bool Active
        {
            get
            {
                return _active;
            }

            set
            {
                /*if (_active == value)
                {
                    return;
                }*/

                if (Style == MOMENTARY)
                    IsPushed = value;

                _active = value;
                RaisePropertyChanged(ActivePropertyName);
            }
        }


        /// <summary>
        /// The <see cref="IsStateDependent" /> property's name.
        /// </summary>
        public const string IsStateDependentPropertyName = "IsStateDependent";

        private bool _isStateDependent = false;

        /// <summary>
        /// Sets and gets the IsStateDependent property.
        /// Changes to that property's value raise the PropertyChanged event. 
        /// </summary>
        public bool IsStateDependent
        {
            get
            {
                return _isStateDependent;
            }

            set
            {
                if (_isStateDependent == value)
                {
                    return;
                }

                _isStateDependent = value;
                RaisePropertyChanged(IsStateDependentPropertyName);
            }
        }

        /// <summary>
        /// The <see cref="IsColorized" /> property's name.
        /// </summary>
        public const string IsColorizedPropertyName = "IsColorized";

        private bool _isColorized = false;

        /// <summary>
        /// Sets and gets the IsColorized property.
        /// Changes to that property's value raise the PropertyChanged event. 
        /// </summary>
        public bool IsColorized
        {
            get
            {
                return _isColorized;
            }

            set
            {
                if (_isColorized == value)
                {
                    return;
                }

                _isColorized = value;
                RaisePropertyChanged(IsColorizedPropertyName);
            }
        }

        /// <summary>
        /// The <see cref="Image" /> property's name.
        /// </summary>
        public const string ImagePropertyName = "Image";

        private string _image = "";

        /// <summary>
        /// Sets and gets the Image property.
        /// Changes to that property's value raise the PropertyChanged event. 
        /// </summary>
        public string Image
        {
            get
            {
                return _image;
            }

            set
            {
                if (_image == value)
                {
                    return;
                }

                _image = value;
                RaisePropertyChanged(ImagePropertyName);
            }
        }

        /// <summary>
        /// The <see cref="ImageMax" /> property's name.
        /// </summary>
        public const string ImageMaxPropertyName = "ImageMax";

        private int _imageMax = 1;

        /// <summary>
        /// Sets and gets the ImageMax property.
        /// Changes to that property's value raise the PropertyChanged event. 
        /// </summary>
        public int ImageMax
        {
            get
            {
                return _imageMax;
            }

            set
            {
                if (_imageMax == value)
                {
                    return;
                }

                _imageMax = value;
                RaisePropertyChanged(ImageMaxPropertyName);
            }
        }

        /// <summary>
        /// The <see cref="ImageNumber" /> property's name.
        /// </summary>
        public const string ImageNumberPropertyName = "ImageNumber";

        private int _imageNumber = 0;

        /// <summary>
        /// Sets and gets the ImageNumber property.
        /// Changes to that property's value raise the PropertyChanged event. 
        /// </summary>
        public int ImageNumber
        {
            get
            {
                return _imageNumber;
            }

            set
            {
                if (_imageNumber == value)
                {
                    return;
                }

                _imageNumber = value;
                RaisePropertyChanged(ImageNumberPropertyName);
            }
        }

        /// <summary>
        /// The <see cref="Variables" /> property's name.
        /// </summary>
        public const string VariablesPropertyName = "Variables";

        [NonSerialized]
        private List<Pair<int, object>> _variables = new List<Pair<int, object>>();

        /// <summary>
        /// Sets and gets the Variables property.
        /// Changes to that property's value raise the PropertyChanged event. 
        /// </summary>
        [XmlIgnore]
        public List<Pair<int, object>> Variables
        {
            get
            {
                return _variables;
            }

            set
            {
                if (_variables == value)
                {
                    return;
                }

                _variables = value;
                RaisePropertyChanged(VariablesPropertyName);
            }
        }


        /// <summary>
        /// The <see cref="IsPushed" /> property's name.
        /// </summary>
        public const string IsPushedPropertyName = "IsPushed";

        private bool _isPushed = false;

        /// <summary>
        /// Sets and gets the IsPushed property.
        /// Changes to that property's value raise the PropertyChanged event. 
        /// </summary>
        public bool IsPushed
        {
            get
            {
                return _isPushed;
            }

            set
            {
                if (_isPushed == value)
                {
                    return;
                }

                if (_imageMax == 2 && value)
                    ImageNumber = 1;
                if (!value)
                    ImageNumber = 0;

                _isPushed = value;
                RaisePropertyChanged(IsPushedPropertyName);
            }
        }

        /// <summary>
        /// The <see cref="Style" /> property's name.
        /// </summary>
        public const string StylePropertyName = "Style";
        private string _style = MOMENTARY;

        /// <summary>
        /// Sets and gets the Style property.
        /// Changes to that property's value raise the PropertyChanged event. 
        /// </summary>
        public string Style
        {
            get
            {
                return _style;
            }

            set
            {
                if (_style == value)
                {
                    return;
                }

                _style = value;
                RaisePropertyChanged(StylePropertyName);
            }
        }

        [NonSerialized]
        private RelayCommand _executeScriptCommand;

        /// <summary>
        /// Gets the ExecuteScriptCommand.
        /// </summary>
        [XmlIgnore]
        public RelayCommand ExecuteScriptCommand
        {
            get
            {
                return _executeScriptCommand
                    ?? (_executeScriptCommand = new RelayCommand(
                    () =>
                    {
                        Debug.Print("Click");
                        if (Style == MOMENTARY)
                            ExecuteScript();

                    }));
            }
        }

        private void ExecuteScript()
        {
            if (Style == MOMENTARY)
                Enabled = false;
            if (_executionWorker != null && _executionWorker.IsBusy)
            {
                _stopThread = true;
                _executionWorker.CancelAsync();
            }
            _stopThread = false;

            if (!_executionWorker.IsBusy)
                _executionWorker.RunWorkerAsync(State);
        }

        [NonSerialized]
        private RelayCommand<MouseEventArgs> _executePushOn;

        /// <summary>
        /// Gets the ExecutePushOn.
        /// </summary>
        public RelayCommand<MouseEventArgs> ExecutePushOn
        {
            get
            {
                return _executePushOn
                    ?? (_executePushOn = new RelayCommand<MouseEventArgs>(
                    (p) =>
                    {
                        Debug.Print("Press");
                        Debug.Print("{0}", Mouse.Capture((IInputElement)p.Source, CaptureMode.SubTree));
                        switch (Style)
                        {
                            case PRESS:
                                IsPushed = true;
                                ExecuteScript();
                                break;
                            case MOMENTARY: if (!IsStateDependent) IsPushed = true; break;
                            case TOGGLE:
                                IsPushed = !IsPushed;
                                ExecuteScript();
                                break;
                        }
                        //p.Handled = true;

                    }));
            }
        }

        [NonSerialized]
        private RelayCommand<MouseEventArgs> _executePushOff;

        /// <summary>
        /// Gets the ExecutePushOff.
        /// </summary>
        public RelayCommand<MouseEventArgs> ExecutePushOff
        {
            get
            {
                return _executePushOff
                    ?? (_executePushOff = new RelayCommand<MouseEventArgs>(
                    (p) =>
                    {
                        Debug.Print("Release");
                        Mouse.Capture(null);
                        switch (Style)
                        {
                            case PRESS:
                                IsPushed = false;
                                ExecuteScript();
                                break;
                            case MOMENTARY:
                                if (!IsStateDependent) IsPushed = false;
                                ExecuteScript();
                                break;
                            case TOGGLE: break;
                        }
                        //p.Handled = true;

                    }));
            }
        }

        [NonSerialized]
        private RelayCommand _stopScriptCommand;

        /// <summary>
        /// Gets the StopScriptCommand.
        /// </summary>
        [XmlIgnore]
        public RelayCommand StopScriptCommand
        {
            get
            {
                return _stopScriptCommand
                    ?? (_stopScriptCommand = new RelayCommand(
                    () =>
                    {
                        _stopThread = true;
                        if (_executionWorker != null && _executionWorker.IsBusy)
                            _executionWorker.CancelAsync();

                        _trackedValues.Clear();
                        _conditions.Clear();
                        Enabled = true;
                    }));
            }
        }

        private void UpdateActiveProperty()
        {
            if (!IsStateDependent) return;
            State.UpdateAsync();

        }

        public vMixControlButton()
        {
            _activeStateUpdateWorker = new BackgroundWorker();
            _activeStateUpdateWorker.RunWorkerCompleted += Worker_RunWorkerCompleted;
            _activeStateUpdateWorker.DoWork += ActiveStateUpdateWorker_DoWork;

            _executionWorker = new BackgroundWorker();
            _executionWorker.DoWork += ExecutionWorker_DoWork;
            _executionWorker.WorkerSupportsCancellation = true;

            _enabled = true;
            _culture = new CultureInfo(CultureInfo.InvariantCulture.Name);
            _culture.NumberFormat.NumberDecimalDigits = 5;
            _culture.NumberFormat.CurrencyDecimalDigits = 5;


            /*_blinker = new DispatcherTimer();
            _blinker.Tick += Blinker_Tick;
            _blinker.Interval = TimeSpan.FromSeconds(1);
            _blinker.Start();*/
        }

        /*private void Blinker_Tick(object sender, EventArgs e)
        {
            if (_defaultBorderColor.A == 0)
                _defaultBorderColor = BorderColor;
            if (!Enabled)
            {
                if (BlinkBorderColor != BorderColor)
                    BlinkBorderColor = BorderColor;
                else
                    BlinkBorderColor = Colors.Lime;
            }
            else
                BlinkBorderColor = BorderColor;
        }*/

        private void ExecutionWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            ExecutionThread((State)e.Argument);
        }

        private void ActiveStateUpdateWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            lock (_locker)
            {

                var stateToCheck = ((State[])e.Argument)[0];
                var currentState = ((State[])e.Argument)[1];
                var result = false;

                /*var cnt = 0;
                for (int i = 0; i < _commands.Count; i++)
                {
                    var item = _commands[i];
                    if (stateToCheck.ChangedInputs.Contains(item.InputKey))
                        cnt++;
                    if (!string.IsNullOrWhiteSpace(item.Action.ActiveStatePath))
                        cnt++;
                }
                if (cnt == 0)
                {
                    e.Result = Active;
                    return;
                }*/


                HasScriptErrors = false;

                Stack<bool> conds = new Stack<bool>();
                for (int i = 0; i < _commands.Count; i++)
                {
                    var item = _commands[i];

                    if (item.UseInActiveState && (conds.Count == 0 || conds.Peek()))
                    {
                        if (currentState == null) return;

                        var input = currentState.Inputs.Where(x => x.Key == item.InputKey).FirstOrDefault()?.Number;

                        int intp = CalculateExpression<int>(item.Parameter);
                        object strp = CalculateExpression(item.StringParameter);
                        int strpasint = -1;
                        if (strp != null)
                            if (!int.TryParse(strp.ToString(), out strpasint))
                                strpasint = int.MinValue;

                        string keybystring = currentState.Inputs.Where(x => x.Number == strpasint).FirstOrDefault()?.Key ?? (strp?.ToString() ?? "");
                        string keybyint = currentState.Inputs.Where(x => x.Number == intp).FirstOrDefault()?.Key ?? "";

                        

                        if (string.IsNullOrWhiteSpace(item.Action.ActiveStatePath) && item.Action.ActiveStatePathIntDependence == null) continue;
                        var path = "";
                        
                        if (!string.IsNullOrWhiteSpace(item.Action.ActiveStatePath))
                            path = string.Format(item.Action.ActiveStatePath, item.InputKey, intp, strp, intp - 1, input ?? -1, "", keybyint, keybystring);
                        
                        if (item.Action.ActiveStatePathIntDependence != null)
                            path = string.Format(item.Action.ActiveStatePathIntDependence[intp], item.InputKey, intp, strp, intp - 1, input ?? -1, "", keybyint, keybystring);

                        if (string.IsNullOrWhiteSpace(path)) return;

                        var nval = GetValueByPath(stateToCheck, path);
                        var val = nval == null ? "" : nval.ToString();
                        HasScriptErrors = HasScriptErrors || nval == null;
                        var aval = string.Format(item.Action.ActiveStateValue, GetInputNumber(item.InputKey, stateToCheck), intp, strp, intp - 1, input ?? -1, "", keybyint, keybystring);
                        var realval = aval;
                        aval = aval.TrimStart('!', '~');
                        //! - not
                        //~ - contains
                        //` - not contains
                        bool mult = (aval == "-" && ((val is string && string.IsNullOrWhiteSpace((string)val)) || (val == null))) ||
                            (aval == "*") ||
                            (val != null && !(val is string) && aval == val.ToString()) ||
                            (val is string && (string)val == aval) ||
                            (realval[0] == '~' && (val is string && ((string)val).IndexOf(aval) >= 0)) ||
                            (realval[0] == '`' && (val is string && ((string)val).IndexOf(aval) < 0));
                        if (!string.IsNullOrWhiteSpace(aval) && realval[0] == '!')
                            mult = !mult;
                        result = result || mult;
                    }

                }
                e.Result = result;
            }
        }

        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {

            Active = (bool)(e.Result ?? false);

        }

        public override Hotkey[] GetHotkeys()
        {
            return new Classes.Hotkey[] {
                new Classes.Hotkey { Name = "Execute" },
                new Classes.Hotkey { Name = "Reset" },
                new Classes.Hotkey { Name = "Clear Variables" },
                new Classes.Hotkey { Name = "Press" },
                new Classes.Hotkey { Name = "Release" }
            };
        }

        private string GetInputNumber(int input)
        {
            try
            {
                return State.Inputs[input].Number.ToString();
            }
            catch (Exception)
            {
                return "-1";
            }
        }

        private string GetInputNumber(string key, vMixAPI.State state = null)
        {
            try
            {
                var input = (state ?? State).Inputs.Where(x => x.Key == key).FirstOrDefault();
                if (input != null)
                    return input.Number.ToString();
                else
                    return "-1";
            }
            catch (Exception)
            {
                return "-1";
            }
        }

        private void PopulateVariables(NCalc.Expression exp)
        {
            foreach (var item in _variables)
            {
                var x = Dispatcher.Invoke(() => new { item.A, item.B });
                exp.Parameters.Add(string.Format("{0}{1}", VARIABLEPREFIX, x.A), x.B);
            }
            foreach (var item in GlobalVariablesViewModel._variables)
            {
                var x = Dispatcher.Invoke(() => new { item.A, item.B });
                if (!exp.Parameters.ContainsKey(x.A))
                    exp.Parameters.Add(x.A, x.B);
            }
            exp.Parameters.Add(parameterName, parameterValue);
            exp.EvaluateFunction += Exp_EvaluateFunction;
        }

        private void Exp_EvaluateFunction(string name, NCalc.FunctionArgs args)
        {
            var p = args.EvaluateParameters();
            args.HasResult = false;
            switch (name)
            {
                case "_":
                    if (p.Length > 0)
                    {
                        args.HasResult = true;
                        if (_isStateDependent && _internalState != null)
                            args.Result = GetValueByPath(_internalState, p[0].ToString());
                        else
                            args.Result = Dispatcher.Invoke<object>(() => GetValueByPath(State, p[0].ToString()));
                    }
                    break;
                case "expandvariables":
                    if (p.Length > 0)
                    {
                        args.HasResult = true;
                        args.Result = Environment.ExpandEnvironmentVariables(p[0].ToString());
                    }
                    break;
                //string functions
                case "split":
                    if (p.Length > 1 && p[0] is string && p[1] is string)
                    {
                        args.HasResult = true;
                        args.Result = ((string)p[0]).Split(new string[] { (string)p[1] }, StringSplitOptions.RemoveEmptyEntries);
                    }
                    break;
                case "trim":
                    if (p.Length > 0 && p[0] is string)
                    {
                        args.HasResult = true;
                        args.Result = ((string)p[0]).Trim();
                    }
                    break;
                //vMix functions
                case "API":
                    //state.SendFunction(string.Format(cmd.Action.FormatString, cmd.InputKey, CalculateExpression<int>(cmd.Parameter), Dispatcher.Invoke(() => CalculateObjectParameter(cmd)), CalculateExpression<int>(cmd.Parameter) - 1, input.HasValue ? input.Value : 0), false);
                    break;
                //array functions
                case "getvalue":
                    if (p.Length > 1 && p[0] is Array && p[1] is int)
                    {
                        args.HasResult = true;
                        args.Result = ((Array)p[0]).GetValue((int)p[1]);
                    }
                    break;
            }
        }

        private bool Calculate(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            NCalc.Expression exp = new NCalc.Expression(s);
            PopulateVariables(exp);
            if (exp.HasErrors())
                return false;
            else
                try
                {
                    return (bool)exp.Evaluate();
                }
                catch (Exception ex)
                {
                    return false;
                }
        }

        private object CalculateExpression(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return s;
            NCalc.Expression exp = new NCalc.Expression(s);
            exp.EvaluateParameter += Exp_EvaluateParameter;
            PopulateVariables(exp);
            if (exp.HasErrors())
                return s;
            else
            {

                try
                {
                    return exp.Evaluate();
                }
                catch (Exception)
                {
                    return s;
                }
            }
        }

        private void Exp_EvaluateParameter(string name, NCalc.ParameterArgs args)
        {
            //Avoid non-defined parameters with their names
            args.Result = name;
        }

        private T CalculateExpression<T>(string s)
        {
            var result = CalculateExpression(s);
            try
            {
                return (T)Convert.ChangeType(result, typeof(T));
            }
            catch
            {
                return default;
            }
        }

        private object EscapeString(object o)
        {
            if (o is string)
            {
                if (!float.TryParse((string)o, out float val))
                    return string.Format("'{0}'", o);
                return
                    val;
            }
            return o;
        }

        private bool TestCondition(vMixControlButtonCommand cmd)
        {
            if (cmd.AdditionalParameters == null || cmd.AdditionalParameters.Count == 0)
                return false;

            var part1 = string.Format(cmd.AdditionalParameters[1].A, cmd.InputKey, cmd.AdditionalParameters[0].A)?.ToString() ?? "";
            var part2 = string.Format(cmd.AdditionalParameters[3].A, cmd.InputKey, cmd.AdditionalParameters[0].A)?.ToString() ?? "";
            Thread.CurrentThread.CurrentCulture = _culture;
            Thread.CurrentThread.CurrentUICulture = _culture;

            var expr1 = CalculateExpression(part1);
            var expr2 = CalculateExpression(part2);

            //put expressions into variables for comparing
            var idx1 = GetVariableIndex(65534);
            var idx2 = GetVariableIndex(65535);

            if (idx1 < 0 || idx2 < 0)
            {
                Dispatcher.Invoke(() => _variables.Add(new Pair<int, object>() { A = 65534, B = expr1 }));
                Dispatcher.Invoke(() => _variables.Add(new Pair<int, object>() { A = 65535, B = expr2 }));
            }
            else
            {
                Dispatcher.Invoke(() => _variables[idx1].B = expr1);
                Dispatcher.Invoke(() => _variables[idx2].B = expr2);
            }

            string expression = string.Format("{0}{1}{2}", "_var65534", cmd.AdditionalParameters[2].A, "_var65535");
            AddLog("CONDITION CHECK {0}{1}{2}", expr1, cmd.AdditionalParameters[2].A, expr2);
            return Calculate(expression);
        }

        private int GetVariableIndex(int number)
        {
            for (int i = 0; i < _variables.Count; i++)
            {
                if (Dispatcher.Invoke(() => _variables[i].A) == number)
                    return i;
            }
            return -1;
        }

        private void ExecutionThread(object _state)
        {
            vMixAPI.State state = (vMixAPI.State)_state;
            Stack<bool?> _conditions = new Stack<bool?>();
            int _waitBeforeUpdate = -1;
            int _jumpCount = 0;
            ClearLog();
            BlinkBorderColor = Colors.Lime;
            for (int _pointer = 0; _pointer < _commands.Count; _pointer++)
            {
                int parameter = 0;
                string strparameter = "";
                bool? conditionResult = null;
                if (_stopThread)
                {
                    BlinkBorderColor = BorderColor;
                    return;
                }
                var cmd = _commands[_pointer];

                if (!cmd.IsExecutable) continue;

                var cond = new bool?(true);
                if (_conditions.Count > 0)
                    cond = _conditions.Peek();
                if ((cond.HasValue && cond.Value) ||
                    (cmd.Action.Function == NativeFunctions.CONDITIONEND ||
                     cmd.Action.Function == NativeFunctions.CONDITION ||
                     cmd.Action.Function == NativeFunctions.HASVARIABLE ||
                     cmd.Action.Function == NativeFunctions.ISPRESSED ||
                     cmd.Action.Function == NativeFunctions.ELSE))
                    if (cmd.Action.Native)
                    {
                        switch (cmd.Action.Function)
                        {
                            case NativeFunctions.API:
                                WebClient _webClient = new vMixWebClient();
                                strparameter = string.Format("http://{0}", CalculateObjectParameter(cmd).ToString());
                                Uri uri;
                                if (Uri.TryCreate(strparameter, UriKind.Absolute, out uri))
                                {
                                    _webClient.DownloadStringAsync(uri, null);
                                    AddLog("{1}) API {0}", strparameter, _pointer + 1);
                                }
                                else
                                    AddLog("{1}) API WRONG URL = {0}", strparameter, _pointer + 1);
                                break;
                            case NativeFunctions.TIMER:
                                parameter = CalculateExpression<int>(cmd.Parameter);
                                AddLog("{2}) TIMER {0} [{1}]", cmd.Parameter, parameter, _pointer + 1);
                                Thread.Sleep(parameter);
                                break;
                            case NativeFunctions.UPDATESTATE:
                                AddLog("{0}) STATE UPDATING", _pointer + 1);
                                Dispatcher.Invoke(() => state?.UpdateAsync());
                                break;
                            case NativeFunctions.UPDATEINTERNALBUTTONSTATE:
                                AddLog("{0}) INTERNAL BUTTON STATE UPDATING", _pointer + 1);
                                Dispatcher.Invoke(() => _internalState?.UpdateAsync());
                                break;
                            case NativeFunctions.GOTO:
                                if (_jumpCount >= 5)
                                {
                                    ClearLog();
                                    _jumpCount = 0;
                                }
                                parameter = CalculateExpression<int>(cmd.Parameter);
                                AddLog("{2}) GOTO {0} [{1}]", cmd.Parameter, parameter, _pointer + 1);
                                _pointer = parameter - 1;
                                _jumpCount++;
                                break;
                            case NativeFunctions.EXECLINK:
                                //Calculating expressions into EXECLINKS
                                strparameter = Dispatcher.Invoke(() => CalculateObjectParameter(cmd)).ToString();
                                AddLog("{2}) EXECLINK {0} [{1}]", cmd.StringParameter, strparameter, _pointer + 1);
                                Dispatcher.Invoke(() => Messenger.Default.Send<Pair<string, object>>(new Pair<string, object>(strparameter, null)));
                                break;
                            case NativeFunctions.LIVETOGGLE:
                                AddLog("{0}) LIVETOGGLE", _pointer + 1);
                                Dispatcher.Invoke(() => Messenger.Default.Send<LIVEToggleMessage>(new LIVEToggleMessage() { State = 2 }));
                                break;
                            case NativeFunctions.LIVEOFF:
                                AddLog("{0}) LIVEOFF", _pointer + 1);
                                Dispatcher.Invoke(() => Messenger.Default.Send<LIVEToggleMessage>(new LIVEToggleMessage() { State = 0 }));
                                break;
                            case NativeFunctions.LIVEON:
                                AddLog("{0}) LIVEON", _pointer + 1);
                                Dispatcher.Invoke(() => Messenger.Default.Send<LIVEToggleMessage>(new LIVEToggleMessage() { State = 1 }));
                                break;
                            case NativeFunctions.CONDITION:
                                conditionResult = cond.HasValue && cond.Value ? new bool?(TestCondition(cmd)) : null;
                                AddLog("{1}) CONDITION IS {0}", conditionResult, _pointer + 1);
                                _conditions.Push(conditionResult);
                                break;
                            case NativeFunctions.ELSE:
                                AddLog("{0}) ELSE EXECUTED", _pointer + 1);
                                _conditions.Push(!_conditions.Pop());
                                break;
                            case NativeFunctions.HASVARIABLE:
                                parameter = CalculateExpression<int>(cmd.Parameter);
                                conditionResult = cond.HasValue && cond.Value ? new bool?(GetVariableIndex(parameter) != -1) : null;
                                AddLog("{2}) HASVARIABLE {0} IS {1}", parameter, conditionResult, _pointer + 1);
                                _conditions.Push(conditionResult);
                                break;
                            case NativeFunctions.ISPRESSED:
                                AddLog("{1}) BUTTON PUSHED = {0}", IsPushed, _pointer + 1);
                                _conditions.Push(IsPushed);
                                break;
                            case NativeFunctions.CONDITIONEND:
                                AddLog("{0}) CONDITIONEND", _pointer + 1);
                                _conditions.Pop();
                                break;
                            case NativeFunctions.SETVARIABLE:

                                var idx = GetVariableIndex(CalculateExpression<int>(cmd.Parameter));
                                var tobj = CalculateObjectParameter(cmd);
                                AddLog("{2}) SETVARIABLE {0} TO {1}", idx, tobj, _pointer + 1);
                                if (idx == -1)
                                    Dispatcher.Invoke(() => _variables.Add(new Pair<int, object>() { A = CalculateExpression<int>(cmd.Parameter), B = tobj }));
                                else
                                {
                                    Dispatcher.Invoke(() => _variables[idx].B = tobj);
                                }
                                break;
                            case NativeFunctions.VALUECHANGED:

                                var obj = CalculateObjectParameter(cmd).ToString();
                                var key = (string.Format(cmd.StringParameter, cmd.InputKey));
                                var hasKey = _trackedValues.ContainsKey(key);
                                AddLog("{2}) VALUECHANGED {0} IS {1}", obj, hasKey, _pointer + 1);
                                _conditions.Push(hasKey ? obj != _trackedValues[key] : false);
                                _trackedValues[key] = obj;
                                break;
                        }
                    }
                    else if (state != null)
                    {
                        var key = Utils.FindInputKeyByVariable(cmd.InputKey, Dispatcher);

                        var input = state.Inputs.Where(x => x.Key == key).FirstOrDefault()?.Number;
                        var command = string.Format(cmd.Action.FormatString, key, CalculateExpression<int>(cmd.Parameter), Convert.ToString(Dispatcher.Invoke(() => CalculateObjectParameter(cmd)), CultureInfo.InvariantCulture), CalculateExpression<int>(cmd.Parameter) - 1, input ?? 0, string.IsNullOrWhiteSpace(key) ? "" : "Input=");

                        if (!cmd.Action.StateDirect)
                            AddLog("{2}) SEND {0} WITH RESULT {1}", command, state.SendFunction(command, false), _pointer + 1);
                        else
                        {
                            var path = string.Format(cmd.Action.StatePath, key, CalculateExpression<int>(cmd.Parameter), Dispatcher.Invoke(() => CalculateObjectParameter(cmd)), CalculateExpression<int>(cmd.Parameter) - 1, input ?? 0, string.IsNullOrWhiteSpace(key) ? "" : "Input=");
                            object value;
                            switch (cmd.Action.StateValue)
                            {
                                case "Input":
                                    value = (object)key;
                                    break;
                                case "String":
                                    value = Dispatcher.Invoke(() => CalculateObjectParameter(cmd))?.ToString() ?? "";
                                    break;
                                default:
                                    value = (object)CalculateExpression<int>(cmd.Parameter);
                                    break;
                            }
                            AddLog("{2}) SET {0} TO {1}", path, value, _pointer + 1);


                            //translate (+/-=number) into expression
                            if (value is string strvalue && _isExpression.IsMatch(strvalue))
                            {
                                var expr = _isExpression.Split(strvalue);
                                value = Dispatcher.Invoke(() => CalculateExpression(string.Format("1 * _('{0}') {1} {2}", path, expr[1], expr[2])))?.ToString() ?? "";
                            }

                            SetValueByPath(state, path, value);
                            int flag = 0;
                            while (GetValueByPath(state, path) != value)
                            {
                                Thread.Sleep(50);
                                if (_stopThread)
                                    return;
                                if (++flag > 10)
                                    break;
                            }
                        }
                        _waitBeforeUpdate = Math.Max((_internalState ?? state).Transitions[cmd.Action.TransitionNumber].Duration, _waitBeforeUpdate);
                    }
                    else
                        AddLog("{0}) {1} IS NOT EXECUTED", _pointer + 1, cmd.Action.Function);
            }
            _conditions.Clear();
            BlinkBorderColor = BorderColor;
            ThreadPool.QueueUserWorkItem((x) =>
            {
                Enabled = true;
                Thread.Sleep(_waitBeforeUpdate);
                _waitBeforeUpdate = -1;
                //NULL Check
                Dispatcher.Invoke(() => _internalState?.UpdateAsync());
            });
        }

        private object CalculateObjectParameter(vMixControlButtonCommand cmd)
        {
            return CalculateExpression(string.Format(cmd.StringParameter, Utils.FindInputKeyByVariable(cmd.InputKey, Dispatcher))?.ToString() ?? "");
        }

        public override void ExecuteHotkey(int index)
        {
            base.ExecuteHotkey(index);
            switch (index)
            {
                case 0:
                    ExecuteScriptCommand.Execute(null);
                    break;
                case 1:
                    StopScriptCommand.Execute(null);
                    break;
                case 2:
                    Variables.Clear();
                    break;
                case 3:
                    IsPushed = true;
                    ExecuteScript();
                    break;
                case 4:
                    IsPushed = false;
                    ExecuteScript();
                    break;
            }
        }

        public override void ExecuteHotkey(int index, object parameter)
        {
            parameterValue = parameter;
            base.ExecuteHotkey(index, parameter);
            ExecuteHotkey(index);
        }

        public override UserControl[] GetPropertiesControls()
        {
            FilePathControl imgctrl = GetPropertyControl<FilePathControl>(this.Type);
            imgctrl.Filter = "Images|*.bmp;*.jpg;*.png;*.ico";
            imgctrl.Value = Image;
            imgctrl.Title = "Image";

            ComboBoxControl imgtype = GetPropertyControl<ComboBoxControl>(this.Type + "imagetype");
            imgtype.Title = LocalizationManager.Get("Image Type");
            imgtype.Items = new string[] { DEFAULT, DEFAULTPRESSED };
            imgtype.Value = ImageMax == 1 ? DEFAULT : DEFAULTPRESSED;

            ComboBoxControl comboctrl = GetPropertyControl<ComboBoxControl>(this.Type + "style");
            comboctrl.Title = LocalizationManager.Get("Style");
            comboctrl.Items = new string[] { MOMENTARY, PRESS/*, TOGGLE */};
            comboctrl.Value = Style;

            BoolControl boolctrl = GetPropertyControl<BoolControl>(this.Type + "SD");
            boolctrl.Title = LocalizationManager.Get("State Dependent");
            boolctrl.Value = IsStateDependent;
            boolctrl.Visibility = System.Windows.Visibility.Visible;
            boolctrl.Help = Help.Button_StateDependent;

            BoolControl boolctrl1 = GetPropertyControl<BoolControl>(this.Type + "EA");
            boolctrl1.Title = LocalizationManager.Get("Execute After Load");
            boolctrl1.Value = AutoStart;
            boolctrl1.Visibility = System.Windows.Visibility.Visible;
            boolctrl1.Help = Help.Button_ExecuteAfterLoad;

            BoolControl boolctrl2 = GetPropertyControl<BoolControl>(Type + "CB");
            boolctrl2.Title = LocalizationManager.Get("Colorize Button");
            boolctrl2.Value = IsColorized;
            boolctrl2.Visibility = System.Windows.Visibility.Visible;
            boolctrl2.Help = Help.Button_Colorize;

            ScriptControl control = GetPropertyControl<ScriptControl>();
            control.VerticalAlignment = System.Windows.VerticalAlignment.Stretch;
            control.Commands.Clear();
            foreach (var item in Commands)
            {
                if (item.AdditionalParameters.Count == 0)
                    for (int i = 0; i < 10; i++)
                        item.AdditionalParameters.Add(new One<string>());
                control.Commands.Add(new vMixControlButtonCommand() { IsExecutable = item.IsExecutable, UseInActiveState = item.UseInActiveState, Action = item.Action, Collapsed = item.Collapsed, Input = item.Input, InputKey = item.InputKey, Parameter = item.Parameter, StringParameter = item.StringParameter, AdditionalParameters = item.AdditionalParameters });
            }
            control.Log = Log;
            return base.GetPropertiesControls().Concat(new UserControl[] { imgctrl, imgtype, comboctrl, boolctrl, boolctrl1, boolctrl2, control }).ToArray();
        }

        public override void SetProperties(vMixWidgetSettingsViewModel viewModel)
        {
            //_blinker.Stop();
            base.SetProperties(viewModel);
            BlinkBorderColor = BorderColor;
            //_defaultBorderColor = BorderColor;
            //_blinker.Start();
        }

        public override void SetProperties(UserControl[] _controls)
        {
            Commands.Clear();
            bool hasGoToOrTimer = false;
            int p;
            int i = 0;
            foreach (var item in (_controls.OfType<ScriptControl>().First()).Commands)
            {
                Commands.Add(new vMixControlButtonCommand() {IsExecutable = item.IsExecutable, UseInActiveState = item.UseInActiveState,  Action = item.Action, Collapsed = item.Collapsed, Input = item.Input, InputKey = item.InputKey, Parameter = item.Parameter, StringParameter = item.StringParameter, AdditionalParameters = item.AdditionalParameters });

                hasGoToOrTimer |= item.Action.Function == NativeFunctions.TIMER;
                hasGoToOrTimer |= item.Action.Function == NativeFunctions.GOTO && ((int.TryParse(item.Parameter, out p) && p < i) || !int.TryParse(item.Parameter, out p));
                i++;
            }

            IsStateDependent = _controls.OfType<BoolControl>().First().Value;
            AutoStart = _controls.OfType<BoolControl>().ElementAt(1).Value;
            IsColorized = _controls.OfType<BoolControl>().ElementAt(2).Value;
            Style = (string)_controls.OfType<ComboBoxControl>().Last().Value;
            ImageMax = (string)_controls.OfType<ComboBoxControl>().First().Value == DEFAULT ? 1 : 2;

            var u = _controls.OfType<FilePathControl>().First().Value;
            /*if (!string.IsNullOrWhiteSpace(u) && File.Exists(u))
            {

                var uri = new Uri(Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), u)));
                var dir = new Uri(Directory.GetCurrentDirectory() + @"\");
                Image = Uri.UnescapeDataString(dir.MakeRelativeUri(uri).ToString());
            }
            else*/
            Image = u;

            RealUpdateActiveProperty(true, State);
            base.SetProperties(_controls);
            if (hasGoToOrTimer && Style != MOMENTARY)
            {
                var d = new Ookii.Dialogs.Wpf.TaskDialog();
                d.WindowTitle = "Possible Script Error";
                d.MainIcon = Ookii.Dialogs.Wpf.TaskDialogIcon.Warning;
                d.Content = "Your script contains TIMER or possible LOOPS!\nUse Momentary buttons for this type of scripts.";
                d.Buttons.Add(new Ookii.Dialogs.Wpf.TaskDialogButton(Ookii.Dialogs.Wpf.ButtonType.Ok));
                d.ShowDialog();
            }
        }

        public override void Update()
        {
            base.Update();
            if (AutoStart)
                ExecuteScriptCommand.Execute(null);
            RealUpdateActiveProperty();
        }

        protected override void Dispose(bool managed)
        {
            if (_disposed) return;

            if (managed)
            {
                //_blinker.Stop();
                if (_internalState != null)
                    _internalState.OnStateSynced -= State_OnStateUpdated;
                _stopThread = true;

                if (_executionWorker != null && _executionWorker.IsBusy)
                {
                    _executionWorker.CancelAsync();
                    //while (_executionWorker.CancellationPending)
                    //    Thread.Sleep(100);
                    _executionWorker.Dispose();
                }

                _activeStateUpdateWorker.Dispose();

                base.Dispose(managed);
                GC.SuppressFinalize(this);
            }
        }
    }
}
