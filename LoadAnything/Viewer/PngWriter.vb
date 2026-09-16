Imports System.IO
Imports System.IO.Compression

''' <summary>
''' Writes an 8-bit RGB PNG, and nothing else.
'''
''' Hand-rolled rather than taking a dependency. `System.Drawing.Common` would
''' do it in one call but is a package to restore, and nuTerra's own `Pngcs.dll`
''' belongs to nuTerra. This is about sixty lines and has no way to fail at
''' restore time, which matters for a self-check the whole point of which is to
''' work unattended.
'''
''' The format, for the next reader:
'''
'''     signature   89 50 4E 47 0D 0A 1A 0A
'''     IHDR        w:u32be h:u32be depth=8 colour=2(RGB) 0 0 0
'''     IDAT        a ZLIB stream, not raw deflate
'''     IEND
'''
''' Each chunk is length:u32be, type:4, data, crc32:u32be over TYPE AND DATA
''' (not over the length). The two traps are both in IDAT: the payload is zlib,
''' so raw deflate has to be wrapped in a 2-byte header and followed by an
''' ADLER-32 of the UNCOMPRESSED bytes; and every scanline is preceded by a
''' filter byte, which is 0 here for "none". Miss the filter byte and the image
''' shears one pixel further left on every row.
''' </summary>
Public NotInheritable Class PngWriter

    Public Shared Sub WriteRgb(path As String, width As Integer, height As Integer, rgb As Byte())
        If width <= 0 OrElse height <= 0 Then Throw New ArgumentException("empty image")
        If rgb.Length < width * height * 3 Then Throw New ArgumentException("short pixel buffer")

        ' scanlines, each prefixed with its filter byte
        Dim raw(height * (1 + width * 3) - 1) As Byte
        Dim o = 0
        For y = 0 To height - 1
            raw(o) = 0
            o += 1
            Array.Copy(rgb, y * width * 3, raw, o, width * 3)
            o += width * 3
        Next

        Dim deflated As Byte()
        Using ms As New MemoryStream()
            ' CompressionLevel.Optimal, not SmallestSize: this runs per
            ' screenshot and the difference in file size is not worth the wait.
            Using ds As New DeflateStream(ms, CompressionLevel.Optimal, leaveOpen:=True)
                ds.Write(raw, 0, raw.Length)
            End Using
            deflated = ms.ToArray()
        End Using

        Dim zlib(deflated.Length + 6 - 1) As Byte
        zlib(0) = &H78                       ' CM=8, CINFO=7
        zlib(1) = &H9C                       ' default compression, check bits
        Array.Copy(deflated, 0, zlib, 2, deflated.Length)
        Dim ad = Adler32(raw)
        zlib(zlib.Length - 4) = CByte((ad >> 24) And &HFF)
        zlib(zlib.Length - 3) = CByte((ad >> 16) And &HFF)
        zlib(zlib.Length - 2) = CByte((ad >> 8) And &HFF)
        zlib(zlib.Length - 1) = CByte(ad And &HFF)

        Using fs As New FileStream(path, FileMode.Create, FileAccess.Write)
            fs.Write(New Byte() {137, 80, 78, 71, 13, 10, 26, 10}, 0, 8)

            Dim ihdr(12) As Byte
            PutBE(ihdr, 0, CUInt(width))
            PutBE(ihdr, 4, CUInt(height))
            ihdr(8) = 8                      ' bit depth
            ihdr(9) = 2                      ' colour type 2 = truecolour RGB
            ihdr(10) = 0 : ihdr(11) = 0 : ihdr(12) = 0
            WriteChunk(fs, "IHDR", ihdr)
            WriteChunk(fs, "IDAT", zlib)
            WriteChunk(fs, "IEND", Array.Empty(Of Byte)())
        End Using
    End Sub

    Private Shared Sub PutBE(b As Byte(), at As Integer, v As UInteger)
        b(at) = CByte((v >> 24) And &HFF)
        b(at + 1) = CByte((v >> 16) And &HFF)
        b(at + 2) = CByte((v >> 8) And &HFF)
        b(at + 3) = CByte(v And &HFF)
    End Sub

    Private Shared Sub WriteChunk(fs As FileStream, kind As String, data As Byte())
        Dim len(3) As Byte
        PutBE(len, 0, CUInt(data.Length))
        fs.Write(len, 0, 4)

        Dim body(4 + data.Length - 1) As Byte
        For i = 0 To 3
            body(i) = CByte(Asc(kind(i)))
        Next
        If data.Length > 0 Then Array.Copy(data, 0, body, 4, data.Length)
        fs.Write(body, 0, body.Length)

        Dim crc(3) As Byte
        PutBE(crc, 0, Crc32(body))
        fs.Write(crc, 0, 4)
    End Sub

    Private Shared crcTable As UInteger()

    Private Shared Function Crc32(b As Byte()) As UInteger
        If crcTable Is Nothing Then
            Dim tbl(255) As UInteger
            For n = 0 To 255
                Dim c As UInteger = CUInt(n)
                For k = 0 To 7
                    If (c And 1UI) <> 0UI Then
                        c = &HEDB88320UI Xor (c >> 1)
                    Else
                        c >>= 1
                    End If
                Next
                tbl(n) = c
            Next
            crcTable = tbl
        End If
        Dim crc As UInteger = &HFFFFFFFFUI
        For Each v In b
            crc = crcTable((crc Xor v) And &HFFUI) Xor (crc >> 8)
        Next
        Return crc Xor &HFFFFFFFFUI
    End Function

    Private Shared Function Adler32(b As Byte()) As UInteger
        Dim a As UInteger = 1UI, s As UInteger = 0UI
        For Each v In b
            a = (a + v) Mod 65521UI
            s = (s + a) Mod 65521UI
        Next
        Return (s << 16) Or a
    End Function
End Class
