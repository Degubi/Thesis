#r "nuget: KnowledgePicker.WordCloud"
#r "nuget: FSharp.Collections.ParallelSeq"

open System
open System.Collections.Generic
open System.Diagnostics
open System.IO
open System.Text.Encodings.Web
open System.Text.Json
open System.Text.RegularExpressions
open FSharp.Collections.ParallelSeq
open KnowledgePicker.WordCloud
open KnowledgePicker.WordCloud.Coloring
open KnowledgePicker.WordCloud.Drawing
open KnowledgePicker.WordCloud.Layouts
open KnowledgePicker.WordCloud.Primitives
open KnowledgePicker.WordCloud.Sizers
open SkiaSharp

type WordStat = IDictionary<string, int>
type POSStat = {
    frequency: int
    averageWordLength: float
    mostFrequentWords: IDictionary<string, int>
    longestWords: IDictionary<string, int>
}
type AnalysisStat = {
    wordFrequencies: IDictionary<string, WordStat>
    wordLengths: IDictionary<string, int>
    posStats: IDictionary<string, POSStat>
}

let mainDir = Directory.GetParent(__SOURCE_DIRECTORY__).FullName
let magyarlancOutputDir = $"{mainDir}/outputs/analyze/magyarlanc"
let statsOutputDir = $"{mainDir}/outputs/stats"
let magyarlancStatsOutputDir = $"{statsOutputDir}/magyarlanc"
let wordCloudOutputDir = $"{mainDir}/outputs/word_cloud"
let chartOutputDir = $"{mainDir}/outputs/chart"
let magyarlancPosDistributionChartOutputDir = $"{chartOutputDir}/magyarlanc/pos_distribution"
let magyarlancPosChangesPerGradeChartDir = $"{chartOutputDir}/magyarlanc/pos_changes"
let magyarlancWordCloudOutputDir = $"{wordCloudOutputDir}/magyarlanc"
let mnszDataFilePath = $"{mainDir}/outputs/analyze/hnc-1.3-wordfreq.txt"

let statJsonSettings = JsonSerializerOptions(WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping)

let posChartIgnoresPOSes = [| "PART"; "X"; "INTJ"; "SYM"; "AUX"; "PUNCT" |]
let perGradeUsedPOSes = [| "NOUN"; "VERB"; "PROPN" |]
let wordCloudPOSes = [| ""; "NOUN"; "VERB"; "ADJ" |]
let magyarlancAnalysisIgnoredPOSes = [| "PUNCT"; ""; "SYM"; "X" |]


let parseMagyarlancWordFrequencies file =
    let calculatePOSCounts(args: seq<string[]>) = args |> Seq.countBy(fun k -> k.[2]) |> dict

    file |> File.ReadLines
         |> Seq.filter(fun k -> k <> String.Empty)
         |> Seq.map(fun k -> k.Split '\t')
         |> Seq.filter(fun k -> not(Array.contains k.[2] magyarlancAnalysisIgnoredPOSes))
         |> Seq.groupBy(fun k -> k.[0])
         |> Seq.map(fun (word, args) -> {| word = word; posCounts = calculatePOSCounts args |})
         |> Seq.toArray

let parseMNSZWordFrequencies file =
    let noGibberishWordFilter = Regex("^[a-zA-Z0-9]*$")
    let posTransformer = function
        | "N" -> "NOUN"
        | "V" -> "VERB"
        | "A" -> "ADJ"
        | k -> k

    let calculatePOSCounts(args: seq<string[]>) =
        args |> Seq.groupBy(fun k -> posTransformer k.[3])
             |> Seq.map(fun (pos, statsForPOS) -> (pos, statsForPOS |> Seq.sumBy(fun k -> int k.[7])))
             |> dict

    file |> File.ReadLines
         |> Seq.map(fun k -> k.Split '\t')
         |> Seq.filter(fun k -> noGibberishWordFilter.IsMatch k.[0])
         |> Seq.groupBy(fun k -> k.[0])
         |> Seq.map(fun (word, args) -> {| word = word; posCounts = calculatePOSCounts args |})
         |> Seq.toArray

let createAnalysisStat(wordFrequencies: {| word: string; posCounts: IDictionary<string, int> |}[]) =
    let createPOSStat pos =
        let wordsToCountsForPOS = wordFrequencies |> Seq.filter(fun k -> k.posCounts.ContainsKey pos)
                                                  |> Seq.map(fun k -> (k.word, k.posCounts.[pos]))
                                                  |> dict

        let mostFrequentWords = wordsToCountsForPOS |> Seq.sortByDescending(fun k -> k.Value)
                                                    |> Seq.map(fun k -> (k.Key, k.Value))

        let longestWords = wordsToCountsForPOS.Keys |> Seq.map(fun k -> (k, k.Length))
                                                    |> Seq.sortByDescending(fun (_, length) -> length)
        {
            frequency = wordsToCountsForPOS |> Seq.sumBy(fun k -> k.Value)
            averageWordLength = wordsToCountsForPOS |> Seq.averageBy(fun k -> float k.Key.Length)
            mostFrequentWords = mostFrequentWords |> dict
            longestWords = longestWords |> dict
        }

    let posStats = wordFrequencies |> Seq.collect(fun k -> k.posCounts.Keys)
                                   |> Seq.distinct
                                   |> Seq.map(fun pos -> (pos, createPOSStat pos))
                                   |> Seq.sortByDescending(fun (_, stats) -> stats.frequency)
    {
        wordFrequencies = wordFrequencies |> Seq.map(fun k -> (k.word, k.posCounts)) |> dict
        wordLengths = wordFrequencies |> Seq.map(fun k -> (k.word, k.word.Length)) |> dict
        posStats = posStats |> dict
    }


let mergeDictionary<'V, 'K when 'K: equality>(valueMerger: seq<'V> -> 'V) (dict1: IDictionary<'K, 'V>) (dict2: IDictionary<'K, 'V>) =
    Seq.append dict1 dict2 |> Seq.groupBy(fun k -> k.Key)
                           |> Seq.map(fun (key, values) -> (key, valueMerger (values |> Seq.map(fun k -> k.Value))))
                           |> dict

let mergeAnalysisStats s1 s2 =
    let mergeWordFrequency = Seq.fold(mergeDictionary Seq.sum) (dict([]))
    let mergePOSStat k = {
        frequency = k |> Seq.sumBy(fun k -> k.frequency)
        averageWordLength = k |> Seq.averageBy(fun k -> k.averageWordLength)
        mostFrequentWords = k |> Seq.map(fun k -> k.mostFrequentWords)
                              |> Seq.fold(mergeDictionary Seq.sum) (dict [])
        longestWords = k |> Seq.map(fun k -> k.longestWords)
                         |> Seq.fold(mergeDictionary Seq.max) (dict [])
    }

    {
        wordFrequencies = mergeDictionary mergeWordFrequency s1.wordFrequencies s2.wordFrequencies
        wordLengths = mergeDictionary Seq.head s1.wordLengths s2.wordLengths
        posStats = mergeDictionary mergePOSStat s1.posStats s2.posStats
    }

let truncateAnalysisStat stat =
    let truncatedWordFrequencies = stat.wordFrequencies |> Seq.sortByDescending(fun k -> k.Value.Values |> Seq.sum)
                                                        |> Seq.truncate 100
                                                        |> Seq.map(fun k -> (k.Key, k.Value))

    let truncatedWordLengths = stat.wordLengths |> Seq.sortByDescending(fun k -> k.Value)
                                                |> Seq.truncate 50
                                                |> Seq.map(fun k -> (k.Key, k.Value))

    let truncateMostFrequentWords(mostFrequentWords: IDictionary<string, int>) =
        mostFrequentWords |> Seq.sortByDescending(fun k -> k.Value)
                          |> Seq.truncate 5
                          |> Seq.map(fun k -> (k.Key, k.Value))
                          |> dict

    let trucateLongestWords(longestWords: IDictionary<string, int>) =
        longestWords |> Seq.sortByDescending(fun k -> k.Value)
                     |> Seq.truncate 5
                     |> Seq.map(fun k -> (k.Key, k.Value))
                     |> dict

    let truncatedPOSStats = stat.posStats |> Seq.map(fun k -> (k.Key, {
                                                                frequency = k.Value.frequency
                                                                averageWordLength = k.Value.averageWordLength
                                                                longestWords = trucateLongestWords k.Value.longestWords
                                                                mostFrequentWords = truncateMostFrequentWords k.Value.mostFrequentWords
                                                            }))
                                          |> Seq.sortByDescending(fun (_, stats) -> stats.frequency)
    {
        wordFrequencies = truncatedWordFrequencies |> dict
        wordLengths = truncatedWordLengths |> dict
        posStats = truncatedPOSStats |> dict
    }

let getBookType(fileName: string) = fileName.[fileName.IndexOf '_' + 1 ..]
let getBookGrade(fileName: string) = fileName.[.. (fileName.IndexOf '_' - 1)]
let writeChart chartData = Process.Start("python", [| "genChart.py"; JsonSerializer.Serialize(chartData) |]).WaitForExit()

let writeWordCloud(wordFrequencies: IDictionary<string, WordStat>) (baseFilePath: string) (pos: string) =
    let calculateWordCountBasedOnPOS(posCounts: IDictionary<string, int>) = if posCounts.ContainsKey pos then posCounts.[pos] else 0
    let calculateTotalWordCount(posCounts: IDictionary<string, int>) = posCounts |> Seq.sumBy(fun k -> k.Value)

    let wordFrequencyCalculator = if pos = "" then calculateTotalWordCount else calculateWordCountBasedOnPOS

    let topWordsToWrite = wordFrequencies |> Seq.map(fun k -> (k.Key, wordFrequencyCalculator k.Value))
                                          |> Seq.sortByDescending(fun (_, count) -> count)
                                          |> Seq.truncate 500
                                          |> Seq.map(WordCloudEntry)

    let wordCloud = WordCloudInput(topWordsToWrite, Width = 1920, Height = 1080, MinFontSize = 1, MaxFontSize = 84)
    use engine = new SkGraphicEngine(LogSizer(wordCloud), wordCloud)
    let generator = WordCloudGenerator<SKBitmap>(wordCloud, engine, SpiralLayout(wordCloud), RandomColorizer())

    use bitmap = new SKBitmap(wordCloud.Width, wordCloud.Height)
    use canvas = new SKCanvas(bitmap)
    canvas.Clear(SKColors.White)
    canvas.DrawBitmap(generator.Draw(), SKPoint(0F, 0F))

    let filePostfix = if pos = "" then "" else $"_{pos}"
    use image = bitmap.Encode(SKEncodedImageFormat.Jpeg, 100)
    image.SaveTo(File.Open($"{baseFilePath}{filePostfix}.jpg", FileMode.Create))

let writePieChart filePath legendTitle data = writeChart {|
    filePath = filePath
    chartType = "pie"
    legendTitle = legendTitle
    data = data
|}

let writeDottedLineChart filePath legendTitle data = writeChart {|
    filePath = filePath
    chartType = "dotted_line"
    legendTitle = legendTitle
    data = data
|}

let writeAnalysisStat fileName stats =
    let wordCloudBaseFilePath = $"{magyarlancWordCloudOutputDir}/{fileName}"

    File.WriteAllText($"{magyarlancStatsOutputDir}/{fileName}.json", JsonSerializer.Serialize(stats |> truncateAnalysisStat, statJsonSettings))
    wordCloudPOSes |> Seq.iter(writeWordCloud stats.wordFrequencies wordCloudBaseFilePath)
    stats.posStats |> Seq.filter(fun k -> not(Array.contains k.Key posChartIgnoresPOSes))
                   |> Seq.map(fun k -> (k.Key, k.Value.frequency))
                   |> Seq.sortByDescending(fun (_, count) -> count)
                   |> dict
                   |> writePieChart $"{magyarlancPosDistributionChartOutputDir}/{fileName}.png" "Szófaj"


let beginTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
let emptyStats = { wordFrequencies = dict([]); wordLengths = dict([]); posStats = dict([]) }

let perBookStats = Directory.GetFiles magyarlancOutputDir
                 |> PSeq.map(fun k -> (Path.GetFileNameWithoutExtension k, k |> parseMagyarlancWordFrequencies |> createAnalysisStat))
                 |> PSeq.toArray

let perBookTypeStats = perBookStats |> Seq.groupBy(fun (fileName, _) -> getBookType fileName)
                                    |> dict

let szgyStats = perBookTypeStats.["szgy"] |> Seq.map(fun (_, stats) -> stats)
                                          |> Seq.fold mergeAnalysisStats emptyStats

let tkStats = perBookTypeStats.["tk"] |> Seq.map(fun (_, stats) -> stats)
                                      |> Seq.fold mergeAnalysisStats emptyStats

let mergedStats = mergeAnalysisStats szgyStats tkStats

Directory.CreateDirectory magyarlancStatsOutputDir
Directory.CreateDirectory magyarlancWordCloudOutputDir
Directory.CreateDirectory magyarlancPosDistributionChartOutputDir
Directory.CreateDirectory magyarlancPosChangesPerGradeChartDir

perBookStats |> PSeq.iter(fun (fileName, stats) -> writeAnalysisStat fileName stats)
perBookTypeStats |> PSeq.iter(fun k ->
    let posStatLookup = k.Value |> Seq.map(fun (fileName, stats) -> (stats.posStats, getBookGrade fileName))
                                |> Seq.sortBy(fun (_, grade) -> int grade)
                                |> dict

    perGradeUsedPOSes |> Seq.map(fun pos -> (pos, posStatLookup |> Seq.map(fun n -> (n.Value, n.Key.[pos].frequency)) |> dict))
                      |> dict
                      |> writeDottedLineChart $"{magyarlancPosChangesPerGradeChartDir}/{k.Key}.png" "Szófaj"
)

writeAnalysisStat "szgy_merged" szgyStats
writeAnalysisStat "tk_merged" tkStats
writeAnalysisStat "merged" mergedStats


if File.Exists mnszDataFilePath then
    let mnszAnalysisStat = mnszDataFilePath |> parseMNSZWordFrequencies |> createAnalysisStat

    File.WriteAllText($"{statsOutputDir}/mnsz.json", JsonSerializer.Serialize(mnszAnalysisStat |> truncateAnalysisStat, statJsonSettings))
    wordCloudPOSes |> Seq.iter(writeWordCloud mnszAnalysisStat.wordFrequencies $"{wordCloudOutputDir}/mnsz")

let endTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
printfn "All done in %dms" (endTime - beginTime)