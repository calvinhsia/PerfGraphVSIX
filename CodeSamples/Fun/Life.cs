//Desc: John Conway's game of Life: Cellular Automata using WinForms
//Desc: https://docs.microsoft.com/en-us/archive/blogs/calvin_hsia/cellular-automata-the-game-of-life
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
//Pragma: verbose = False

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


// https://docs.microsoft.com/en-us/archive/blogs/calvin_hsia/turtle-graphics-logo-program
// https://github.com/calvinhsia/HwndHost
//using hWndHost;
using System;
using System.Threading;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using PerfGraphVSIX;
using Microsoft.Test.Stress;

namespace Life
{
    public static class MyMainClass
    {
        public static void DoMain(object[] args)
        {
            var logger = args[1] as ILogger;
            var oWin = new Form1(logger);
            oWin.ShowDialog();
        }
    }
    public class Form1 : System.Windows.Forms.Form
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        [DllImport("kernel32.dll")]
        public static extern bool Beep(int freq, int dura);

        private System.ComponentModel.Container components = null;
        private TextBox m_box;
        private Button m_button;
        private Thread m_Thread = null;
        private double xScale = 1;
        private double yScale = 1;
        bool m_nReset = false;
        private ILogger _logger;
        CancellationTokenSource m_cts = new CancellationTokenSource();
        int m_numx = 200;
        int m_numy = 200;
        int m_cellx = 15;
        int m_celly = 15;
        int m_Offx = 10;
        int m_Offy = 10;
        const int MAXCOLOR = 8;
        int COLORDIV = 16; // # of gens before color changes
        bool m_fDrawAll = false;
        SolidBrush m_brushBlack = new SolidBrush(Color.Black);
        SolidBrush m_brushWhite = new SolidBrush(Color.White);
        SolidBrush[] m_brushArray = new SolidBrush[MAXCOLOR];
        Queue m_queue = new Queue();
        int[,] m_cells;
        int[,] m_diff;
        private System.Windows.Forms.Button button1;
        private int DrawCells(bool fDrawAll)
        {
            int nTotal = 0;
            Graphics g = Graphics.FromHwnd(this.Handle);
            int x, y;
            if (fDrawAll)
            {
                System.Diagnostics.Debug.Write("\nDraw");
            }
            for (x = 0; x < m_numx; x++)
            {
                for (y = 0; y < m_numy; y++)
                {
                    if (fDrawAll || m_diff[x, y] == 1)
                    {
                        if (m_cells[x, y] == 0)
                        {
                            g.FillRectangle(m_brushWhite, m_Offx + x * m_cellx, m_Offy + y * m_celly, m_cellx - 1, m_celly - 1);
                        }
                        else
                        {
                            nTotal++;
                            g.FillRectangle(m_brushArray[m_cells[x, y] / COLORDIV], m_Offx + x * m_cellx, m_Offy + y * m_celly, m_cellx - 1, m_celly - 1);
                        }
                    }
                }
            }
            return nTotal;
        }
        private void Shuffle()
        {
            int x, y;
            Random myrandom = new Random();
            for (x = 0; x < m_numx; x++)
            {
                for (y = 0; y < m_numy; y++)
                {
                    m_diff[x, y] = m_cells[x, y] = myrandom.Next(100) < 50 ? 1 : 0;
                }
            }
            DrawCells(false);
        }
        private int CountNeighbors(int x, int y)
        {
            if (x >= 0 && x < m_numx && y >= 0 && y < m_numy)
            {
                return m_cells[x, y] > 0 ? 1 : 0;
            }
            return 0;
        }
        private int Generation()
        {
            int[,] tmp = new int[m_numx, m_numy];
            int x, y;
            for (x = 0; x < m_numx; x++)
            {
                for (y = 0; y < m_numy; y++)
                {
                    tmp[x, y] = CountNeighbors(x - 1, y - 1) +
                        CountNeighbors(x - 1, y) +
                        CountNeighbors(x - 1, y + 1) +
                        CountNeighbors(x, y - 1) +
                        CountNeighbors(x, y + 1) +
                        CountNeighbors(x + 1, y - 1) +
                        CountNeighbors(x + 1, y) +
                        CountNeighbors(x + 1, y + 1);
                }
            }
            for (x = 0; x < m_numx; x++)
            {
                for (y = 0; y < m_numy; y++)
                {
                    if (tmp[x, y] == 3 && m_cells[x, y] == 0) // an empty cell with exactly 3 neighbors: 1 is born
                    {
                        m_diff[x, y] = m_cells[x, y] = 1;
                    }
                    else if (tmp[x, y] >= 2 && tmp[x, y] <= 3 && m_cells[x, y] > 0) // a live cell with 2 or more neighbors lives
                    {
                        if (m_cells[x, y] == COLORDIV * MAXCOLOR - 1)
                        {
                            m_diff[x, y] = 0; // no change
                        }
                        else
                        {
                            m_cells[x, y]++;
                            m_diff[x, y] = 1;
                        }
                    }
                    else
                    { // loneliness
                        if (m_cells[x, y] == 0) // already empty
                        {
                            m_diff[x, y] = 0; // no change
                        }
                        else
                        {
                            m_cells[x, y] = 0;
                            m_diff[x, y] = 1;
                        }
                    }
                }
            }
            if (m_queue.Count > 0)
            {
                lock (m_queue.SyncRoot)
                {
                    foreach (Point pt in m_queue)
                    {
                        m_diff[pt.X, pt.Y] = 1;
                        if (m_cells[pt.X, pt.Y] == 0)
                        {
                            m_cells[pt.X, pt.Y] = 1;
                        }
                        else
                        {
                            m_cells[pt.X, pt.Y] = 0;
                        }
                    }
                    m_queue.Clear();
                }
            }
            int nResult = DrawCells(m_fDrawAll);
            m_fDrawAll = false;
            return nResult;
        }
        private void Generations()
        {
            int nTotal;
            while (!m_cts.IsCancellationRequested)
            {
                Shuffle();
                for (int i = 0; i < 100000; i++)
                {
                    if (m_cts.IsCancellationRequested)
                    {
                        break;
                    }
                    nTotal = Generation();
                    this.Text = i.ToString() + " : " + nTotal.ToString();
                    if (m_nReset)
                    {
                        m_nReset = false;
                        break;
                    }
                }
            }
        }
        private void handlethebutton(object sender, EventArgs e)
        {
            m_nReset = true;
        }
        private void OnClick(object sender, EventArgs e)
        {
        }
        private void Onmmove(object sender, MouseEventArgs e)
        {
            Point pt = this.PointToClient(Cursor.Position);
            Point ptcell = new Point();
            ptcell.X = (pt.X - m_Offx) / m_cellx;
            ptcell.Y = (pt.Y - m_Offy) / m_celly;
            if (ptcell.X >= 0 && ptcell.X < m_numx && ptcell.Y >= 0 && ptcell.Y < m_numy)
            {
                lock (m_queue.SyncRoot)
                {
                    m_queue.Enqueue(ptcell);
                }
            }
        }
        private void OnActivated(object sender, EventArgs e)
        {
            // Generations();
            if (m_Thread == null)
            {
                m_Thread = new Thread(new ThreadStart(MyThread));
                m_Thread.IsBackground = true;
                m_Thread.Start();
            }
            else
            {
                m_fDrawAll = true;
            }
        }
        private void FormPaint(object sender, PaintEventArgs e)
        {
            m_fDrawAll = true;
        }
        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (m_Thread != null && m_Thread.IsAlive)
            {
                m_Thread.Abort();
            }
            if (disposing)
            {
                if (components != null)
                {
                    components.Dispose();
                }
            }
            base.Dispose(disposing);
        }
        #region Windows Form Designer generated code
        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.button1 = new System.Windows.Forms.Button();

            this.SuspendLayout();
            //
            // button1
            //
            this.button1.Location = new System.Drawing.Point(392, 0);
            this.button1.Name = "button1";
            this.button1.TabIndex = 0;
            this.button1.Text = "button1";
            this.button1.Click += new System.EventHandler(this.button1_Click);
            //
            // Form1
            //
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.ClientSize = new System.Drawing.Size(800, 558);
            this.Controls.AddRange(new System.Windows.Forms.Control[] {
                                                                                          this.button1});
            this.Name = "Form1";
            this.Text = "Life";
            this.Click += new System.EventHandler(this.Form1_Click);
            this.Load += new System.EventHandler(this.Form1_Load);
            this.ResumeLayout(false);
        }
        #endregion
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        //[STAThread]
        //static void Main()
        //{
        //    Application.Run(new Form1());
        //}
        private void MyThread()
        {
            try
            {
                Generations();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
            }
        }
        public Form1(ILogger logger)
        {
            _logger = logger;
            //
            // Required for Windows Form Designer support
            //
            InitializeComponent();
            using (Graphics g = Graphics.FromHwnd(this.Handle))
            {
                xScale = g.DpiX / 96;
                yScale = g.DpiY / 96;
            }
            m_numx = (int)(m_numx * xScale);
            m_numy = (int)(m_numy * yScale);
            m_cellx = (int)(m_cellx * xScale);
            m_celly = (int)(m_celly* yScale);
            m_cells = new int[m_numx, m_numy];
            m_diff = new int[m_numx, m_numy];
            this.Height = m_Offy + m_numy * m_celly;
            this.Width = m_Offx + m_numx * m_cellx;
            this.Left = 00;
            this.Top = 0;
            this.BackColor = Color.White;
            m_box = new TextBox();
            m_box.BackColor = Color.Cyan;
            m_box.Text = $"Hi {xScale} {yScale}";
            m_box.Size = new Size(100, 100);
            m_button = new Button();
            m_button.BackColor = Color.Red;
            m_button.Location = new Point(150, 00);
            m_button.Text = "&Reset";
            Controls.Add(m_button);
            m_button.Click += new EventHandler(this.handlethebutton);
            Controls.Add(m_box);
            // this.Paint += new PaintEventHandler(this.FormPaint);
            this.Activated += new EventHandler(this.OnActivated);
            this.Click += new EventHandler(this.OnClick);
            this.MouseMove += new MouseEventHandler(this.Onmmove);
            this.Closed += (o, e) =>
            {
                m_cts.Cancel();
            };
            this.Paint += new PaintEventHandler(this.FormPaint);
            for (int i = 0; i < MAXCOLOR; i++)
            {
                Color clr = Color.FromArgb(255, (MAXCOLOR - i - 1) * 32, 0, 0);
                m_brushArray[i] = new SolidBrush(clr);
            }
        }
        private void Form1_Load(object sender, System.EventArgs e)
        {
            Form.CheckForIllegalCrossThreadCalls = false;
            // Beep(1000,1000);
        }
        private void Form1_Click(object sender, System.EventArgs e)
        {
        }
        private void button1_Click(object sender, System.EventArgs e)
        {
            Generation();
        }
    }

}
