'   Copyright 2021-2023 Artem Drobanov (artem.drobanov@gmail.com)

'   Licensed under the Apache License, Version 2.0 (the "License");
'   you may Not use this file except In compliance With the License.
'   You may obtain a copy Of the License at

'     http://www.apache.org/licenses/LICENSE-2.0

'   Unless required by applicable law Or agreed To In writing, software
'   distributed under the License Is distributed On an "AS IS" BASIS,
'   WITHOUT WARRANTIES Or CONDITIONS Of ANY KIND, either express Or implied.
'   See the License For the specific language governing permissions And
'   limitations under the License.

Imports System.IO
Imports System.Threading

''' <summary>
''' Модуль голосования на битовом уровне с весовыми коэффициентами.
''' </summary>
Module BitVote
    ''' <summary>
    ''' Результат обработки.
    ''' </summary>
    Public Structure ProcessResult
        ''' <summary> Количество откорректированных ошибок. </summary>
        Public ReadOnly CorrectedErrorCount As Integer
        ''' <summary> Количество неоткорректированных ошибок при N >= 3 (количество несовпадающих позиций при N=2). </summary>
        Public ReadOnly UncorrectedErrorCount As Integer
        Public Sub New(correctedErrorCount As Integer, uncorrectedErrorCount As Integer)
            Me.CorrectedErrorCount = correctedErrorCount
            Me.UncorrectedErrorCount = uncorrectedErrorCount
        End Sub
    End Structure

    ''' <summary>
    ''' Заполнение входных буферов данных на основе потоков.
    ''' </summary>
    ''' <param name="inputBuffers">Входные буферы данных (массивы данных томов).</param>
    ''' <param name="inputStreams">Входные потоки томов.</param>
    ''' <param name="rowsCount">Используемое количество строк во входных буферах.</param>
    Public Sub FillInputBuffers(inputBuffers As Byte()(), inputStreams As Stream(), rowsCount As Integer)
        Parallel.For(0, inputStreams.Length, Sub(i As Integer)
                                                 Dim done As Integer = 0 : Dim task As Integer = rowsCount
                                                 While task > 0
                                                     done += inputStreams(i).Read(inputBuffers(i), done, task) : task = rowsCount - done
                                                 End While
                                             End Sub)
    End Sub

    ''' <summary>
    ''' Голосование на битовом уровне с весовыми коэффициентами.
    ''' </summary>
    ''' <param name="inputBuffers">Входные буферы данных (массивы данных томов).</param>
    ''' <param name="output">Результат битового голосования с исправленными ошибками.</param>
    ''' <param name="rowsCount">Используемое количество строк во входных буферах.</param>
    ''' <param name="weights">Весовые коэффициенты томов данных.</param>
    ''' <returns>Количество корректируемых и некорректируемых ошибок.</returns>
    Public Function Process(inputBuffers As Byte()(), output As Byte(), rowsCount As Integer,
                            Optional weights As Integer() = Nothing) As ProcessResult
        Dim correctedErrors = 0 'Корректируемые ошибки
        Dim uncorrectedErrors = 0 'Некорректируемые ошибки - неопределенности
        If weights Is Nothing Then 'Если веса томов не установлены...
            weights = New Integer(inputBuffers.Length - 1) {}
            For i = 0 To weights.Length - 1
                weights(i) = 1 '...все тома равноценны
            Next
        Else
            If weights.Length <> inputBuffers.Length Then
                Throw New Exception("BitVote.Process(): weights.Length <> inputBuffers.Length")
            End If
        End If
        Parallel.For(0, rowsCount, Sub(row As Integer)
                                       Dim N = inputBuffers.Length
                                       Dim slice = New Byte(N - 1) {} 'Срез данных томов
                                       For i = 0 To N - 1 'Заполнение среза данных томов
                                           slice(i) = inputBuffers(i)(row)
                                       Next
                                       Dim votedByteRes = Vote(slice, weights) 'Голосуем на срезе байт томов...
                                       If votedByteRes >= 0 Then '...если получился определенный результат...
                                           output(row) = votedByteRes '...сохраняем его...
                                           For i = 0 To N - 1 '...и после голосования проверяем...
                                               If output(row) <> slice(i) Then '...есть ли в срезе хотя бы один отличающийся байт
                                                   Interlocked.Increment(correctedErrors) 'Т.к. голосования было определенным - это откорректированная ошибка
                                                   Exit For
                                               End If
                                           Next
                                       Else 'Отрицательный результат - неопределенность в голосовании, пишем "пустое место"
                                           output(row) = 0
                                           Interlocked.Increment(uncorrectedErrors) 'Неопределенный результат - некорректируемая ошибка
                                       End If
                                   End Sub)
        Return New ProcessResult(correctedErrors, uncorrectedErrors)
    End Function

    ''' <summary>
    ''' Метод побитового голосования на наботе байт с весовыми коэффициентами.
    ''' </summary>
    ''' <param name="slice">Срез томов данных для проведения голосования.</param>
    ''' <param name="weights">Весовые коэффициенты томов в голосовании.</param>
    ''' <returns>Значение итогового байта (если >=0, при результате -1 неопределенность).</returns>
    Private Function Vote(slice As Byte(), weights As Integer()) As Integer
        Dim c0, c1, c2, c3, c4, c5, c6, c7 As Integer
        Dim b0, b1, b2, b3, b4, b5, b6, b7 As Integer
        b0 = 1 : b1 = 2 : b2 = 4 : b3 = 8 : b4 = 16 : b5 = 32 : b6 = 64 : b7 = 128
        For i = 0 To slice.Length - 1
            Dim s = slice(i) : Dim w = weights(i)
            'Накопление весов по позициям бит.
            'Инкремент счетчика весом - плюсуем за гипотезу "Бит = 1".
            If (s And b0) <> 0 Then c0 += w Else c0 -= w
            If (s And b1) <> 0 Then c1 += w Else c1 -= w
            If (s And b2) <> 0 Then c2 += w Else c2 -= w
            If (s And b3) <> 0 Then c3 += w Else c3 -= w
            If (s And b4) <> 0 Then c4 += w Else c4 -= w
            If (s And b5) <> 0 Then c5 += w Else c5 -= w
            If (s And b6) <> 0 Then c6 += w Else c6 -= w
            If (s And b7) <> 0 Then c7 += w Else c7 -= w
        Next
        'Проверка счетчиков гипотез по каждому биту
        If c0 * c1 * c2 * c3 * c4 * c5 * c6 * c7 <> 0 Then
            If c0 < 0 Then b0 = 0
            If c1 < 0 Then b1 = 0
            If c2 < 0 Then b2 = 0
            If c3 < 0 Then b3 = 0
            If c4 < 0 Then b4 = 0
            If c5 < 0 Then b5 = 0
            If c6 < 0 Then b6 = 0
            If c7 < 0 Then b7 = 0
            Return b0 Or b1 Or b2 Or b3 Or b4 Or b5 Or b6 Or b7
        Else
            Return -1 'Если хотя бы один счетчик гипотез нулевой, есть неопределенность
        End If
    End Function
End Module
