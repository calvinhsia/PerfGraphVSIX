'Desc: Cartoon animation in WPF: Draw figures with your finger or mouse and animate them
'Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\PresentationFramework.dll
'Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\PresentationCore.dll
'Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\WindowsBase.dll
'Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\System.Windows.Forms.dll
'Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\System.Xaml.dll
'Ref: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\System.Xml.dll

'Pragma: verbose=false



'Option Strict On

Imports System.Runtime.InteropServices
Imports System.Threading
Imports System.Diagnostics
Imports System.Windows
Imports System.Windows.Markup
Imports System.Windows.Media
Imports System.Collections
Imports System.Collections.Generic
Imports System.Windows.Controls
Imports System.Windows.Input
Imports System.Windows.Threading

Public Module Main
    Sub DoMain(args As Object())
        Dim owin = New MainWindow
        owin.ShowDialog

    End Sub
End Module
' see also https://blogs.msdn.microsoft.com/calvin_hsia/2009/01/29/cartoon-animation-program/

Public Class MainWindow
    Inherits Window
    Friend WithEvents btnNewFrame As Button
    Friend WithEvents btnErase As Button
    Friend WithEvents btnPlay As Button
    Friend WithEvents btnDemo As Button
    Friend WithEvents btnReset As Button
    Friend WithEvents sldBetween As Slider
    Friend txtStatus As TextBlock
    Private _AnimControl As AnimControl
    Sub Load() Handles MyBase.Initialized
        WindowState = WindowState.Maximized
        Title = "Cartoon Animation by Calvin Hsia 1982"
        Dim xaml =
    <DockPanel
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" LastChildFill="True">
        <StackPanel Background="Transparent" Orientation="Vertical" DockPanel.Dock="Top">
            <TextBlock>Draw To create lines For a cartoon frame. Add a New frame, hit play. 
                        Rebirth Of Calvin's cartoon program circa 1982
                </TextBlock>
        </StackPanel>
        <Border DockPanel.Dock="Top" Height="25">
            <StackPanel Orientation="Horizontal">
                <Button Name="btnNewFrame"
                    ToolTip="Add current drawing to cartoon, so you can create a new one">_New Frame</Button>
                <Button Name="btnErase"
                    ToolTip="erase current frame">_Erase</Button>
                <Button Name="btnPlay"
                    ToolTip="Animate the current frames or stop animation" Width="40">_Play</Button>
                <Button Name="btnDemo"
                    ToolTip="Automatically generate frames with curves. Left Ctrl-Click to vary thickness">_Demo</Button>
                <Button Name="btnReset"
                    ToolTip="Erase all frames">_Reset</Button>
                <Slider Name="sldBetween" Width="200"
                    ToolTip="# Frames to generate in between"
                    Maximum="1000" Minimum="0" Value="500"/>
                <TextBlock Name="txtStatus"/>
            </StackPanel>
        </Border>
        <UserControl Name="MyCtrl"/>
    </DockPanel>

        Dim dPanel = CType(XamlReader.Load(xaml.CreateReader), DockPanel)
        Dim MyCtrl = CType(dPanel.FindName("MyCtrl"), UserControl)
        _AnimControl = New AnimControl(Me)
        MyCtrl.Content = _AnimControl
        btnPlay = CType(dPanel.FindName("btnPlay"), Button)
        btnNewFrame = CType(dPanel.FindName("btnNewFrame"), Button)
        btnPlay = CType(dPanel.FindName("btnPlay"), Button)
        btnDemo = CType(dPanel.FindName("btnDemo"), Button)
        btnErase = CType(dPanel.FindName("btnErase"), Button)
        btnReset = CType(dPanel.FindName("btnReset"), Button)
        txtStatus = CType(dPanel.FindName("txtStatus"), TextBlock)
        sldBetween = CType(dPanel.FindName("sldBetween"), Slider)
        Me.Content = dPanel
    End Sub
    Sub winloaded() Handles Me.Loaded
        _AnimControl.Demo(varyThickness:=True)
        btnPlay_Click()
    End Sub
    Sub btnNewFrame_Click() Handles btnNewFrame.Click
        _AnimControl.NewFrame()
        RefreshStatus()
    End Sub
    Sub btnPlay_Click() Handles btnPlay.Click
        If _AnimControl._timer.IsEnabled Then
            StopPlay()
        Else
            btnNewFrame_Click()   ' save any currently drawn changes first
            btnPlay.Content = "Sto_p"
            _AnimControl.Play()
        End If
    End Sub
    Sub StopPlay()
        If _AnimControl._timer.IsEnabled Then
            btnPlay.Content = "_Play"
            _AnimControl._timer.IsEnabled = False
        End If
    End Sub
    Sub btnDemo_Click() Handles btnDemo.Click
        StopPlay()
        Dim varyThickness = Keyboard.IsKeyDown(Key.LeftCtrl)
        _AnimControl.Demo(varyThickness)
        btnPlay_Click()
    End Sub
    Sub btnErase_Click() Handles btnErase.Click
        _AnimControl.EraseBtn()
    End Sub
    Sub btnReset_Click() Handles btnReset.Click
        StopPlay()
        _AnimControl.Reset()
    End Sub

    Friend Sub RefreshStatus()
        Me.txtStatus.Text = $"Between={CInt(sldBetween.Value),4:n0}  Frame count={_AnimControl._UserFrameList.Count} #Lines={_AnimControl._CurLineList.Count} CurFrame={_AnimControl._ndxUserFrame}"
    End Sub
End Class

Public Class AnimControl
    Inherits FrameworkElement
    Friend WithEvents _timer As New DispatcherTimer
    Friend _rand = New Random()
    Private _MainWindow As MainWindow
    Friend _ndxUserFrame As Integer   ' index into user created frames. 
    Private _ndxBetween As Integer  ' from 0 to nBetween
    Private _ptCurrent As Point?
    Private _ptOld As Point?
    Private _fPenDown As Boolean
    Private _PenModeDrag As Boolean = True ' either create line segs, or continuous drag to create multiple segs
    ' lines to draw for current image: could be while composing, or playing.
    Friend _CurLineList As New List(Of cFrameLine)
    Public Const ThicknessDefault = 10
    Public Const ThicknessMax = 50
    Public Const InBetweenDefault = 100

    'Frames stored by user
    Friend _UserFrameList As New List(Of cCartoonFrame)
    Sub New(w As MainWindow)
        _MainWindow = w
    End Sub
    Sub Reset()
        Me._MainWindow.sldBetween.Value = InBetweenDefault
        Me._UserFrameList.Clear() ' erase all user data
        EraseBtn()
    End Sub
    Sub EraseBtn() ' erase current frame
        _CurLineList.Clear()
        Me._ptOld = Nothing
        Me._fPenDown = False
        Me.InvalidateVisual()
    End Sub
    Sub Demo(varyThickness As Boolean)
        Reset()
        Dim nFrames = 20
        Dim numObjects = 2
        Dim numSegsPerObject = 1000
        Dim fForce = Me.ActualHeight / 5
        Dim pow = 2
        Dim wallBound = 15
        For nFrame = 0 To nFrames - 1
            Dim nInitSpeed = 4
            For nObject = 0 To numObjects - 1
                Dim pos0 As New Point With {
                    .X = _rand.Next(Me.ActualWidth),
                    .Y = _rand.Next(Me.ActualHeight)
                }
                Dim startPos = pos0
                Dim vel As New Vector With {
                    .X = _rand.NextDouble * nInitSpeed,
                    .Y = _rand.NextDouble * nInitSpeed
                }
                Dim thickness = AnimControl.ThicknessDefault
                For nSeg = 0 To numSegsPerObject - 2
                    Dim pos1 = Point.Add(pos0, vel)
                    If varyThickness Then
                        thickness = 1 + AnimControl.ThicknessMax * nSeg / numSegsPerObject
                    End If
                    Me._CurLineList.Add(New cFrameLine(pos0, pos1, thickness))
                    ' all 4 walls exert a force proportional to inverse square of distance
                    Dim dWest = Math.Max(pos1.X, wallBound) ' displacement 
                    Dim accWest = fForce / dWest ^ pow
                    Dim dEast = Math.Max(Me.ActualWidth - pos1.X, wallBound)
                    Dim accEast = -fForce / dEast ^ pow
                    Dim dNorth = Math.Max(pos1.Y, wallBound)
                    Dim accNorth = fForce / dNorth ^ pow
                    Dim dSouth = Math.Max(Me.ActualHeight - pos1.Y, wallBound)
                    Dim accSouth = -fForce / dSouth ^ pow
                    Dim accEastWest = (accEast + accWest) * _rand.NextDouble * 20
                    Dim accNorthSouth = (accNorth + accSouth) * _rand.NextDouble * 20
                    Dim accel = New Vector(accEastWest, accNorthSouth)
                    vel = Vector.Add(vel, accel)
                    pos0 = pos1 ' next line segment starts at end of cur seg
                Next
                ' now add a final line segment that closes the curve
                Me._CurLineList.Add(New cFrameLine(pos0, startPos))
            Next
            Me._UserFrameList.Add(New cCartoonFrame(Me._CurLineList))
            Me._CurLineList.Clear() ' reset for next frame
        Next
    End Sub
    Sub NewFrame()
        If _CurLineList.Count > 0 Then
            Dim curFrame = New cCartoonFrame(_CurLineList)
            _UserFrameList.Add(curFrame)
            EraseBtn()
        End If
    End Sub

    Friend Sub Play()
        If _UserFrameList.Count < 2 Then
            MsgBox("Need at least 2 frames to animate")
            Return
        End If
        If _timer.IsEnabled Then ' if we're already playing, stop
            _timer.IsEnabled = False
        Else
            _timer.Interval = New TimeSpan(0, 0, 0, 0, 50) ' days,hrs,mins,secs,msecs
            _timer.IsEnabled = True
        End If
        Me._fPenDown = False
        Me._ndxUserFrame = 0
        Me._ndxBetween = 1 ' 1st is drawn now, next by timer tick
        Me._CurLineList.Clear()
        Me._CurLineList.AddRange(Me._UserFrameList(0)._Lines) 'get the 1st frame
        Me.InvalidateVisual() ' show it
    End Sub
    Sub tmr_tick() Handles _timer.Tick ' let's do the animating
        If _ndxUserFrame = Me._UserFrameList.Count Then ' restart at last frame
            Me._ndxUserFrame = 0
            Me._ndxBetween = 0
        End If
        Me._CurLineList.Clear()
        Dim frmLeft = Me._UserFrameList(Me._ndxUserFrame) ' the frame on the left
        Dim ndxFrmRight = Me._ndxUserFrame + 1
        If ndxFrmRight = Me._UserFrameList.Count Then
            ndxFrmRight = 0 ' wrap around to first frame
        End If
        Dim frmRight = Me._UserFrameList(ndxFrmRight)
        Dim nBetween = CInt(_MainWindow.sldBetween.Value) + 1
        Dim fnInterpolate = Function(lVal, rVal) lVal + Me._ndxBetween * (rVal - lVal) / nBetween
        Dim nLinesToDraw = Math.Max(frmLeft._Lines.Count, frmRight._Lines.Count) - 1
        Dim pt0, pt1 As Point

        For ndx = 0 To nLinesToDraw ' calc the lines to draw
            Dim lineLeft = frmLeft._Lines(Math.Min(ndx, frmLeft._Lines.Count - 1))
            Dim lineRight = frmRight._Lines(Math.Min(ndx, frmRight._Lines.Count - 1))

            Dim thickness = fnInterpolate(lineLeft.thickness, lineRight.thickness)
            pt0.X = fnInterpolate(lineLeft.pt0.X, lineRight.pt0.X)
            pt0.Y = fnInterpolate(lineLeft.pt0.Y, lineRight.pt0.Y)
            pt1.X = fnInterpolate(lineLeft.pt1.X, lineRight.pt1.X)
            pt1.Y = fnInterpolate(lineLeft.pt1.Y, lineRight.pt1.Y)

            Dim newLine = New cFrameLine(pt0, pt1, thickness)
            Me._CurLineList.Add(newLine)
        Next
        If Me._ndxBetween >= nBetween Then ' we've reached the right
            Me._ndxUserFrame += 1 ' advance to next user frame 
            Me._ndxBetween = 0
        End If
        Me._ndxBetween += 1 ' advance to next frame
        Me.InvalidateVisual()
    End Sub
    Protected Overrides Sub OnRender(drawingContext As DrawingContext)
        drawingContext.DrawRectangle(
            Brushes.AliceBlue,
            New Pen(Brushes.Purple, 1),
            New Rect(0, 0, Me.RenderSize.Width, Me.RenderSize.Height))
        Dim colorVal = &H0
        Dim colorDelta = IIf(_timer.IsEnabled, Me._CurLineList.Count, 50)
        For Each lin In Me._CurLineList ' draw the lines in the current frame
            Dim c = Color.FromRgb((colorVal / 256) And &HFF,
                                  (colorVal / 256 / 256) And &HFF,
                                  colorVal And &HFF)
            Dim brush = New SolidColorBrush(c)

            Dim oPen = New Pen(brush, lin.thickness)
            colorVal = (colorVal + 10 * colorDelta) Mod &HFFFFFF
            drawingContext.DrawLine(oPen, lin.pt0, lin.pt1)
        Next
        If Me._fPenDown Then
            If Me._ptOld.HasValue Then
                drawingContext.DrawLine(
                    New Pen(Brushes.Black, 2),
                    Me._ptOld,
                    Me._ptCurrent)
            End If
        End If
        _MainWindow.RefreshStatus()
    End Sub
    Protected Overrides Sub OnMouseDown(e As MouseButtonEventArgs)
        If e.RightButton = MouseButtonState.Pressed Then
            Me._PenModeDrag = Not Me._PenModeDrag ' toggle modes on right click
        Else
            If Me._PenModeDrag Then
                Me._ptOld = e.GetPosition(Me)
            Else
                If e.RightButton = MouseButtonState.Pressed Then
                    Me._fPenDown = False
                    Me._ptOld = Nothing
                Else
                    Me._fPenDown = True
                    Me._ptCurrent = e.GetPosition(Me) ' get cur pos rel to self
                    If Not Me._ptOld.HasValue Then
                        Me._ptOld = Me._ptCurrent ' same
                    End If
                    Me.InvalidateVisual()
                End If
            End If
        End If
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        If Me._PenModeDrag Then
            If e.LeftButton = MouseButtonState.Pressed Then
                If Me._ptOld.HasValue Then
                    Me._ptCurrent = e.GetPosition(Me)
                    Dim thickNesss = AnimControl.ThicknessDefault
                    If e.StylusDevice IsNot Nothing Then
                        Dim pts = e.StylusDevice.GetStylusPoints(e.StylusDevice.Target)
                        Dim avgPressure = Aggregate pt In pts Into Average(pt.PressureFactor)
                        thickNesss = AnimControl.ThicknessMax * avgPressure
                    End If
                    Dim newFrameLine = New cFrameLine(Me._ptOld, Me._ptCurrent, thickNesss)
                    Me._CurLineList.Add(newFrameLine)
                    Me._ptOld = Me._ptCurrent
                    Me.InvalidateVisual()
                End If
            End If
        Else
            If Me._fPenDown Then
                If Me._ptOld.HasValue Then
                    Me._ptCurrent = e.GetPosition(Me)
                End If
                Me.InvalidateVisual()
            End If
        End If
    End Sub
    Protected Overrides Sub OnMouseUp(e As MouseButtonEventArgs)
        If Me._fPenDown Then
            Me._ptCurrent = e.GetPosition(Me) ' get cur pos rel to self
            Dim newFrameLine = New cFrameLine(Me._ptOld, Me._ptCurrent)
            Me._CurLineList.Add(newFrameLine)
            Me._ptOld = Me._ptCurrent
            Me._fPenDown = False
            Me.InvalidateVisual()
        End If
    End Sub
    Protected Overrides Sub OnMouseWheel(e As MouseWheelEventArgs)
        If Me._timer.IsEnabled Then ' only when playing
            Me._MainWindow.sldBetween.Value += If(e.Delta > 0, 10, -10)
        End If
    End Sub

    <DebuggerDisplay("{ToString()}")>
    Public Class cCartoonFrame ' User created cartoon frame
        Public ReadOnly _Lines As New List(Of cFrameLine) ' a frame is a list of lines
        ' # of frames to gen between real user frames
        Sub New(lst As List(Of cFrameLine))
            _Lines.AddRange(lst)
        End Sub
        Public Overrides Function ToString() As String
            Return String.Format("LineCount = {0}", _Lines.Count)
        End Function
    End Class
    'A line to be animated. Could belong to a real or gen'd user frame, or while user is actively drawing
    <DebuggerDisplay("{ToString()}")>
    Public Class cFrameLine ' User created cartoon line. It's just 2 points.
        Friend ReadOnly pt0 As Point
        Friend ReadOnly pt1 As Point
        Friend ReadOnly thickness As Double
        Sub New(pt0 As Point, pt1 As Point, Optional thickness As Double = AnimControl.ThicknessDefault)
            Me.pt0 = pt0
            Me.pt1 = pt1
            Me.thickness = thickness
        End Sub
        Public Overrides Function ToString() As String
            Return String.Format("Line = ({0:n0}, {1:n0}) ({2:n0}, {3:n0}) {4:n3}", pt0.X, pt0.Y, pt1.X, pt1.Y, (pt1 - pt0).Length)
        End Function
    End Class
End Class