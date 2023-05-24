Imports System.IO
Imports System.Text

Module Program
    Sub Main(args As String())
        Console.CursorVisible = False
        Console.ForegroundColor = ConsoleColor.Green
        Console.WriteLine("FileRepair 1.00   Copyright (c) 2021-2023 Artem Drobanov   24 May 2023")
        Console.ForegroundColor = ConsoleColor.Gray
        Console.WriteLine()
        Dim candidates As New List(Of FileInfo)()
        For Each fileName In args
            If File.Exists(fileName) Then
                Dim fi = New FileInfo(fileName)
                candidates.Add(fi)
                Console.WriteLine($"Cand. : ""{fi.Name}"", {fi.Length} [{candidates.Count}]")
            End If
        Next
        Dim inputSize = VoteLongs(candidates.Select(Function(item) item.Length)) '�������� �������� ����� ������������� ������ �����
        Console.WriteLine($"Size  : {inputSize}")
        If candidates.Any() Then Console.WriteLine()
        Dim inputFiles = candidates.Where(Function(item) item.Length = inputSize).ToList()
        For i = 0 To inputFiles.Count - 1
            Console.WriteLine($"File  : ""{inputFiles(i).Name}"", {inputFiles(i).Length} [{i + 1}]")
        Next
        If inputFiles.Any() Then Console.WriteLine()
        Console.WriteLine($"Count : {candidates.Count} files")
        Console.WriteLine($"Input : {inputSize \ (1024 * 1024)} Mb")
        If inputFiles.Count >= 2 Then
            Dim MB = 1024 * 1024 '1 ��������
            Dim bufferedStreamBufsize = 128 * MB '������ ������ �� �������� �����: 128 ��
            Dim bufferSize = 1 * MB '������ ������ ��� ���������: 1 ��
            '�������� �������� ������� - ���� � �����
            Dim inputStreams = inputFiles.Select(Function(item) New BufferedStream(New FileStream(item.FullName, FileMode.Open), bufferedStreamBufsize)).ToArray()
            Dim outputStream = New BufferedStream(New FileStream(inputFiles.First.FullName + ".repaired", FileMode.Create), bufferedStreamBufsize)
            '��������� ������ ��� �������� ������
            Dim inputBuffers = New Byte(inputStreams.Count - 1)() {}
            For i = 0 To inputBuffers.Length - 1
                inputBuffers(i) = New Byte(bufferSize - 1) {}
            Next
            Dim outputBuffer = New Byte(bufferSize) {}
            '������ �� �������� ������� / ���������
            Dim outputSize As Long = 0
            Dim totalBlockCount As Long = 0
            Dim correctedErrorCountTotal As Long = 0
            Dim uncorrectedErrorCountTotal As Long = 0
            Dim errorMap As New Queue(Of Char)({"["c})
            Dim startTime = Now
            While outputSize < inputSize
                Dim processDataCount = inputSize - outputSize
                Dim rowsCount = Math.Min(processDataCount, inputBuffers(0).Length)
                BitVote.FillInputBuffers(inputBuffers, inputStreams, rowsCount)
                Dim result = BitVote.Process(inputBuffers, outputBuffer, rowsCount)
                totalBlockCount += 1
                correctedErrorCountTotal += result.CorrectedErrorCount
                uncorrectedErrorCountTotal += result.UncorrectedErrorCount
                outputStream.Write(outputBuffer, 0, rowsCount)
                outputSize += rowsCount
                Dim spdMbps = outputSize / (1024 * 1024 * (Now - startTime).TotalSeconds)
                Console.Write($"Output: {outputStream.Length \ (1024 * 1024)} MB; Corr/Uncorr:{correctedErrorCountTotal}/{uncorrectedErrorCountTotal}; [{spdMbps.ToString("F1")} MB/s] {vbCr}")
                errorMap.Enqueue(If(result.UncorrectedErrorCount > 0, "U"c, If(result.CorrectedErrorCount > 0, "c"c, "."))) '���������� ����� ������: "./c/U" -> "��� ������/��������������/����������������"
            End While
            errorMap.Enqueue("]"c)
            errorMap.Enqueue(";"c)
            errorMap.Enqueue($"{vbCr}")
            errorMap.Enqueue($"{vbLf}")
            For Each c In $"Stat: [./c/U = {totalBlockCount}/{correctedErrorCountTotal}/{uncorrectedErrorCountTotal}].".ToArray()
                errorMap.Enqueue(c)
            Next
            Console.WriteLine()
            '�������� �������� �������
            For Each fs In inputStreams
                Try
                    fs.Close()
                Catch
                End Try
            Next
            With outputStream
                .Flush()
                .Close()
            End With
            '������ ����� ������ � ��������
            Dim sb As New StringBuilder()
            With sb
                .AppendLine("<ERROR MAP>")
                .AppendLine(DateTime.Now.ToString())
                .AppendLine()
                .AppendLine("Content: [...c...U...], 1 MB/symbol (./c/U);")
                .AppendLine("Legend:  '.' - normal data, 'c' - corrected error, 'U' - uncorrected error;")
                .AppendLine()
                .Append($"Map:  {New String(errorMap.ToArray())}")
                .AppendLine()
            End With
            File.WriteAllText(inputFiles.First.FullName + ".ErrorMap.txt", sb.ToString())
        Else
            Console.WriteLine()
            Console.WriteLine("Usage: FileRepair <fileCopy1> <fileCopy2> <fileCopy3> ... <fileCopyN> (>= 2 files with the equal size)")
            Console.WriteLine("Output file name (auto): <fileCopy1> + '.repaired'")
            Console.WriteLine("Error map:")
            Console.WriteLine("  File name (auto): <fileCopy1> + '.ErrorMap.txt';")
            Console.WriteLine("  Content: [...c...U...], 1 MB/symbol (./c/U);")
            Console.WriteLine("  Legend:  '.' - normal data, 'c' - corrected error, 'U' - uncorrected error.")
        End If
    End Sub

    ''' <summary>
    ''' ����������� �� ������ long-��������.
    ''' </summary>
    ''' <param name="values">�������� ��������.</param>
    ''' <returns>�������� ����� ������������� ��������.</returns>
    Private Function VoteLongs(values As IEnumerable(Of Long)) As Long
        Dim result As Long = -1
        If values.Any() Then
            Dim counters = New Dictionary(Of Long, Integer)()
            For Each value In values
                If Not counters.ContainsKey(value) Then
                    counters(value) = 0
                Else
                    counters(value) += 1
                End If
            Next
            Dim maxCounter = counters.Max(Function(item) item.Value)
            result = counters.First(Function(item) item.Value = maxCounter).Key
        End If
        Return result
    End Function
End Module
