Imports System.Drawing
Imports System.Runtime.InteropServices
Imports OpenTK.Graphics.OpenGL4

' System.Drawing.Imaging is NOT imported and neither is System.Drawing.Text.
' Both collide with names already in scope - `PixelFormat` exists in
' System.Drawing.Imaging and in OpenTK.Graphics.OpenGL4, and `Text` would
' resolve against System.Text - so the few types wanted from them are written
' out in full below. Importing either turns every use into an ambiguity error.

''' <summary>
''' ASCII 32..126 rasterised once into a texture atlas, so the viewer can draw
''' text without a third-party UI library.
'''
''' WHY THE OS RASTERISER AND NOT A HAND-TYPED GLYPH TABLE. The first attempt
''' here was a 5x7 bitmap font authored in this file, on the theory that it kept
''' the app to its one dependency. It does not pay: 95 glyphs is 665 rows of
''' art to get right by eye, a single wrong row is a silently ugly character,
''' and the result is worse-looking than what the machine already has. What
''' `UseWindowsForms` buys is `System.Drawing` out of the Windows Desktop shared
''' framework - it is NOT a NuGet package, so the project still carries exactly
''' one PackageReference (OpenTK), which is what that rule was protecting.
'''
''' MONOSPACE IS LOAD-BEARING, not a style choice. Every cell being the same
''' width makes the atlas a plain grid, makes `Width()` a multiply instead of a
''' per-character sum, and - the part that matters - makes hit-testing a click
''' against a character position arithmetic rather than a measurement. A
''' proportional font would need a per-glyph advance table threaded through all
''' three.
'''
''' The atlas is 16 x 6 cells = 96, one more than the 95 printable characters.
''' That spare cell is filled SOLID WHITE and is what `UiOverlay` points its
''' untextured quads at, so panel backgrounds, row highlights and the scrollbar
''' all go down the same textured path as the text. One shader, one buffer, one
''' draw call for the whole UI.
''' </summary>
Public NotInheritable Class UiFont

    Public Const COLS As Integer = 16
    Public Const ROWS As Integer = 6
    Private Const FIRST As Integer = 32
    Private Const LAST As Integer = 126
    Private Const WHITE_CELL As Integer = 95     ' one past the last glyph

    Public ReadOnly CellW As Integer
    Public ReadOnly CellH As Integer
    Public ReadOnly Texture As Integer

    Private ReadOnly atlasW As Integer
    Private ReadOnly atlasH As Integer

    Public Sub New(family As String, pixelSize As Single)
        ' GenericTypographic on purpose: the default StringFormat adds a few
        ' pixels of padding either side of a run, which would put the measured
        ' advance and the drawn glyph out of step and make text drift right
        ' across a long string.
        Dim fmt = CType(StringFormat.GenericTypographic.Clone(), StringFormat)
        fmt.FormatFlags = fmt.FormatFlags Or StringFormatFlags.MeasureTrailingSpaces

        Dim fnt As Font = Nothing
        For Each name In New String() {family, "Consolas", "Courier New"}
            Try
                fnt = New Font(name, pixelSize, FontStyle.Regular, GraphicsUnit.Pixel)
                If fnt.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase) Then Exit For
            Catch
            End Try
        Next
        If fnt Is Nothing Then fnt = New Font(FontFamily.GenericMonospace, pixelSize, GraphicsUnit.Pixel)

        Using scratch As New Bitmap(1, 1), probe = Graphics.FromImage(scratch)
            ' "M" is the widest character in a monospace face and every other
            ' one advances the same, so this single measurement sizes the grid.
            Dim s = probe.MeasureString("M", fnt, PointF.Empty, fmt)
            CellW = Math.Max(1, CInt(Math.Ceiling(s.Width)))
            CellH = Math.Max(1, CInt(Math.Ceiling(fnt.GetHeight(probe))))
        End Using

        atlasW = COLS * CellW
        atlasH = ROWS * CellH

        Dim rgba(atlasW * atlasH * 4 - 1) As Byte
        Using bmp As New Bitmap(atlasW, atlasH, Imaging.PixelFormat.Format32bppArgb)
            Using g = Graphics.FromImage(bmp)
                g.Clear(Color.Black)
                ' Grid-fitted antialiasing: hinted to the pixel grid so stems
                ' stay crisp at 13 px, but still smoothed, which plain
                ' SingleBitPerPixel is not.
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit
                For c = FIRST To LAST
                    Dim i = c - FIRST
                    g.DrawString(ChrW(c).ToString(), fnt, Brushes.White,
                                 New PointF((i Mod COLS) * CellW, (i \ COLS) * CellH), fmt)
                Next
                ' The solid cell the overlay uses for untextured quads. Inset by
                ' a pixel so linear sampling at a cell edge can never bleed a
                ' neighbouring glyph into a panel background.
                g.FillRectangle(Brushes.White,
                                (WHITE_CELL Mod COLS) * CellW, (WHITE_CELL \ COLS) * CellH,
                                CellW, CellH)
            End Using

            Dim lockData = bmp.LockBits(New Rectangle(0, 0, atlasW, atlasH),
                                        Imaging.ImageLockMode.ReadOnly, Imaging.PixelFormat.Format32bppArgb)
            Dim raw(atlasW * atlasH * 4 - 1) As Byte
            Marshal.Copy(lockData.Scan0, raw, 0, raw.Length)
            bmp.UnlockBits(lockData)

            ' Coverage becomes ALPHA and the colour is forced white, so a draw
            ' can tint the text by its vertex colour. Keeping the rasterised
            ' grey in RGB instead would multiply the tint twice and every
            ' coloured label would come out muddy.
            For i = 0 To atlasW * atlasH - 1
                Dim b = raw(i * 4), gg = raw(i * 4 + 1), r = raw(i * 4 + 2)
                rgba(i * 4) = 255 : rgba(i * 4 + 1) = 255 : rgba(i * 4 + 2) = 255
                rgba(i * 4 + 3) = Math.Max(r, Math.Max(gg, b))
            Next
        End Using
        fnt.Dispose()

        Texture = GL.GenTexture()
        GL.BindTexture(TextureTarget.Texture2D, Texture)
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, atlasW, atlasH, 0,
                      OpenTK.Graphics.OpenGL4.PixelFormat.Rgba, PixelType.UnsignedByte, rgba)
        ' Nearest, and no mips: the UI draws at exactly 1:1, so any filtering
        ' can only soften text that is already pixel-aligned.
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, CInt(TextureMinFilter.Nearest))
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, CInt(TextureMagFilter.Nearest))
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, CInt(TextureWrapMode.ClampToEdge))
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, CInt(TextureWrapMode.ClampToEdge))
        GL.BindTexture(TextureTarget.Texture2D, 0)
    End Sub

    ''' <summary>Pixel width of a string. A multiply, because the face is
    ''' monospace.</summary>
    Public Function Width(s As String) As Integer
        If s Is Nothing Then Return 0
        Return s.Length * CellW
    End Function

    ''' <summary>How many characters fit in `px` pixels.</summary>
    Public Function Fits(px As Integer) As Integer
        Return Math.Max(0, px \ Math.Max(1, CellW))
    End Function

    Friend Sub GlyphUv(ch As Char, ByRef u0 As Single, ByRef v0 As Single,
                       ByRef u1 As Single, ByRef v1 As Single)
        Dim c = AscW(ch)
        If c < FIRST OrElse c > LAST Then c = AscW("?"c)
        CellUv(c - FIRST, u0, v0, u1, v1)
    End Sub

    Friend Sub WhiteUv(ByRef u As Single, ByRef v As Single)
        Dim a, b, cc, d As Single
        CellUv(WHITE_CELL, a, b, cc, d)
        ' Dead centre of the solid cell - as far from any edge as possible.
        u = (a + cc) * 0.5F
        v = (b + d) * 0.5F
    End Sub

    Private Sub CellUv(i As Integer, ByRef u0 As Single, ByRef v0 As Single,
                       ByRef u1 As Single, ByRef v1 As Single)
        Dim col = i Mod COLS, row = i \ COLS
        u0 = CSng(col * CellW) / atlasW
        v0 = CSng(row * CellH) / atlasH
        u1 = CSng((col + 1) * CellW) / atlasW
        v1 = CSng((row + 1) * CellH) / atlasH
    End Sub
End Class
