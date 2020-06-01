//Desc: Fish vs Sharks Predator Prey Simulation
//Desc: inherits from VB hwndhost.dll
// This code will be compiled and run when you hit the ExecCode button. Any error msgs will be shown in the status log control.
// This allows you to create a stress test by repeating some code, while taking measurements between each iteration.

//  Macro substitution: %PerfGraphVSIX% will be changed to the fullpath to PerfGraphVSIX
//                      %VSRoot% will be changed to the fullpath to VS: e.g. "C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview"

//Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.8.0.dll
//Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.10.0.dll
//Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.11.0.dll
//Ref: "%VSRoot%\VSSDK\VisualStudioIntegration\Common\Assemblies\v4.0\Microsoft.VisualStudio.Shell.Interop.12.1.DesignTime.dll"
//Ref: "%VSRoot%\VSSDK\VisualStudioIntegration\Common\Assemblies\v4.0\Microsoft.VisualStudio.Shell.Interop.15.0.DesignTime.dll"
//Ref: "%VSRoot%\VSSDK\VisualStudioIntegration\Common\Assemblies\v4.0\Microsoft.VisualStudio.Shell.Interop.15.8.DesignTime.dll"
//Ref: "%VSRoot%\VSSDK\VisualStudioIntegration\Common\Assemblies\v4.0\Microsoft.VisualStudio.Threading.dll"
//Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Interop.dll
//Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.15.0.dll
//Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Shell.Framework.dll
//Ref: %VSRoot%\Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.ComponentModelHost.dll

//Ref:"%VSRoot%\Common7\IDE\PublicAssemblies\envdte.dll"
//Ref: hwndhost.dll

//Ref: %PerfGraphVSIX%
//Pragma: GenerateInMemory = False
//Pragma: UseCSC = true
//Pragma: showwarnings = true
//Pragma: verbose = false

////Ref: c:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Windows.Forms.dll

//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\PresentationFramework.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\PresentationCore.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\WindowsBase.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.Xaml.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.Xml.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.ComponentModel.Composition.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.Core.dll
//Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.Windows.Forms.dll


// https://docs.microsoft.com/en-us/archive/blogs/calvin_hsia/fish-vs-sharks-predator-prey-simulation
// https://github.com/calvinhsia/HwndHost

//using hWndHost;
using System;
using System.Windows;
using System.Text;
using System.Runtime.CompilerServices;
using System.ComponentModel;
using System.Reflection;
using System.Threading;
using System.Diagnostics;
using System.Linq;
using System.Windows.Markup;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml;

namespace Fish
{
    public static class MyMainClass
    {
        public static void DoMain(object[] args)
        {
            var oWin = new FishWindow();
            oWin.ShowDialog();
        }
    }
    public partial class FishWindow : Window
    {
        public TextBox _tbxStatus { get; set; }
        public FishWindow()
        {
            WindowState = System.Windows.WindowState.Maximized;
            this.Loaded += (ol, el) =>
            {
                this.Top = 0;
                this.Left = 0;
                try
                {
                    // Make a namespace referring to our namespace and assembly
                    // using the prefix "l:"
                    //xmlns:l=""clr-namespace:Fish;assembly=Fish"""
                    var nameSpace = this.GetType().Namespace;
                    var asm = System.IO.Path.GetFileNameWithoutExtension(
                        Assembly.GetExecutingAssembly().Location);

                    var xmlns = string.Format(
      @"xmlns:l=""clr-namespace:{0};assembly={1}""", nameSpace, asm);
                    //there are a lot of quotes (and braces) in XAML
                    //and the C# string requires quotes to be doubled
                    var strxaml =
      @"<Grid
xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
" + xmlns + // add our xaml namespace
      @" Margin=""5,5,5,5"">
<Grid.RowDefinitions>
    <RowDefinition/>
    <RowDefinition Height=""25"" />
</Grid.RowDefinitions>
<DockPanel Grid.Row=""0"">
    <Grid>
        <Grid.ColumnDefinitions>
        <ColumnDefinition Width = ""70""/>
        <ColumnDefinition/>
        </Grid.ColumnDefinitions>
        <StackPanel Name=""inputPanel"" 
            Orientation=""Vertical"" 
            >
            <CheckBox Content=""_Running"" 
                IsChecked= ""{Binding Path=IsRunning}"" />
            <CheckBox Content=""_Circles"" 
                IsChecked= ""{Binding Path=UseCircles}"" />
            <CheckBox Content=""_Action1PerYear"" 
                IsChecked= ""{Binding Path=_OneActionPerYear}""
                ToolTip=""Can do only one of move AND eat AND give birth in same year?""
                />

            <CheckBox Content=""_Torus"" 
                IsChecked= ""{Binding Path=_Torus}""
                ToolTip=""The ocean wraps around the edges: the top and the bottom are neighbors""
                />

            <Label Content=""ColorGrad""/>
            <l:MyTextBox 
                Text =""{Binding Path=ColorAgeGradient}"" 
                ToolTip=""As an animcal gets older, darken the color 
(256 is max color). 0 makes it go much faster"" />
            <Label Content=""CellWidth""/>
            <l:MyTextBox 
                Text =""{Binding Path=CellWidth}"" 
                ToolTip=""Width of a cell"" />
            <Label Content=""CellHeight""/>
            <l:MyTextBox 
                Text =""{Binding Path=CellHeight}"" 
                ToolTip=""Height of a cell"" />

            <Label Content=""Starve""/>
            <l:MyTextBox 
                Text =""{Binding Path=_SharkStarve}"" 
                ToolTip=""How long a shark can go without eating"" />

            <Label Content=""FishBreed""/>
            <l:MyTextBox 
                Text =""{Binding Path=_FishBreedAge}"" 
                ToolTip=""How old a Fish has to be before it can give birth
(requires neighboring empty cell)"" />
            <Label Content=""SharkBreed""/>
            <l:MyTextBox 
                Text =""{Binding Path=_SharkBreedAge}"" 
                ToolTip=""How old a Shark has to be before it can give birth 
(requires neighboring empty cell)"" />
            <Label Content=""FishLifeLength""/>
            <l:MyTextBox 
                Text =""{Binding Path=_FishLifeLength}"" 
                ToolTip=""How old a Fish is before it dies of old age"" />
            <Label Content=""SharkLifeLength""/>
            <l:MyTextBox 
                Text =""{Binding Path=_SharkLifeLength}"" 
                ToolTip=""How old a Shark is before it dies of old age"" />
            <Label Content=""Rows""/>
            <TextBlock
                Text =""{Binding Path=nTotRows}"" 
                />
            <Label Content=""Cols""/>
            <TextBlock
                Text =""{Binding Path=nTotCols}"" 
                />
                

        </StackPanel>
        <UserControl Name=""MyUserControl"" Grid.Column=""1""></UserControl>
    </Grid>
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
        Width=""420""/>
    <Slider 
        HorizontalAlignment=""Left"" 
        Minimum=""0""
        Maximum=""1000""
        Margin=""12,2,0,0"" 
        Value=""{Binding Path=_nDelay}""
        VerticalAlignment=""Top"" 
        ToolTip=""Change the delay""
        Width=""100""/>
    <Button 
        Name=""btnGraph"" 
        Content=""_Graph"" 
        HorizontalAlignment=""Left""
        Margin=""10,2,0,0"" 
        VerticalAlignment=""Top"" 
        ToolTip=""Write to Excel.Hit these keys 'Alt N, N, Enter'
Alt activates Menu
N(Insert) (N Line chart) Enter (choose default 2D line chart)""
        Width=""55""/>
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
                    var bgdOcean = NativeMethods.CreateSolidBrush(
                        new IntPtr(0xffffff));
                    var fishBowl = new FishBowl(this, bgdOcean);

                    var strReader = new System.IO.StringReader(strxaml);
                    var xamlreader = XmlReader.Create(strReader);
                    var grid = (Grid)(XamlReader.Load(xamlreader));
                    grid.DataContext = fishBowl;

                    _tbxStatus = (TextBox)grid.FindName("tbxStatus");
                    var btnQuit = (Button)grid.FindName("btnQuit");
                    btnQuit.Click += (ob, eb) =>
                    {
                        this.Close();
                    };
                    this.Closing += (o, e) =>
                    {
                        FishBowl._Instance = null;
                    };
                    var btnGraph = (Button)grid.FindName("btnGraph");
                    btnGraph.Click += (ob, eb) =>
                    {
                        var oldIsRunning = fishBowl.IsRunning;
                        try
                        {
                            fishBowl.IsRunning = false;
                            // write to Excel
                            // Hit these keys "Alt N, N, Enter"
                            //  Alt activates Menu
                            // N(Insert) (N Line chart) Enter (choose default 2D line chart)

                            var tempFileName = System.IO.Path.ChangeExtension(
                                System.IO.Path.GetTempFileName(),
                                "csv");
                            System.IO.File.WriteAllText(
                                tempFileName,
                                FishBowl._Instance._sbStatus.ToString());
                            System.Diagnostics.Process.Start(tempFileName);
                        }
                        catch (Exception)
                        {
                            // if file in use by Excel, just ignore
                        }
                        if (oldIsRunning)
                        {
                            fishBowl.IsRunning = true;
                        }
                    };
                    _tbxStatus = (TextBox)grid.FindName("tbxStatus");
                    var userCtrl = (UserControl)grid.FindName("MyUserControl");
                    userCtrl.Content = fishBowl;
                    this.Content = grid;
                    this.SizeChanged += (os, es) =>
                    {
                        fishBowl.OnSizeChanged();
                    };
                    btnQuit.ToolTip = @"Fish vs Sharks Program by Calvin Hsia
Per A.K. Dewdney article on Wator, December 1984 Scientific American
";

                }
                catch (Exception ex)
                {
                    this.Content = ex.ToString();
                }
            };
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!e.Handled)
            {
                FishBowl._Instance.onMouseMove(e);
            }
        }
    }
    // a textbox that selects all when focused:
    public class MyTextBox : TextBox
    {
        public MyTextBox()
        {
            this.GotFocus += (o, e) =>
            {
                this.SelectAll();
            };
        }
    }

    public class FishBowl : MyHwndHost, INotifyPropertyChanged
    {
        public StringBuilder _sbStatus;
        FishWindow _wndParent;
        public static FishBowl _Instance;
        bool _ResetRequired;
        bool _StopRequested;
        public Random _random = new Random(1);
        public IntPtr _hdc;
        public Cell[,] _cells;


        // Calculated values:
        private int _nTotRows;
        private int _nTotCols;
        public int nTotRows
        {
            get { return _nTotRows; }
            set { if (_nTotRows != value) { _nTotRows = value; RaisePropChanged(); } }
        }
        public int nTotCols
        {
            get { return _nTotCols; }
            set { if (_nTotCols != value) { _nTotCols = value; RaisePropChanged(); } }
        }

        public int _nCurrentSharks;
        public int _nCurrentFish;
        public int _nYearCurrent; // how many generations

        public IntPtr _BgdBrush;
        public int _nDelay { get; set; }

        bool _UseCircles;
        public bool UseCircles
        {
            get { return _UseCircles; }
            set { _ResetRequired = true; _UseCircles = value; EraseRect(); }
        }

        public bool _OneActionPerYear { get; set; }

        public bool _Torus { get; set; }

        bool _IsRunning = false;
        public bool IsRunning
        {
            get { return _IsRunning; }
            set
            {
                if (_IsRunning != value)
                {
                    if (value) // if we're starting
                    {
                        if (_StopRequested)
                        {
                            while (_StopRequested)
                            {
                                Thread.Sleep(100);
                                //System.Windows.Application.Current.Dispatcher.Invoke(
                                //    DispatcherPriority.Background,
                                //    new System.Threading.ThreadStart(() => System.Threading.Thread.Sleep(1)));
                            }
                        }
                        if (_nCurrentFish == 0 || _nCurrentSharks == 0)
                        {
                            _ResetRequired = true;
                        }
                        if (_ResetRequired)
                        {
                            InitWorld(null, null);
                        }
                        _StopRequested = false;
                        ThreadPool.QueueUserWorkItem((o) =>
                        {
                            try
                            {
                                _IsRunning = true;
                                _hdc = NativeMethods.GetDC(_hwnd);
                                InitCachedImagesOfAnimals();
                                while (!_StopRequested)
                                {
                                    DoGenerations();
                                }
                                _StopRequested = false; // indicate no pending stop
                                NativeMethods.ReleaseDC(_hwnd, _hdc);
                                var rect = new NativeMethods.WinRect();
                                NativeMethods.GetClientRect(_hwnd, ref rect);
                                NativeMethods.ValidateRect(_hwnd, ref rect);
                            }
                            catch (Exception)
                            {
                            }
                        }
                        );
                    }
                    else
                    {// we're stopping
                        _StopRequested = true;
                    }
                    _IsRunning = value;
                    RaisePropChanged();
                }
            }
        }

        int _CellWidth;
        public int CellWidth
        {
            get { return _CellWidth; }
            set { _CellWidth = value; _ResetRequired = true; }
        }

        int _CellHeight;
        public int CellHeight
        {
            get { return _CellHeight; }
            set { _CellHeight = value; _ResetRequired = true; }
        }
        public int _SharkInitPct { get; set; }
        public int _FishInitPct { get; set; }
        int _colorAgeGradient;
        public int ColorAgeGradient
        {
            get { return _colorAgeGradient; }
            set { _colorAgeGradient = value; _ResetRequired = true; }
        }
        public int _FishBreedAge { get; set; }
        public int _SharkBreedAge { get; set; }
        public int _FishLifeLength { get; set; }
        public int _SharkLifeLength { get; set; }
        public int _SharkStarve { get; set; }
        public int _FishNumMovesPerYear { get; set; }
        public int _SharkNumMovesPerYear { get; set; }

        public FishBowl(FishWindow wndParent, IntPtr hBgd)
          : base(hBgd)
        {
            //changing these requires resetting the fishbowl
            _colorAgeGradient = 1;
            _FishInitPct = 10;
            _SharkInitPct = 10;

            CellWidth = CellHeight = 10;
            //these can be changed while running
            _SharkStarve = 6;
            _FishBreedAge = 3;
            _SharkBreedAge = 5;
            _FishLifeLength = 22;
            _SharkLifeLength = 33;

            _FishNumMovesPerYear = 1;
            _SharkNumMovesPerYear = 1;

            _wndParent = wndParent;
            _Instance = this;
            _BgdBrush = hBgd;
            _wndParent.Closing += (o, e) =>
            {
                _StopRequested = true;
                FishBowl._Instance = null;
            };
        }
        public void InitWorld(object sender, RoutedEventArgs e)
        {
            IsRunning = false;
            _sbStatus = new StringBuilder();
            _sbStatus.AppendLine("Fish,Sharks");
            nTotRows = (int)(this.ActualHeight * yScale / CellHeight);
            nTotCols = (int)(this.ActualWidth * xScale / CellWidth);
            _cells = new Cell[nTotRows, nTotCols];
            _nCurrentFish = _nCurrentSharks = 0;
            _nYearCurrent = 0;
            for (var row = 0; row < nTotRows; row++)
            {
                for (var col = 0; col < nTotCols; col++)
                {
                    _cells[row, col] = new Cell(row, col);
                }
            }
            EraseRect();

            _ResetRequired = false;
        }
        public IntPtr _hdcCached;
        void InitCachedImagesOfAnimals()
        {
            if (_hdcCached != IntPtr.Zero)
            {
                NativeMethods.DeleteDC(_hdcCached);
            }
            _hdcCached = NativeMethods.CreateCompatibleDC(_hdc);
            var hBitmap = NativeMethods.CreateCompatibleBitmap(_hdc,
              nTotRows * CellWidth,
              nTotCols * CellHeight);
            NativeMethods.SelectObject(_hdcCached, hBitmap);
            IntPtr br;
            var rect = new NativeMethods.WinRect();
            for (int i = 0; i < 255; i++) // for each age
            {
                rect.Left = i * _CellWidth;
                rect.Right = rect.Left + _CellWidth;
                rect.Top = 0;
                rect.Bottom = _CellHeight;
                br = NativeMethods.CreateSolidBrush(new IntPtr(0xff00 - i * 0x100));
                NativeMethods.FillRect(_hdcCached, ref rect, br);
                NativeMethods.DeleteObject(br);
                br = NativeMethods.CreateSolidBrush(new IntPtr(0xff - i));
                rect.Top = _CellHeight;
                rect.Bottom = rect.Top + _CellHeight;
                NativeMethods.FillRect(_hdcCached, ref rect, br);
                NativeMethods.DeleteObject(br);
            }
            // now draw an empty cell
            rect.Top += _CellHeight;
            rect.Bottom = rect.Top + _CellHeight;
            rect.Left = 0;
            rect.Right = _CellWidth;
            br = NativeMethods.CreateSolidBrush(new IntPtr(0xffffff));
            NativeMethods.FillRect(_hdcCached, ref rect, br);
            NativeMethods.DeleteObject(br);
            NativeMethods.DeleteObject(hBitmap);
        }
        void DoGenerations()
        {
            _nYearCurrent++;
            if (_nCurrentFish > 0 || _nCurrentSharks > 0)
            {
                _wndParent._tbxStatus.Dispatcher.Invoke(
                    () =>
                    {
                        if (!_StopRequested)
                        {
                            _wndParent._tbxStatus.Text = this.ToString();
                            _sbStatus.AppendFormat("{0},{1}\n",
                        _nCurrentFish,
                        _nCurrentSharks);
                        }
                    }
                );
            }
            // first, for each cell, determine what happens:
            // animals can move to adjacent empty cells
            // randomly alternate between scanning (top-bottom/left to right) and backwards
            var forward = _random.Next(100) < 50;
            var rowStart = 0;
            var colStart = 0;
            var rowEnd = nTotRows;
            var colEnd = nTotCols;
            var rowInc = 1;
            var colInc = 1;
            if (!forward)
            {
                rowInc = colInc = -1;
                colEnd = rowEnd = -1;
                rowStart = nTotRows - 1;
                colStart = nTotCols - 1;
            }
            for (var row = rowStart; row != rowEnd; row += rowInc)
            {
                for (var col = colStart; col != colEnd; col += colInc)
                {
                    _cells[row, col].CalcGeneration();
                    // we can't draw yet, because other cells could affect us
                }
            }
            //Validate();
            foreach (var cell in _cells)
            {
                cell.Draw();
                if (_StopRequested)
                {
                    return; // fast exit. Draw is much slower than Calc
                }
            }
            if (_nDelay > 0)
            {
                Thread.Sleep(_nDelay);
            }
        }
        public void Validate()
        {
            int nSharks = 0;
            int nFish = 0;
            foreach (var cell in _cells)
            {
                if (cell._animal is fish)
                {
                    nFish++;
                }
                if (cell._animal is shark)
                {
                    nSharks++;
                }
            }
            Debug.Assert(nFish == _nCurrentFish, "# fish mismatch");
            Debug.Assert(nSharks == _nCurrentSharks, "#sharks mismatch");
        }
        bool _DidItAlready = false;
        public override void OnReady(IntPtr hwnd)
        {
            _ResetRequired = true;
            if (!_DidItAlready)
            { // avoid deadlock while changing DPI
                _DidItAlready = true;
                //                IsRunning = false;
                {
                    IsRunning = true;
                    // called on resize too
                    //InitWorld(null, null);
                    //StartTheSimulation();
                }
            }
        }

        internal void OnSizeChanged()
        {

        }
        // get neighboring cells in random order
        // edges wrap around
        int[] _RandOrder = { 0, 1, 2, 3 };
        public List<Cell> GetRandomNeighbors(Cell rootCell)
        {
            var res = new List<Cell>();
            // first we shuffle the direction
            for (int i = 0; i < _RandOrder.Length; i++)
            {
                int tmp = _RandOrder[i];
                int j = _random.Next(_RandOrder.Length);
                _RandOrder[i] = _RandOrder[j];
                _RandOrder[j] = tmp;
            }
            for (int i = 0; i < _RandOrder.Length; i++)
            {
                int nRow = rootCell._nRow;
                int nCol = rootCell._nCol;
                switch (_RandOrder[i])
                {
                    case 0: // North
                        if (nRow == 0)
                        {
                            if (_Torus)
                            {
                                nRow = _nTotRows - 1;
                            }
                            else
                            {
                                continue;
                            }
                        }
                        else
                        {
                            nRow--;
                        }
                        break;
                    case 1: // South
                        if (nRow >= nTotRows - 1)
                        {
                            if (_Torus)
                            {
                                nRow = 0;
                            }
                            else
                            {
                                continue;
                            }
                        }
                        else
                        {
                            nRow++;
                        }
                        break;
                    case 2: // West
                        if (nCol == 0)
                        {
                            if (_Torus)
                            {
                                nCol = _nTotCols - 1;
                            }
                            else
                            {
                                continue;
                            }
                        }
                        else
                        {
                            nCol--;
                        }
                        break;
                    case 3: // East
                        if (nCol >= nTotCols - 1)
                        {
                            if (_Torus)
                            {
                                nCol = 0;
                            }
                            else
                            {
                                continue;
                            }
                        }
                        else
                        {
                            nCol++;
                        }
                        break;
                }
                res.Add(_cells[nRow, nCol]);
            }
            return res;
        }
        public override string ToString()
        {
            return string.Format("Gen= {0} #Fish={1} #Sharks={2}",
                _nYearCurrent,
                _nCurrentFish,
                _nCurrentSharks);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        void RaisePropChanged([CallerMemberName] string propName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
            }
        }

        internal Cell GetCellFromCoordinates(double x, double y)
        {
            Cell result = null;
            if (x > 0 && y > 0)
            {
                int row, col;
                col = (int)(x * xScale / FishBowl._Instance.CellWidth);
                row = (int)(y * yScale / FishBowl._Instance.CellHeight);
                if (col < nTotCols && row < nTotRows)
                {
                    result = _cells[row, col];
                }
            }
            return result;
        }
        internal void onMouseMove(MouseEventArgs e)
        {
            var pos = e.GetPosition(this);
            var cell = GetCellFromCoordinates(pos.X, pos.Y);
            if (cell != null)
            {
                if (Mouse.LeftButton == MouseButtonState.Pressed)
                {
                    cell._MouseButton = MouseButton.Left;
                }
                else
                {
                    if (Mouse.RightButton == MouseButtonState.Pressed)
                    {
                        cell._MouseButton = MouseButton.Right;
                    }
                }
            }
        }
    }
    public class Cell
    {
        public int _nRow;
        public int _nCol;
        public MouseButton? _MouseButton;
        IntPtr _priorBrush = IntPtr.Zero;
        NativeMethods.WinRect _winRect
        {
            get
            {
                var rect = new NativeMethods.WinRect(
                    FishBowl._Instance.CellWidth * _nCol,
                    FishBowl._Instance.CellHeight * _nRow,
                    FishBowl._Instance.CellWidth * (_nCol + 1),
                    FishBowl._Instance.CellHeight * (_nRow + 1)
                    );
                return rect;
            }
        }
        public animal _animal; // null is empty, else fish or shark
        public Cell(int nRow, int nCol)
        {
            _nRow = nRow;
            _nCol = nCol;
            var nRand = FishBowl._Instance._random.Next(100);
            if (nRand < FishBowl._Instance._FishInitPct)
            {
                _animal = new fish();
            }
            else
            {
                nRand -= FishBowl._Instance._FishInitPct;
                if (nRand < FishBowl._Instance._SharkInitPct)
                {
                    _animal = new shark();
                }
                else
                {
                    _animal = null; // no animal
                }
            }
        }

        public void KillCurrentAnimal()
        {
            if (_animal != null)
            {
                if (_animal is fish)
                {
                    FishBowl._Instance._nCurrentFish--;
                }
                else
                {
                    FishBowl._Instance._nCurrentSharks--;
                }
                _animal = null;
            }
        }
        public void CalcGeneration()
        {// see what happends to this cell
            if (_MouseButton.HasValue)
            {
                var button = _MouseButton;
                _MouseButton = null;
                {
                    var baby = Activator.CreateInstance(
                      button == MouseButton.Left ? typeof(fish) : typeof(shark)
                      );
                    KillCurrentAnimal();
                    _animal = (animal)baby;
                    _animal._nDateOfLastChildBirth = FishBowl._Instance._nYearCurrent;
                    _animal._nDateOfLastAction = FishBowl._Instance._nYearCurrent;
                    _priorBrush = IntPtr.Zero; // recalc color for cur cell
                    return;
                }
            }
            // is it a shark? does it die of starvation?
            if (_animal != null && !_animal.Survives())
            {
                KillCurrentAnimal();
            }
            if (_animal != null
                && (FishBowl._Instance._OneActionPerYear ?
                !_animal.DidSomethingInCurrentYear : // moved in current yr
                true)
                )
            { // if still alive
                var neighborCells = FishBowl._Instance.GetRandomNeighbors(this);
                // sharks eat 1 adjacent fish
                bool fDidEat = false;
                if (_animal is shark)
                {
                    // find first fish in neighbors
                    var foodCell = neighborCells.Where(
                        c => c._animal is fish
                         && c._animal.Age > 0 // don't eat babies
                        ).FirstOrDefault();
                    if (foodCell != null)
                    {// eat the fish! and move shark to where food was
                        _animal._nDateOfLastMeal =
                                FishBowl._Instance._nYearCurrent;
                        foodCell._animal = _animal;
                        _animal._nDateOfLastAction = FishBowl._Instance._nYearCurrent;
                        _priorBrush = IntPtr.Zero; // recalc color for cur cell
                        FishBowl._Instance._nCurrentFish--;
                        fDidEat = true;
                        if (_animal.CanBreed) // replace current cell with baby cuz mama moved
                        {
                            _animal._nDateOfLastAction = FishBowl._Instance._nYearCurrent;
                            var baby = Activator.CreateInstance(_animal.GetType());
                            foodCell._animal._nDateOfLastChildBirth = FishBowl._Instance._nYearCurrent;
                            _animal = (animal)baby;
                        }
                        else
                        {
                            _animal = null; // shark moved, so current cell empty
                        }
                    }
                }
                if (!fDidEat) // if cell still occupied by original animal (didn't move)
                {
                    var emptyNeighborCell = neighborCells.Where(
                        c => c._animal == null
                        ).FirstOrDefault();
                    if (emptyNeighborCell != null)
                    {
                        // Breeding
                        if (_animal.CanBreed)
                        {
                            // old enough to breed: have a baby
                            // 100% fertility rate
                            var baby = Activator.CreateInstance(_animal.GetType());
                            emptyNeighborCell._animal = (animal)baby;
                            _animal._nDateOfLastChildBirth = FishBowl._Instance._nYearCurrent;
                            _animal._nDateOfLastAction = FishBowl._Instance._nYearCurrent;
                            _priorBrush = IntPtr.Zero; // recalc color for cur cell
                        }
                        else
                        {// can't breed AND move
                            var nMoves = _animal.NumMovesPerGeneration;
                            for (int i = 0; i < nMoves; i++)
                            {
                                // moving to neighbor cell
                                _animal._nDateOfLastAction = FishBowl._Instance._nYearCurrent;
                                emptyNeighborCell._animal = _animal;
                                _animal = null;
                                _priorBrush = IntPtr.Zero; // recalc color for cur cell
                                if (i + 1 < nMoves)
                                { // could move back to original cell
                                    neighborCells = FishBowl._Instance.GetRandomNeighbors(this);
                                    emptyNeighborCell = neighborCells.Where(
                                        c => c._animal == null
                                        ).FirstOrDefault();
                                    if (emptyNeighborCell == null)
                                    {
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        static IntPtr _penBlank = IntPtr.Zero;
        public void Draw()
        {
            /*
            var ypos = FishBowl._Instance.CellHeight * 2;
            var xpos = 0;
            if (_animal != null)
            {
              ypos = _animal is fish ? 0 : FishBowl._Instance.CellHeight;
              xpos = _animal.Age * FishBowl._Instance.CellWidth;
            }
            NativeMethods.BitBlt(FishBowl._Instance._hdc,
              _winRect.Left,
              _winRect.Top,
              FishBowl._Instance.CellWidth,
              FishBowl._Instance.CellHeight,
              FishBowl._Instance._hdcCached,
              xpos,
              ypos,
              NativeMethods.SRCCOPY
              );
            /*/
            IntPtr clrBrush = FishBowl._Instance._BgdBrush;
            if (_animal != null)
            {
                clrBrush = _animal.GetColorFromAge();
            }
            if (clrBrush != _priorBrush)
            {
                var rect = _winRect;
                if (FishBowl._Instance.UseCircles)
                {
                    if (_penBlank == IntPtr.Zero)
                    {
                        _penBlank = NativeMethods.CreatePen(
                            0, // pen Style 
                            0,
                            new IntPtr(0xffffff)// color
                            );
                    }
                    // get a pen for the outline and brush for the inside
                    NativeMethods.SelectObject(FishBowl._Instance._hdc, _penBlank);
                    NativeMethods.SelectObject(FishBowl._Instance._hdc, clrBrush);
                    NativeMethods.Ellipse(
                        FishBowl._Instance._hdc,
                        rect.Left,
                        rect.Top,
                        rect.Right,
                        rect.Bottom);
                }
                else
                {
                    NativeMethods.FillRect(FishBowl._Instance._hdc, ref rect, clrBrush);
                }
                _priorBrush = clrBrush;
            }
            //*/
        }
        public override string ToString()
        {
            return string.Format("({0},{1}) {2}",
                _nRow,
                _nCol,
                _animal != null ? _animal.ToString() : string.Empty);
        }
    }
    public abstract class animal
    {
        public int _nDateOfBirth;
        public int _nDateOfLastChildBirth;
        public int _nDateOfLastAction;
        public int _nDateOfLastMeal;
        public int Age
        {
            get { return (FishBowl._Instance._nYearCurrent - _nDateOfBirth); }
        }
        public abstract int BreedAge { get; }
        public abstract bool Survives();
        public abstract int NumMovesPerGeneration { get; }
        public abstract IntPtr GetColorFromAge();

        public animal()
        {
            _nDateOfBirth = _nDateOfLastAction = FishBowl._Instance._nYearCurrent;
        }
        public virtual bool CanBreed { get { return Age > BreedAge; } }

        public virtual bool DidSomethingInCurrentYear
        {
            get { return _nDateOfLastAction == FishBowl._Instance._nYearCurrent; }
        }
        protected IntPtr CalcColor(IntPtr[] brushTable, int baseColor, int colorMult)
        {
            int ndxToUse = 0; ;
            var coloradj = Math.Min(Age * FishBowl._Instance.ColorAgeGradient, 255);
            if (coloradj > 0)
            {
                coloradj *= colorMult;
                ndxToUse = Math.Min(Age, 255);
            }
            var clr = baseColor - coloradj;
            if (brushTable[ndxToUse] == IntPtr.Zero)
            {
                brushTable[ndxToUse] = NativeMethods.CreateSolidBrush((IntPtr)clr);
            }
            return brushTable[ndxToUse];
        }
        public override string ToString()
        {
            return string.Format("{0} DOB={1}", this.GetType().Name, _nDateOfBirth);
        }
    }
    public class fish : animal
    {
        static IntPtr[] _brushes = new IntPtr[256];
        public fish()
        {
            FishBowl._Instance._nCurrentFish++;
        }
        public override int BreedAge { get { return FishBowl._Instance._FishBreedAge; } }
        public override int NumMovesPerGeneration
        {
            get { return FishBowl._Instance._FishNumMovesPerYear; }
        }
        public override IntPtr GetColorFromAge()
        {
            // 0x00BBGGRR
            return CalcColor(_brushes, baseColor: 0xff00, colorMult: 256);
        }
        public override bool Survives()
        {
            var survives = Age <= FishBowl._Instance._FishLifeLength;
            return survives;
        }
    }
    public class shark : animal
    {
        static IntPtr[] _brushes = new IntPtr[256];

        public shark()
        {
            FishBowl._Instance._nCurrentSharks++;
            _nDateOfLastMeal = FishBowl._Instance._nYearCurrent;
        }
        public override bool Survives()
        {
            var survives = Age < FishBowl._Instance._SharkLifeLength;
            if (survives &&
                FishBowl._Instance._nYearCurrent - _nDateOfLastMeal >= FishBowl._Instance._SharkStarve
                )
            {
                survives = false;
            }
            return survives;
        }
        public override int BreedAge { get { return FishBowl._Instance._SharkBreedAge; } }
        public override int NumMovesPerGeneration
        {
            get { return FishBowl._Instance._SharkNumMovesPerYear; }
        }
        public override IntPtr GetColorFromAge()
        {
            // 0x00BBGGRR
            return CalcColor(_brushes, baseColor: 0xff, colorMult: 1);
        }
    }

}
