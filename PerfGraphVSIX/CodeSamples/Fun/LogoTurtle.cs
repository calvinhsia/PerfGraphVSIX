//Desc: A turtle with a pen can move around with simple commands, like Left, Forward
//Desc: It's very fast, and creates mesmerizing images
// This code will be compiled and run when you hit the ExecCode button. Any error msgs will be shown in the status log control.
// This allows you to create a stress test by repeating some code, while taking measurements between each iteration.

//  Macro substitution: %PerfGraphVSIX% will be changed to the fullpath to PerfGraphVSIX
//                      %VSRoot% will be changed to the fullpath to VS: e.g. "C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview"

//Ref: hwndhost.dll

//Ref: %PerfGraphVSIX%
//Pragma: GenerateInMemory = False
//Pragma: UseCSC = true
//Pragma: showwarnings = true
//Pragma: verbose = False

////Ref: c:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Windows.Forms.dll


//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\PresentationFramework.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\PresentationCore.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\WindowsBase.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\System.Xaml.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\System.Xml.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\System.ComponentModel.Composition.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\System.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\System.Core.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\System.Windows.Forms.dll


// https://docs.microsoft.com/en-us/archive/blogs/calvin_hsia/turtle-graphics-logo-program
// https://github.com/calvinhsia/HwndHost
//using hWndHost;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml;

namespace Logo
{
    public static class MyMainClass
    {
        public static void DoMain(object[] args)
        {
            var oWin = new MainWindow();
            oWin.ShowDialog();
        }
    }
    public class LogoControl : MyHwndHost
    {
        public Turtle _Turtle;
        public const string strLegalCommands = "flrbhpalrcdn+-";
        private string __currentCommandList = "fr+cd";
        public string _currentCmdList
        {
            get
            {
                return __currentCommandList;
            }
            set
            {
                __currentCommandList = value;
                UpdateTbxCmdList(__currentCommandList);
            }
        }
        private IntPtr _hBgd;
        private bool _fIsRunningScript;
        private Point _boundSize;
        MainWindow _mainWindow;
        public LogoControl(IntPtr hbgd, MainWindow mainWindow)
            : base(hbgd)
        {
            _hBgd = hbgd;
            _mainWindow = mainWindow;
            this._Turtle = new Turtle(this);
            UpdateTbxCmdList(_currentCmdList);
            _mainWindow._tbxCmdList.TextChanged += (ot, et) =>
            {
                {
                    __currentCommandList = _mainWindow._tbxCmdList.Text;
                }
            };
        }
        public void GotTextInput(TextCompositionEventArgs e)
        {
            if (_fIsRunningScript)
            { //any key will stop running script
                _fIsRunningScript = false;
                e.Handled = true;
            }
            else
            {
                var cmdChar = e.Text.ToLower();
                switch (cmdChar)
                {
                    case "q":
                        _mainWindow.Close();
                        break;
                    case ".":
                        var nTimes = 10000000;

                        System.Threading.ThreadPool.QueueUserWorkItem(
                            o =>
                            {
                                _fIsRunningScript = true;
                                _Turtle._fShowTurtle = false;
                                PlayBack(_currentCmdList, nTimes);
                                _fIsRunningScript = false;
                                _Turtle._fShowTurtle = true;
                            }
                            );
                        cmdChar = string.Empty;
                        break;
                    case "e": // erase/reset
                        EraseRect();
                        _currentCmdList = string.Empty;
                        _Turtle.InitVals();
                        _Turtle._fShowTurtle = true;
                        _Turtle.Draw();
                        cmdChar = string.Empty;
                        break;
                }
                if (!string.IsNullOrEmpty(cmdChar))
                {
                    _currentCmdList += cmdChar;
                    _Turtle.DoTurtleDrawCommand(cmdChar);
                }
            }
        }
        public void UpdateTbxCmdList(string strCmd)
        {
            _mainWindow._tbxCmdList.Dispatcher.Invoke(
                        () =>
                        {
                            _mainWindow._tbxCmdList.Text = strCmd;
                        }
                );
        }
        public void UpdateTbxStatus(string stat)
        {
            _mainWindow._tbxStatus.Dispatcher.Invoke(
                        () =>
                        {
                            _mainWindow._tbxStatus.Text = stat;
                        }
                );
        }
        internal void OnKey(KeyEventArgs ek)
        {
            switch (ek.Key)
            {
                case Key.Back: // backspace
                    if (!string.IsNullOrEmpty(_currentCmdList))
                    {
                        _currentCmdList = _currentCmdList.Substring(0, _currentCmdList.Length - 1);
                    }
                    ek.Handled = true;
                    break;
                case Key.Escape:
                    _mainWindow.Close();
                    break;
            }
        }

        void PlayBack(string currentCmdList, int nTimes)
        {
            int nReptCnt = 0;
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            for (int i = 0; i < nTimes; i++)
            {
                nReptCnt++;
                if (_Turtle._position.X < 0 ||
                        _Turtle._position.X > _boundSize.X ||
                        _Turtle._position.Y < 0 ||
                        _Turtle._position.Y > _boundSize.Y ||
                        nReptCnt == 20000
                    )
                {
                    EraseRect();
                    nReptCnt = 0;
                    int nangleStepSave = _Turtle._nAngleStep;
                    int nStepSizeSave = _Turtle._nStepSize;
                    _Turtle.InitVals();
                    _Turtle._nStepSize = (nStepSizeSave + 1) % 20;
                    _Turtle._nAngleStep = (nangleStepSave + 1) % 360;
                    UpdateTbxStatus("Angle= " + _Turtle._nAngleStep.ToString());
                }
                for (int j = 0; j < currentCmdList.Length; j++)
                {
                    _Turtle.DoTurtleDrawCommand(currentCmdList[j].ToString());
                    if (!_fIsRunningScript)
                    {
                        i = nTimes;
                        break;
                    }
                }
            }
            sw.Stop();
            UpdateTbxStatus(string.Format("Done in {0} secs", sw.Elapsed.TotalSeconds));
        }

        internal void OnSizeChanged()
        {
            _boundSize = new Point(
                (this.ActualWidth * xScale),
                (this.ActualHeight * yScale));
        }

        public override void OnReady(IntPtr hwnd)
        {
            if (_boundSize.X == 0)
            {
                _boundSize = new Point(
                    (this.ActualWidth * xScale),
                    (this.ActualHeight * yScale)
                );
                // send a "." command to start the default program
                Keyboard.FocusedElement.RaiseEvent(
                    new TextCompositionEventArgs(
                        InputManager.Current.PrimaryKeyboardDevice,
                        new TextComposition(InputManager.Current, Keyboard.FocusedElement, ".")
                        )
                    { RoutedEvent = TextCompositionManager.TextInputEvent }
                    );
            }
            _Turtle.OnReady(hwnd);
        }

        public int _Delay { get; set; }
    }
    public class Turtle
    {
        IntPtr _hwnd { get { return _logoControl._hwnd; } }
        LogoControl _logoControl;
        int[,] _turtle = {{0,0},   {1,1},   {1,3},  { 4,5},  { 7,3},  { 7,4},   {5,7},  {7,11},
                  {7,15},  {6,17},  {7,20},  {4,19},  {1,20},  {2,27},  {0,30} };
        public Point _position;
        const int _nTurtleSize = 2;
        public IntPtr _colorTurtle;
        public int _turtleDrawingColor = 0xffffff; // turtle pen color
        public IntPtr _penTurtleDrawingPen; // pen of turtle (CreatePen)
        public int _nAngleStep;
        public double _nAngle; //direction turtle is facing
        public bool _fPenDown;
        public int _penWidth = 1; // the width of the turtle's pen
        public int _nStepSize;
        public bool _fShowTurtle = true;
        const double piOver180 = Math.PI / 180;
        public int _nInput = 0;// user input integer

        NativeMethods.WinPoint _prevPos = new NativeMethods.WinPoint(0, 0);
        public Turtle(LogoControl logoControl)
        {
            _logoControl = logoControl;
            _colorTurtle = NativeMethods.CreatePen(0, 0, (IntPtr)0x8f00);
            InitVals();
        }
        public void InitVals()
        {
            _nStepSize = 20;
            _nAngle = -90;
            _nAngleStep = 90;
            _fPenDown = true;
            _nInput = 0;
            SetColor(0x0);
            var rect = new NativeMethods.WinRect();
            NativeMethods.GetClientRect(_hwnd, ref rect);
            _position = new Point((rect.Right - rect.Left) / 2,
                (rect.Bottom - rect.Top) / 2);
        }
        public void Draw()
        {
            if (_fShowTurtle)
            {
                IntPtr hdc = NativeMethods.GetDC(_hwnd);
                NativeMethods.SelectObject(hdc, _colorTurtle);
                var cosT = Math.Cos((90 + _nAngle) * piOver180); // cos(Theta)
                var sinT = Math.Sin((90 + _nAngle) * piOver180); // sin(Theta)
                NativeMethods.SetROP2(hdc, NativeMethods.RasterOps.R2_NOTXORPEN);
                NativeMethods.MoveToEx(hdc, (int)_position.X, (int)_position.Y, ref _prevPos);
                for (int i = 0; i < _turtle.Length / 2; i++)
                {
                    var x1 = _nTurtleSize * _turtle[i, 0];
                    var y1 = _nTurtleSize * _turtle[i, 1];
                    NativeMethods.LineTo(
                        hdc,
                        (int)(_position.X + x1 * cosT + y1 * sinT),
                        (int)(_position.Y + x1 * sinT - y1 * cosT)
                        );
                }
                // now the other half of the turtle
                NativeMethods.MoveToEx(hdc, (int)_position.X, (int)_position.Y, ref _prevPos);
                for (int i = 0; i < _turtle.Length / 2; i++)
                {
                    var x1 = -_nTurtleSize * _turtle[i, 0];
                    var y1 = -_nTurtleSize * _turtle[i, 1];
                    NativeMethods.LineTo(
                        hdc,
                        (int)(_position.X + x1 * cosT - y1 * sinT),
                        (int)(_position.Y + x1 * sinT + y1 * cosT)
                        );
                }
                var rect = new NativeMethods.WinRect();
                NativeMethods.GetClientRect(_hwnd, ref rect);
                NativeMethods.ValidateRect(_hwnd, ref rect);
                NativeMethods.SetROP2(hdc, NativeMethods.RasterOps.R2_COPYPEN);
                NativeMethods.ReleaseDC(_hwnd, hdc);
            }
        }
        public void DoTurtleDrawCommand(string cmd)
        {
            int nInputParam = _nInput;
            if (char.IsDigit(cmd[0]))
            {
                _nInput = _nInput * 10 + cmd[0] - 48;
            }
            else
            {
                _nInput = 0; // reset
                if (nInputParam == 0)
                {
                    nInputParam = 1;
                }
                Draw(); // hide turtle
                Point newpos;
                switch (cmd[0])
                {
                    case 'f': // forward
                        newpos = new Point(
                                (_position.X +
                                    Math.Cos(_nAngle * piOver180) *
                                    _nStepSize *
                                    nInputParam
                                ),
                                (_position.Y +
                                    Math.Sin(_nAngle * piOver180) *
                                    _nStepSize *
                                    nInputParam
                                )
                            );
                        if (_fPenDown)
                        {
                            var hdc = NativeMethods.GetDC(_hwnd);
                            if (_penTurtleDrawingPen == IntPtr.Zero)
                            {
                                SetColor(_turtleDrawingColor);
                            }
                            var old = NativeMethods.SelectObject(hdc, _penTurtleDrawingPen);
                            NativeMethods.MoveToEx(hdc, (int)_position.X, (int)_position.Y, ref _prevPos);
                            NativeMethods.LineTo(hdc, (int)newpos.X, (int)newpos.Y);
                            NativeMethods.ReleaseDC(_hwnd, hdc);
                        }
                        _position = newpos;
                        break;
                    case 'l': //left
                        _nAngle = (_nAngle - _nAngleStep) % 360;
                        break;
                    case 'r': //right
                        _nAngle = (_nAngle + _nAngleStep) % 360;
                        break;
                    case 'a': // angle
                        if (nInputParam == 99)
                        {
                            _nAngleStep = (new Random()).Next(100);
                        }
                        else
                        {
                            _nAngleStep += nInputParam;
                        }
                        break;
                    case 'p': // pen up/down
                        _fPenDown = !_fPenDown;
                        break;
                    case '+': // increment step size
                        if (nInputParam == 99)
                        {
                            _nStepSize = (new Random()).Next(100) - 50;
                        }
                        else
                        {
                            _nStepSize += nInputParam;
                        }
                        break;
                    case '-': // decrement step size
                        _nStepSize -= nInputParam;
                        break;
                    case 'd': // delay
                        //                        System.Threading.Thread.Sleep(nInputParam);
                        var delay = _logoControl._Delay;
                        if (delay == 0)
                        {
                            delay = nInputParam;
                        }
                        var sw = new System.Diagnostics.Stopwatch();
                        sw.Start();
                        while (sw.ElapsedTicks < delay)
                        {
                        }
                        break;
                    case 'c': // color
                        if (nInputParam == 1)
                        {
                            nInputParam = 140; // a little more pronounced
                        }
                        SetColor((int)((_turtleDrawingColor + nInputParam) & 0xffffff));
                        break;
                    case 'h': // hide
                        _fShowTurtle = !_fShowTurtle;
                        break;
                }
                Draw();
            }
        }

        void SetColor(int nColor)
        {
            {
                _turtleDrawingColor = nColor;
                if (_penTurtleDrawingPen != null)
                {
                    NativeMethods.DeleteObject(_penTurtleDrawingPen);
                }
                _penTurtleDrawingPen = NativeMethods.CreatePen(
                    0, // pen Style 
                    _penWidth,
                    (IntPtr)_turtleDrawingColor);
            }
        }
        internal void OnReady(IntPtr hwnd)
        {
            _fShowTurtle = true;
            InitVals();
            Draw();
        }
        public override string ToString()
        {
            return string.Format("{0}", _position);
        }

        internal void SetPenWidth(int p)
        {
            _penWidth = p;
            SetColor(_turtleDrawingColor);
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public LogoControl _logoControl;
        public TextBox _tbxCmdList;
        public TextBox _tbxStatus;
        public MainWindow()
        {
//            InitializeComponent();
            this.WindowState = WindowState.Maximized;
            this.Title = "Logo";
            this.Loaded += (o, e) =>
            {
                this.Top = 0;
                this.Left = 0;
                this.Width = 800;
                this.Height = 800;
                try
                {
                    //there are a lot of quotes in XAML
                    //and the C# string requires quotes to be doubled
                    var strxaml = @"        <Grid
            xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
            xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
            Name=""HwndTest"" Margin=""5,5,5,5"">
            <Grid.RowDefinitions>
                <RowDefinition></RowDefinition>
                <RowDefinition Height=""25""></RowDefinition>
            </Grid.RowDefinitions>
            <DockPanel Grid.Row=""0"">
                <UserControl Name=""MyUserControl""></UserControl>
            </DockPanel>
            <DockPanel Grid.Row=""1"">
                <TextBox 
                    Name=""tbxStatus"" 
                    HorizontalAlignment=""Left"" 
                    Height=""23"" 
                    Margin=""10,2,0,0"" 
                    IsReadOnly=""True""
                    TextWrapping=""Wrap"" 
                    VerticalAlignment=""Top"" 
                    Width=""120""/>
                <TextBox 
                    Name=""tbxCmdList"" 
                    HorizontalAlignment=""Left"" 
                    Height=""23"" 
                    Margin=""10,2,0,0"" 
                    TextWrapping=""Wrap"" 
                    VerticalAlignment=""Top"" 
                    Width=""320""/>
                <Slider 
                    Name=""sldPenWidth"" 
                    HorizontalAlignment=""Left"" 
                    Margin=""12,2,0,0"" 
                    VerticalAlignment=""Top"" 
                    Width=""100""
                    ToolTip=""Change the pen width""
                    />
                <Slider 
                    Name=""sldDelay"" 
                    HorizontalAlignment=""Left"" 
                    Margin=""12,2,0,0"" 
                    VerticalAlignment=""Top"" 
                    ToolTip=""Change the delay""
                    Width=""100""/>
                <Button 
                    Name=""btnQuit"" 
                    Content=""_Quit"" 
                    HorizontalAlignment=""Left"" 
                    Margin=""10,2,0,0"" 
                    VerticalAlignment=""Top"" 
                    Width=""55""/>

            </DockPanel>
        </Grid>
";

                    var strReader = new System.IO.StringReader(strxaml);
                    var xamlreader = XmlReader.Create(strReader);
                    var grid = (Grid)(System.Windows.Markup.XamlReader.Load(xamlreader));
                    var btnQuit = (Button)grid.FindName("btnQuit");
                    btnQuit.Click += (ob, eb) =>
                    {
                        this.Close();
                    };
                    _tbxCmdList = (TextBox)grid.FindName("tbxCmdList");
                    _tbxStatus = (TextBox)grid.FindName("tbxStatus");
                    var userCtrl = (UserControl)grid.FindName("MyUserControl");
                    var bgd = NativeMethods.CreateSolidBrush(new IntPtr(0xffffff));
                    _logoControl = new LogoControl(bgd, this);
                    userCtrl.Content = _logoControl;
                    userCtrl.Focusable = true;
                    userCtrl.IsTabStop = true;
                    this.Content = grid;
                    var sldPenWidth = (Slider)grid.FindName("sldPenWidth");
                    sldPenWidth.Minimum = 0;
                    sldPenWidth.Maximum = 400;
                    sldPenWidth.ValueChanged += (os, es) =>
                    {
                        _logoControl._Turtle.SetPenWidth((int)sldPenWidth.Value);
                    };
                    var sldDelay = (Slider)grid.FindName("sldDelay");
                    sldDelay.Minimum = 0;
                    sldDelay.Maximum = 100000;
                    sldDelay.ValueChanged += (os, es) =>
                    {
                        _logoControl._Delay = (int)sldDelay.Value;
                    };
                    this.TextInput += (ot, et) =>
                    {
                        _logoControl.GotTextInput(et);
                    };
                    userCtrl.KeyUp += (ok, ek) =>
                    {
                        _logoControl.OnKey(ek);
                    };
                    this.SizeChanged += (os, es) =>
                    {
                        _logoControl.OnSizeChanged();
                    };
                    btnQuit.ToolTip = @"Logo Program by Calvin Hsia
Logo Drawing Program by Calvin Hsia
    based on Seymour Papert's Logo
	f = Move Forward the Stepsize (*)
	r = turn Right
	l = turn Left
	p = Pen Up (off the paper) or down
	h = Hide turtle
	e = Erase and start over (prerecorded programs survive)
	+ = Increase the turtle's step size for Forward
	- = Decrease the step size
	a = Increase the angle for left/right
	. = Repeat current command indefinitely
	q = Quit out of program
	c = Change Color (*) InputValue
	d = Delay (*) 
	n = Number for User Parameter. Used for 'x'
	s = Store (*) cmd. Inputvalue indicates which storage cell (1-9)
	x = Execute stored program (*) User parameter times
	[0-9] = input integer into Input value (defaults to 1)
	[any char] while executing will stop turtle
	* = command will use the Input value for parameter
";


                }
                catch (Exception ex)
                {
                    this.Content = ex.ToString();
                }
            };
        }
    }
}
