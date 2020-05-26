
'Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\PresentationFramework.dll
'Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\PresentationCore.dll
'Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\WindowsBase.dll
'Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.Windows.Forms.dll
'Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.Xaml.dll
'Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.Xml.dll

'Pragma: verbose=false
#If False
'Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.Xaml.dll
'Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\System.Xml.dll


#End If


' https://docs.microsoft.com/en-us/archive/blogs/calvin_hsia/you-can-use-hwndhost-to-host-a-win32-hwnd-window-inside-a-wpf-element
' https://github.com/calvinhsia/HwndHost

Option Strict On

Imports System.Runtime.InteropServices
Imports System.Threading
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Input

Public Module Main
    Sub DoMain(args As Object())
        Dim owin = New MainWindow
        owin.ShowDialog

    End Sub
End Module

Class MainWindow
    Inherits System.Windows.Window
    Dim _MovingBalls As MovingBalls
    Sub Load() Handles MyBase.Loaded
        Try
            Me.Width = 600
            Me.Height = 600
            AddHandler Me.SizeChanged,
                Sub()
                    If _MovingBalls IsNot Nothing Then
                        _MovingBalls.OnSizeChanged()
                    End If
                End Sub

            Me.Content = CreateUIElem("Button", "Bisque")
        Catch ex As Exception
            Me.Content = ex.ToString
        End Try
    End Sub

    Function CreateUIElem(
                         UIElemName As String,
                         BackGround As String) As UIElement
        Dim xaml =
        <Grid
            xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
            Name="HwndTest" Margin="5,5,5,5">
            <Grid.RowDefinitions>
                <RowDefinition Height="25"></RowDefinition>
                <RowDefinition></RowDefinition>
            </Grid.RowDefinitions>
            <DockPanel Grid.Row="0">
                <Button
                    Content="_Quit"
                    Name="btnQuit"
                    HorizontalAlignment="Left"
                    Margin="10,2,0,0"
                    VerticalAlignment="Top"
                    Width="75"/>
                <TextBox
                    Name="tbx"
                    HorizontalAlignment="Left"
                    Height="23"
                    Margin="10,2,0,0"
                    TextWrapping="Wrap"
                    Text="TextBox"
                    VerticalAlignment="Top"
                    Width="120"/>
                <Slider
                    Name="Slider"
                    HorizontalAlignment="Left"
                    Margin="12,2,0,0"
                    VerticalAlignment="Top"
                    Width="200"/>

            </DockPanel>
            <DockPanel Grid.Row="1">
                <UserControl Name="MyUserControl"></UserControl>
            </DockPanel>

        </Grid>
        Dim grid = CType(Markup.XamlReader.Load(xaml.CreateReader), Grid)
        Dim btnQuit = CType(grid.FindName("btnQuit"), Button)
        AddHandler btnQuit.Click, Sub() Me.Close()

        Dim slider = CType(grid.FindName("Slider"), Slider)
        Dim tbx = CType(grid.FindName("tbx"), TextBox)
        slider.Minimum = 0
        slider.SmallChange = 1
        slider.Maximum = 200
        slider.TickPlacement = Primitives.TickPlacement.BottomRight
        slider.TickFrequency = 5
        AddHandler slider.ValueChanged,
          Sub()
              tbx.Text = slider.Value.ToString
              _MovingBalls._nBalls = CInt(slider.Value)
              _MovingBalls._doInit = True
          End Sub

        Dim userConrtol = CType(grid.FindName("MyUserControl"), UserControl)
        _MovingBalls = New MovingBalls(Me)
        userConrtol.Content = _MovingBalls
        Return grid
    End Function

    Protected Overrides Sub OnKeyDown(e As System.Windows.Input.KeyEventArgs)
        _MovingBalls.OnKey(e)
    End Sub
End Class

Public Class MovingBalls
    Inherits MyHwndHost
    Public _balls As Ball()
    Public _ballSpeed As Integer = 10
    Public _nBalls As Integer = 100
    Public _rand As New Random(1)
    Public _doErase As Boolean = False
    Public IsClosing As Boolean = False
    Dim tmr As Timers.Timer
    Dim _wParent As Window

    Dim _boundSize As Point
    Public _doInit As Boolean = True

    Public Sub New(wParent As Window)
        MyBase.New(CreateSolidBrush(CType(&HF88080, IntPtr))) ' Blue
        _wParent = wParent
        AddHandler wParent.Closing,
        Sub()
            IsClosing = True
        End Sub
    End Sub
    Dim fDidStart As Boolean = False
    Public Overrides Sub OnReady(hwnd As IntPtr)
        If Not fDidStart Then
            fDidStart = True
            ThreadPool.QueueUserWorkItem(
          Sub()
              While Not IsClosing
                  If _doInit Then
                      _doInit = False
                      InitBalls()
                  End If
                  Dim hDC = GetDC(_hwnd) ' get device context
                  SelectObject(hDC, _hbrBackground)
                  For i = 0 To _nBalls - 1
                      Dim ball = _balls(i)
                      If ball Is Nothing Then
                          Exit For
                      End If
                      ' first we draw over the old ball with the back color to erase it
                      If _doErase Then
                          ball.Erase(hDC, _hbrBackground)
                      End If

                      ' now we detect wall collision
                      If ball._Position.X >= Me._boundSize.X OrElse
                        ball._Position.X < 0 Then
                          ball._Speed.X = -ball._Speed.X
                      End If
                      If ball._Position.Y > Me._boundSize.Y OrElse
                        ball._Position.Y < 0 Then
                          ball._Speed.Y = -ball._Speed.Y
                      End If
                      ' now we move the ball
                      ball._Position.X += ball._Speed.X
                      ball._Position.Y += ball._Speed.Y
                      ' now we draw the ball in the new position
                      ball.Draw(hDC)
                  Next
                  ReleaseDC(_hwnd, hDC)
              End While
          End Sub)
        End If
    End Sub

    Dim curColor As Integer = &HFFFFFF
    Public Sub InitBalls()
        If _boundSize.X = 0 Then
            _boundSize.X = CInt(Me.ActualWidth * xScale)
            _boundSize.Y = CInt(Me.ActualHeight * yScale)
        End If
        If _balls IsNot Nothing Then
            For Each b In _balls
                If b IsNot Nothing Then
                    b.Dispose()
                End If
            Next
        End If
        ReDim _balls(_nBalls + 1)
        For i = 0 To _nBalls - 1
            curColor -= 100 ' // change the color some way
            _balls(i) = New Ball(curColor)
            _balls(i)._Position = New Point With {
          .X = _rand.Next(CInt(_boundSize.X)) + 1,
          .Y = _rand.Next(CInt(_boundSize.Y)) + 1
      }
            _balls(i)._Speed = New Point With {
          .X = _rand.Next(_ballSpeed) + 1,
          .Y = _rand.Next(_ballSpeed) + 1
      }
        Next
        EraseRect()
    End Sub
    Sub OnSizeChanged()
        Me._doInit = True
        _boundSize = New Point(CInt(Me.ActualWidth * xScale), CInt(Me.ActualHeight * yScale))
    End Sub
    Sub OnKey(e As System.Windows.Input.KeyEventArgs)
        Select Case e.Key
            Case Key.Subtract
                _ballSpeed -= 1
            Case Key.Add
                _ballSpeed += 1
            Case Key.E
                _doErase = Not _doErase
            Case Key.R
                _doInit = True
            Case Key.Q, Key.Escape
                Me._wParent.Close
        End Select
    End Sub

    Public Class MyObject
        Public _rect As Rect
        Public _ForeColor As IntPtr
        Public _Position As Point
        Public _Speed As Point
    End Class

    Public Class Ball
        Inherits MyObject
        Implements IDisposable

        Public Sub New(hbrBallColor As Integer)
            _rect = New Rect(0, 0, 20, 20)
            _Speed = New Point(1, 1)
            _ForeColor = CreateSolidBrush(CType(hbrBallColor, IntPtr))
        End Sub
        Public Sub Draw(hdc As IntPtr)
            SelectObject(hdc, _ForeColor)
            Dim newPos = _rect.RectMove(_Position)
            Ellipse(hdc, CInt(newPos.Left),
              CInt(newPos.Top),
              CInt(newPos.Right),
              CInt(newPos.Bottom)
              )
        End Sub
        Public Sub [Erase](hdc As IntPtr, hBackColor As IntPtr)
            Dim rect = _rect.RectMove(_Position).ToWinRect
            FillRect(hdc, rect, hBackColor)
        End Sub

#Region "IDisposable Support"
        Private disposedValue As Boolean ' To detect redundant calls

        ' IDisposable
        Protected Overridable Sub Dispose(disposing As Boolean)
            If Not Me.disposedValue Then
                If disposing Then
                    ' TODO: dispose managed state (managed objects).
                End If
                DeleteObject(_ForeColor)
                ' TODO: free unmanaged resources (unmanaged objects) and override Finalize() below.
                ' TODO: set large fields to null.
            End If
            Me.disposedValue = True
        End Sub

        Protected Overrides Sub Finalize()
            ' Do not change this code.  Put cleanup code in Dispose(ByVal disposing As Boolean) above.
            Dispose(False)
            MyBase.Finalize()
        End Sub

        ' This code added by Visual Basic to correctly implement the disposable pattern.
        Public Sub Dispose() Implements IDisposable.Dispose
            ' Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
            Dispose(True)
            GC.SuppressFinalize(Me)
        End Sub
#End Region
        Public Overrides Function ToString() As String
            Return String.Format("{0}", _rect.ToString())
        End Function
    End Class
End Class

Public MustInherit Class MyHwndHost
    Inherits Interop.HwndHost
    Public _hwnd As IntPtr
    Protected _hbrBackground As IntPtr
    Public xScale As Double = 1
    Public yScale As Double = 1

    Public Sub New(hbrBackground As IntPtr)
        _hbrBackground = hbrBackground
    End Sub
    Protected Overrides Function BuildWindowCore(hwndParent As HandleRef) As HandleRef
        Dim psource = PresentationSource.FromVisual(Me)
        If psource IsNot Nothing Then
            ' deal with scaling:try commenting these out
            xScale = psource.CompositionTarget.TransformToDevice.M11
            yScale = psource.CompositionTarget.TransformToDevice.M22
        End If
        _hwnd = CreateWindowEx(
            0,
            "static",
            "",
            WindowStyles.WS_CHILD Or
            WindowStyles.WS_CLIPCHILDREN Or
            WindowStyles.WS_CLIPSIBLINGS,
            0, 0, 50, 50,
            hwndParent.Handle,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero)
        Return New HandleRef(Me, _hwnd)
    End Function
    Public MustOverride Sub OnReady(hwnd As IntPtr)

    Public Sub EraseRect()
        Dim r = New WinRect
        GetClientRect(_hwnd, r)
        Dim hDC = GetDC(_hwnd)
        FillRect(hDC, r, _hbrBackground)
        ReleaseDC(_hwnd, hDC)
    End Sub

    Protected Overrides Function WndProc(
                                        hwnd As IntPtr,
                                        msg As Integer,
                                        wParam As IntPtr,
                                        lParam As IntPtr,
                                        ByRef handled As Boolean
                                        ) As IntPtr
        'Debug.WriteLine(String.Format("{0:x8} {1:x8} {2:x8} {3:x8}",
        '                              hwnd, msg, wParam, lParam))
        Select Case msg
            Case WM_.WM_ERASEBKGND
                EraseRect()
                ValidateRect(_hwnd, Nothing)
                OnReady(_hwnd)
                handled = True
                Return IntPtr.Zero
            Case WM_.WM_PAINT
                Dim ps As PAINTSTRUCT = Nothing
                Dim hdc = BeginPaint(_hwnd, ps)
                EndPaint(_hwnd, ps)
                handled = True
                Return IntPtr.Zero
        End Select
        Return MyBase.WndProc(hwnd, msg, wParam, lParam, handled)
    End Function

    Protected Overrides Sub DestroyWindowCore(hwnd As HandleRef)
        DeleteObject(_hbrBackground)
        DestroyWindow(hwnd.Handle)
    End Sub
End Class

Public Module NativeMethods
    ''' <summary>
    ''' WinPoint uses integer for WinApi
    ''' (System.Windows.Point uses Double)
    ''' </summary>
    ''' <remarks></remarks>
    <StructLayout(LayoutKind.Sequential)>
    Public Structure WinPoint
        Public Sub New(x As Integer, y As Integer)
            Me.x = x
            Me.y = y
        End Sub
        Public x As Integer
        Public y As Integer
        Public Shared Operator +(p1 As WinPoint, p2 As WinPoint) As WinPoint
            Dim pt As WinPoint
            pt.x = p1.x + p2.x
            pt.y = p1.y + p2.y
            Return pt
        End Operator
        Public Overrides Function ToString() As String
            Return String.Format("({0},{1})", x, y)
        End Function
    End Structure

    ''' <summary>
    ''' WinRect uses integers to interop with WinAPI
    ''' (System.Windows.Rect uses Double)
    ''' </summary>
    ''' <remarks></remarks>
    <StructLayout(LayoutKind.Sequential)>
    Public Structure WinRect
        Public Sub New(
                      left As Integer,
                      top As Integer,
                      right As Integer,
                      bottom As Integer)
            Me.Left = left
            Me.Top = top
            Me.Right = right
            Me.Bottom = bottom
        End Sub
        Public Left As Integer
        Public Top As Integer
        Public Right As Integer
        Public Bottom As Integer
        Public Function ToRect() As System.Windows.Rect
            Return New System.Windows.Rect(Left, Top, Right - Left, Bottom - Top)
        End Function
        Public Overrides Function ToString() As String
            Return String.Format(
                "({0},{1}), ({2},{3})",
                Left,
                Top,
                Right,
                Bottom)
        End Function
    End Structure

    <System.Runtime.CompilerServices.Extension()>
    Public Function RectMove(rect1 As Rect, pt As Point) As Rect
        rect1.X += pt.X
        rect1.Y += pt.Y
        Return rect1
    End Function

    <System.Runtime.CompilerServices.Extension()>
    Public Function ToWinRect(rect1 As Rect) As WinRect
        Return New WinRect(CInt(rect1.Left),
                           CInt(rect1.Top),
                           CInt(rect1.Left + rect1.Width),
                           CInt(rect1.Top + rect1.Height)
                           )
    End Function
#Region "WIN32 Defs"
    Public Enum COLOR
        COLOR_SCROLLBAR = 0
        COLOR_BACKGROUND = 1
        COLOR_DESKTOP = 1
        COLOR_ACTIVECAPTION = 2
        COLOR_INACTIVECAPTION = 3
        COLOR_MENU = 4
        COLOR_WINDOW = 5
        COLOR_WINDOWFRAME = 6
        COLOR_MENUTEXT = 7
        COLOR_WINDOWTEXT = 8
        COLOR_CAPTIONTEXT = 9
        COLOR_ACTIVEBORDER = 10
        COLOR_INACTIVEBORDER = 11
        COLOR_APPWORKSPACE = 12
        COLOR_HIGHLIGHT = 13
        COLOR_HIGHLIGHTTEXT = 14
        COLOR_BTNFACE = 15
        COLOR_3DFACE = 15
        COLOR_BTNSHADOW = 16
        COLOR_3DSHADOW = 16
        COLOR_GRAYTEXT = 17
        COLOR_BTNTEXT = 18
        COLOR_INACTIVECAPTIONTEXT = 19
        COLOR_BTNHIGHLIGHT = 20
        COLOR_3DHIGHLIGHT = 20
        COLOR_3DHILIGHT = 20
        COLOR_BTNHILIGHT = 20
        COLOR_3DDKSHADOW = 21
        COLOR_3DLIGHT = 22
        COLOR_INFOTEXT = 23
        COLOR_INFOBK = 24
    End Enum

    Public Enum RasterOps
        R2_BLACK = 1        '  /*  0       */
        R2_NOTMERGEPEN = 2  '  /* DPon     */
        R2_MASKNOTPEN = 3   '  /* DPna     */
        R2_NOTCOPYPEN = 4   '  /* PN       */
        R2_MASKPENNOT = 5   '  /* PDna     */
        R2_NOT = 6          '  /* Dn       */
        R2_XORPEN = 7       '  /* DPx      */
        R2_NOTMASKPEN = 8   '  /* DPan     */
        R2_MASKPEN = 9      '  /* DPa      */
        R2_NOTXORPEN = 10   '  /* DPxn     */
        R2_NOP = 11         '  /* D        */
        R2_MERGENOTPEN = 12 '  /* DPno     */
        R2_COPYPEN = 13     '  /* P        */
        R2_MERGEPENNOT = 14 '  /* PDno     */
        R2_MERGEPEN = 15    '  /* DPo      */
        R2_WHITE = 16       '  /*  1       */
        R2_LAST = 16
    End Enum

    <DllImport("user32.dll", CharSet:=CharSet.Auto)>
    Public Function GetClientRect(ByVal hWnd As System.IntPtr,
       ByRef lpRECT As WinRect) As Integer
    End Function

    Public Enum WM_
        WM_PAINT = &HF
        WM_ERASEBKGND = &H14
    End Enum
    <Flags()>
    Public Enum WindowStyles
        WS_OVERLAPPED = &H0
        WS_POPUP = &H80000000
        WS_CHILD = &H40000000
        WS_MINIMIZE = &H20000000
        WS_VISIBLE = &H10000000
        WS_DISABLED = &H8000000
        WS_CLIPSIBLINGS = &H4000000
        WS_CLIPCHILDREN = &H2000000
        WS_MAXIMIZE = &H1000000
        WS_BORDER = &H800000
        WS_DLGFRAME = &H400000
        WS_VSCROLL = &H200000
        WS_HSCROLL = &H100000
        WS_SYSMENU = &H80000
        WS_THICKFRAME = &H40000
        WS_GROUP = &H20000
        WS_TABSTOP = &H10000
        WS_MINIMIZEBOX = &H20000
        WS_MAXIMIZEBOX = &H10000
        WS_CAPTION = WS_BORDER Or WS_DLGFRAME
        WS_TILED = WS_OVERLAPPED
        WS_ICONIC = WS_MINIMIZE
        WS_SIZEBOX = WS_THICKFRAME
        WS_TILEDWINDOW = WS_OVERLAPPEDWINDOW
        WS_OVERLAPPEDWINDOW = WS_OVERLAPPED Or WS_CAPTION Or WS_SYSMENU Or WS_THICKFRAME Or WS_MINIMIZEBOX Or WS_MAXIMIZEBOX
        WS_POPUPWINDOW = WS_POPUP Or WS_BORDER Or WS_SYSMENU
        WS_CHILDWINDOW = WS_CHILD
    End Enum
    <DllImport("user32.dll", SetLastError:=True, CharSet:=CharSet.Auto)>
    Public Function ShowWindow(ByVal hwnd As IntPtr, ByVal nCmdShow As Int32) As Boolean
    End Function
    <DllImport("user32.dll")>
    Public Function UpdateWindow(
     ByVal hWnd As IntPtr) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function
    <DllImport("user32.dll", CharSet:=CharSet.Auto)>
    Public Function CreateWindowEx(
         ByVal dwExStyle As UInteger,
         ByVal lpClassName As String,
         ByVal lpWindowName As String,
         ByVal dwStyle As WindowStyles,
         ByVal x As Integer,
         ByVal y As Integer,
         ByVal nWidth As Integer,
         ByVal nHeight As Integer,
         ByVal hWndParent As IntPtr,
         ByVal hMenut As IntPtr,
         ByVal hInstancet As IntPtr,
         ByVal lpParamt As IntPtr) As IntPtr
    End Function

    <DllImport("user32.dll", SetLastError:=True, CharSet:=CharSet.Auto)>
    Public Function DestroyWindow(ByVal hwnd As IntPtr) As Boolean
    End Function

    Public Const CW_USEDEFAULT As Int32 = &H80000000

    Public Enum Show_Window
        SW_HIDE = 0
        SW_SHOWNORMAL = 1
        SW_NORMAL = 1
        SW_SHOWMINIMIZED = 2
        SW_SHOWMAXIMIZED = 3
        SW_MAXIMIZE = 3
        SW_SHOWNOACTIVATE = 4
        SW_SHOW = 5
        SW_MINIMIZE = 6
        SW_SHOWMINNOACTIVE = 7
        SW_SHOWNA = 8
        SW_RESTORE = 9
        SW_SHOWDEFAULT = 10
        SW_FORCEMINIMIZE = 11
        SW_MAX = 11
    End Enum
    <StructLayout(LayoutKind.Sequential, Pack:=4)>
    Public Structure PAINTSTRUCT
        Public hdc As IntPtr
        Public fErase As Integer
        Public rcPaint As WinRect
        Public fRestore As Integer
        Public fIncUpdate As Integer
        <MarshalAs(UnmanagedType.ByValArray, SizeConst:=32)>
        Public rgbReserved As Byte()
    End Structure
    <DllImport("user32.dll")>
    Public Function BeginPaint(
     ByVal hWnd As IntPtr, ByRef lpPaint As PAINTSTRUCT) As IntPtr
    End Function
    <DllImport("user32.dll")>
    Public Function EndPaint(
     ByVal hWnd As IntPtr, ByRef lpPaint As PAINTSTRUCT) As IntPtr
    End Function
    <DllImport("user32.dll")>
    Public Function GetDC(
     ByVal hWnd As IntPtr) As IntPtr
    End Function
    <DllImport("user32.dll")>
    Public Function ReleaseDC(
     ByVal hWnd As IntPtr, hdc As IntPtr) As IntPtr
    End Function
    <DllImport("user32.dll")>
    Public Function FillRect(ByVal hDC As IntPtr, ByRef lpRect As WinRect, ByVal hBR As IntPtr) As IntPtr
    End Function
    <DllImport("user32.dll")>
    Public Function InvalidateRect(ByVal hWnd As IntPtr, ByRef lpRect As WinRect, ByVal bErase As Boolean) As IntPtr
    End Function
    <DllImport("user32.dll")>
    Public Function ValidateRect(ByVal hWnd As IntPtr, ByRef lpRect As WinRect) As Boolean
    End Function
    <DllImport("user32.dll")>
    Public Function GetUpdateRect(ByVal hWnd As IntPtr, ByRef lpRect As WinRect, ByVal bErase As Boolean) As Boolean
    End Function
    <DllImport("gdi32.dll")>
    Public Function Ellipse(ByVal hDC As IntPtr, nLeft As Integer, nTop As Integer, nRight As Integer, nBottom As Integer) As IntPtr
    End Function
    <DllImport("gdi32.dll")>
    Public Function CreateSolidBrush(ByVal crColor As IntPtr) As IntPtr
    End Function

    <DllImport("gdi32.dll")>
    Public Function DeleteObject(ByVal hObject As IntPtr) As IntPtr
    End Function
    <DllImport("gdi32.dll")>
    Public Function SelectObject(hDC As IntPtr, ByVal hObject As IntPtr) As IntPtr
    End Function
    <DllImport("gdi32.dll")>
    Public Function MoveToEx(hDC As IntPtr, X As Integer, Y As Integer, ByRef lpPointPrev As WinPoint) As Boolean
    End Function
    <DllImport("gdi32.dll")>
    Public Function LineTo(hDC As IntPtr, nXEnd As Integer, nYEnd As Integer) As Boolean
    End Function

    <DllImport("gdi32.dll")>
    Public Function CreatePen(nPenStyle As Integer, nWidth As Integer, nColor As IntPtr) As IntPtr
    End Function


    <DllImport("user32.dll")>
    Public Function SetProcessDPIAware() As Boolean
    End Function
    <DllImport("user32.dll")>
    Public Function IsProcessDPIAware() As Boolean
    End Function
    <DllImport("gdi32.dll")>
    Public Function SetROP2(hdc As IntPtr, fnDrawode As RasterOps) As Boolean
    End Function

    <DllImport("gdi32.dll")>
    Public Function CreateCompatibleDC(hdc As IntPtr) As IntPtr
    End Function

    <DllImport("gdi32.dll")>
    Public Function DeleteDC(hdc As IntPtr) As IntPtr
    End Function

    <DllImport("gdi32.dll")>
    Public Function CreateCompatibleBitmap(hdc As IntPtr, nWidth As Integer, nHeight As Integer) As IntPtr
    End Function

    <DllImport("gdi32.dll")>
    Public Function GetPixel(hdc As IntPtr, xPos As Integer, yPos As Integer) As IntPtr
    End Function
    <DllImport("gdi32.dll")>
    Public Function SetPixel(hdc As IntPtr, xPos As Integer, yPos As Integer, crColor As IntPtr) As IntPtr
    End Function

    <DllImport("gdi32.dll")>
    Public Function BitBlt(hdcDest As IntPtr,
                           xDest As Integer,
                           yDest As Integer,
                           nWidth As Integer,
                           nHeight As Integer,
                           hdcSrc As IntPtr,
                           xSrc As Integer,
                           ySrc As Integer,
                           dwRop As Integer) As IntPtr
    End Function

    Public Const BLACKNESS = &H42
    Public Const DSTINVERT = &H550009
    Public Const MERGECOPY = &HC000CA
    Public Const MERGEPAINT = &HBB0226
    Public Const NOTSRCCOPY = &H330008
    Public Const NOTSRCERASE = &H1100A6
    Public Const PATCOPY = &HF00021
    Public Const PATINVERT = &H5A0049
    Public Const PATPAINT = &HFB0A09
    Public Const SRCAND = &H8800C6
    Public Const SRCCOPY = &HCC0020
    Public Const SRCERASE = &H440328
    Public Const SRCINVERT = &H660046
    Public Const SRCPAINT = &HEE0086
    Public Const WHITENESS = &HFF0062


#End Region

End Module

